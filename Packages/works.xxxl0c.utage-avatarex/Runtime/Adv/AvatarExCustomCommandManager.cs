using System;
using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// FileType=AvatarEx を AdvGraphicObjectAvatarEx に割り当てるための登録マネージャー
    /// Utage の AdvEngine と同じヒエラルキー配下（子でも可）に AddComponent すること
    /// </summary>
    [AddComponentMenu("UtageAvatarEx/Avatar Ex Custom Command Manager")]
    public sealed class AvatarExCustomCommandManager : AdvCustomCommandManager
    {
        public const string FileTypeAvatarEx = "AvatarEx";

        public override void OnBootInit()
        {
            // PlayMode開始のたびに CallbackCreateCustom は null にリセットされるため毎回登録し直す
            // エディタでのシナリオインポート時にも OnBootInit は呼ばれるため、多重登録を避けて -=; += する
            AdvGraphicInfo.CallbackCreateCustom -= OnCreateCustom;
            AdvGraphicInfo.CallbackCreateCustom += OnCreateCustom;
        }

        // AdvEngine.OnDestroy は CallbackCreateCustom をクリアしないため、
        // 破棄済みインスタンスへの参照が残らないよう static メソッドを登録する
        static void OnCreateCustom(string fileType, ref Type type)
        {
            if (fileType == FileTypeAvatarEx)
            {
                type = typeof(AdvGraphicObjectAvatarEx);
            }
        }
    }
}
