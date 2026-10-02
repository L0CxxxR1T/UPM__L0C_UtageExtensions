using UnityEditor;
using UnityEngine;

namespace XXXL0C.UtagePlugins.GoogleSheets
{
    [CustomEditor(typeof(AdvScenarioFileReaderSettingsGoogleSheets))]
    public class AdvScenarioFileReaderSettingsGoogleSheetsEditor : Editor
    {
        private const string AppsScriptTemplate = @"// Google Apps Script for Utage Scenario Reader
// スプレッドシートの「拡張機能」→「Apps Script」にこのコードを貼り付けてください。

function doGet(e) {
  var sheetId = e.parameter.sheetId;
  var ss = SpreadsheetApp.openById(sheetId);
  var sheets = ss.getSheets().map(function(sheet) {
    return {
      name: sheet.getName(),
      rows: sheet.getDataRange().getValues().map(function(row) {
        return {
          cells: row.map(function(cell) {
            return cell === null || cell === undefined ? '' : String(cell);
          })
        };
      })
    };
  });

  var json = JSON.stringify({ sheets: sheets });
  return ContentService.createTextOutput(json).setMimeType(ContentService.MimeType.JSON);
}";

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Google Apps Script セットアップ", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                EditorGUILayout.HelpBox(
                    "Googleスプレッドシートからシナリオを読み込むには、Apps Scriptのセットアップが必要です。\n" +
                    "以下のいずれかの方法でセットアップしてください。",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Apps Scriptを表示・コピー", GUILayout.Height(30)))
                {
                    ShowAppsScriptTemplate();
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("スプレッドシート版テンプレートをコピー", GUILayout.Height(30)))
                {
                    Application.OpenURL("https://docs.google.com/spreadsheets/d/1ckd2f6qHxrukdgO5hXI9Zr5QvEQjO9Stvx-ymjF_Uis/copy");
                }
            }
        }

        private void ShowAppsScriptTemplate()
        {
            AppsScriptTemplateWindow.ShowWindow(AppsScriptTemplate);
        }
    }

    public class AppsScriptTemplateWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _template;

        public static void ShowWindow(string template)
        {
            AppsScriptTemplateWindow window = GetWindow<AppsScriptTemplateWindow>(true, "Apps Scriptテンプレート", true);
            window.minSize = new Vector2(500, 400);
            window._template = template;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Google Apps Script コード", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.TextArea(_template, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("コピー", GUILayout.Height(30)))
            {
                GUIUtility.systemCopyBuffer = _template;
                Debug.Log("コードをコピーしました");
            }

            if (GUILayout.Button("閉じる", GUILayout.Height(30)))
            {
                Close();
            }
        }
    }
}
