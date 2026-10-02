using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// FmodParam カスタムコマンドを登録するマネージャー
    /// Utage の AdvEngine と同じヒエラルキー配下（子でも可）に AddComponent すること
    /// </summary>
    [AddComponentMenu("UtageFmod/Fmod Custom Command Manager")]
    public sealed class FmodCustomCommandManager : AdvCustomCommandManager
    {
        public const string IdFmodParam = "FmodParam";

        public override void OnBootInit()
        {
            AdvCommandParser.OnCreateCustomCommandFromID += OnCreateCustomCommandFromID;
        }

        private static void OnCreateCustomCommandFromID(string id, StringGridRow row, AdvSettingDataManager dataManager, ref AdvCommand command)
        {
            if (id == IdFmodParam)
            {
                command = new AdvCommandFmodParam(row);
            }
        }
    }
}
