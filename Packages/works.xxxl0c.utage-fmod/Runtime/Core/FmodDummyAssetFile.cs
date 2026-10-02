using System;
using System.Collections;
using Utage;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// FMOD が管理するサウンドについて、Unity 側の AudioClip ロードを行わないダミーの AssetFile
    /// FmodAssetFileInterceptor が CustomLoadManager.OnFindAsset 経由で差し込む
    /// </summary>
    internal sealed class FmodDummyAssetFile : AssetFileBase
    {
        public FmodDummyAssetFile(AssetFileManager manager, AssetFileInfo fileInfo, IAssetFileSettingData settingData)
            : base(manager, fileInfo, settingData)
        {
        }

        public override bool CheckCacheOrLocal()
        {
            return true;
        }

        public override IEnumerator LoadAsync(Action onComplete, Action onFailed)
        {
            IsLoadEnd = true;
            IsLoadError = false;
            onComplete?.Invoke();
            yield break;
        }

        public override void Unload()
        {
            // 実体をロードしていないため何もしない
        }
    }
}
