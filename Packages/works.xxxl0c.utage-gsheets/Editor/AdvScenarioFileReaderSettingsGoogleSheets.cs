using UnityEngine;

namespace XXXL0C.UtagePlugins.GoogleSheets
{
    [CreateAssetMenu(menuName = "Utage/ScenarioFileReader/GoogleSheets", fileName = "GoogleSheetsFileReaderSettings")]
    public class AdvScenarioFileReaderSettingsGoogleSheets : Utage.ScenarioFileReaderSettings
    {
        [SerializeField, Tooltip("Google Apps Script WebアプリのURL（必須）")]
        private string _appsScriptUrl;

        [SerializeField, Tooltip("シートのコメント記号（この文字で始まるシート名はインポートから除外する。'\\0'で無効化）")]
        private char _sheetCommentPrefix = '#';

        public string AppsScriptUrl => _appsScriptUrl;
        public char SheetCommentPrefix => _sheetCommentPrefix;

        public override Utage.IAdvScenarioFileReader CreateReader()
        {
            return new AdvScenarioFileReaderGoogleSheets(this);
        }
    }
}
