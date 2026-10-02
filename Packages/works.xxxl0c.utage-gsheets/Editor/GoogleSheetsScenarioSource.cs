using UnityEngine;

namespace XXXL0C.UtagePlugins.GoogleSheets
{
    // Googleスプレッドシートの参照情報を持つアセット。
    // シナリオフォルダ（AdvScenarioDataProject.ChapterData.scenarioDir）配下に置くことで、
    // Utage の通常のアセット列挙経路に乗せてインポート対象にする。
    [CreateAssetMenu(menuName = "Utage/ScenarioFileReader/GoogleSheetsSource", fileName = "GoogleSheetsSource")]
    public class GoogleSheetsScenarioSource : ScriptableObject
    {
        [SerializeField, Tooltip("スプレッドシートのID、またはスプレッドシートのURL")]
        private string _spreadsheetId;

        public string SpreadsheetId => _spreadsheetId;
    }
}
