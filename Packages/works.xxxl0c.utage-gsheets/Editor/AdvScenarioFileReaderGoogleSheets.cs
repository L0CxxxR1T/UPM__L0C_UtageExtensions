using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.GoogleSheets
{
    // Googleスプレッドシート形式のシナリオリーダー
    // GoogleSheetsScenarioSource アセット（AdvScenarioDataProject.ChapterData.scenarioDir 配下）を入力とし、
    // Apps Script 経由で全シートをJSONで取得してStringGridDictionaryに変換する
    public class AdvScenarioFileReaderGoogleSheets : IAdvScenarioFileReader
    {
        private readonly AdvScenarioFileReaderSettingsGoogleSheets _settings;

        public AdvScenarioFileReaderGoogleSheets(AdvScenarioFileReaderSettingsGoogleSheets settings)
        {
            _settings = settings;
        }

        public bool IsTargetFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return false;
            return AssetDatabase.LoadAssetAtPath<GoogleSheetsScenarioSource>(path) != null;
        }

        public bool TryReadFile(string path, out StringGridDictionary stringGridDictionary)
        {
            if (!IsTargetFile(path))
            {
                stringGridDictionary = null;
                return false;
            }

            GoogleSheetsScenarioSource source = AssetDatabase.LoadAssetAtPath<GoogleSheetsScenarioSource>(path);
            if (source == null || string.IsNullOrEmpty(source.SpreadsheetId))
            {
                Debug.LogError($"GoogleSheetsScenarioSourceにスプレッドシートIDが設定されていません: {path}");
                stringGridDictionary = null;
                return false;
            }

            if (string.IsNullOrEmpty(_settings.AppsScriptUrl))
            {
                Debug.LogError($"Apps Script URLが設定されていません。設定アセットを確認してください: {path}");
                stringGridDictionary = null;
                return false;
            }

            string sheetId = ExtractSheetId(source.SpreadsheetId);
            if (string.IsNullOrEmpty(sheetId))
            {
                Debug.LogError($"スプレッドシートIDを解決できませんでした: {source.SpreadsheetId}");
                stringGridDictionary = null;
                return false;
            }

            try
            {
                string json = FetchSheetsJson(sheetId);
                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogError($"Google Sheetsからのデータ取得に失敗しました: {path}");
                    stringGridDictionary = null;
                    return false;
                }

                SheetsResponseDto response = JsonUtility.FromJson<SheetsResponseDto>(json);
                if (response == null || response.sheets == null)
                {
                    Debug.LogError($"Google Sheetsのレスポンス形式が不正です: {path}\n{json}");
                    stringGridDictionary = null;
                    return false;
                }

                stringGridDictionary = new StringGridDictionary();
                foreach (SheetDto sheet in response.sheets)
                {
                    StringGrid grid = new StringGrid(path + ":" + sheet.name, sheet.name, CsvType.Csv);
                    if (sheet.rows != null)
                    {
                        foreach (RowDto row in sheet.rows)
                        {
                            grid.AddRow(TrimTrailingEmptyCells(row.cells));
                        }
                    }
                    grid.ParseHeader();
                    stringGridDictionary.Add(sheet.name, grid);
                }

                // コメント記号で始まるシート名は除外する（Excelリーダーと同じ挙動）
                char commentPrefix = _settings.SheetCommentPrefix;
                if (commentPrefix != '\0')
                {
                    stringGridDictionary.RemoveSheets("^" + System.Text.RegularExpressions.Regex.Escape(commentPrefix.ToString()));
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Google Sheetsの読み込み中にエラーが発生しました: {path}\n{e}");
                stringGridDictionary = null;
                return false;
            }
        }

        // getDataRange()は全行をシートの最大列数まで空文字で埋めて返す。
        // AnimationシートのTime行などは行末まで数値として読むため、Excelリーダーと同じく
        // 最後の非空セルまでで行を切る
        private static List<string> TrimTrailingEmptyCells(string[] cells)
        {
            if (cells == null) return new List<string>();

            int length = cells.Length;
            while (length > 0 && string.IsNullOrEmpty(cells[length - 1]))
            {
                length--;
            }

            List<string> result = new List<string>(length);
            for (int i = 0; i < length; i++)
            {
                result.Add(cells[i]);
            }
            return result;
        }

        // Apps ScriptにHTTPリクエストを送り、全シート分のJSONを取得する
        // TryReadFileはUtageの同期APIのため、UnityWebRequestは使わずHttpClientで同期的に待つ
        // （UnityWebRequestをメインスレッドでThread.Sleepブロックすると進行しないため）
        private string FetchSheetsJson(string sheetId)
        {
            string url = BuildRequestUrl(sheetId);

            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            try
            {
                return Task.Run(async () =>
                {
                    using HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Debug.LogError($"Google Sheetsへのリクエストが失敗しました: {url}\n{e.Message}");
                return null;
            }
        }

        private string BuildRequestUrl(string sheetId)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(_settings.AppsScriptUrl);
            builder.Append(_settings.AppsScriptUrl.Contains("?") ? "&" : "?");
            builder.Append("sheetId=").Append(Uri.EscapeDataString(sheetId));
            return builder.ToString();
        }

        // URLまたは生のIDからスプレッドシートIDを取り出す
        // 例: https://docs.google.com/spreadsheets/d/{SHEET_ID}/edit
        private string ExtractSheetId(string idOrUrl)
        {
            if (!idOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return idOrUrl;
            }

            try
            {
                Uri uri = new Uri(idOrUrl);
                string[] segments = uri.AbsolutePath.Split('/');
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] == "d" && i + 1 < segments.Length)
                    {
                        return segments[i + 1];
                    }
                }
            }
            catch (UriFormatException e)
            {
                Debug.LogError($"スプレッドシートURLの解析に失敗しました: {idOrUrl}\n{e.Message}");
            }

            return null;
        }

        [Serializable]
        private class SheetsResponseDto
        {
            public SheetDto[] sheets;
        }

        [Serializable]
        private class SheetDto
        {
            public string name;
            public RowDto[] rows;
        }

        [Serializable]
        private class RowDto
        {
            public string[] cells;
        }
    }
}
