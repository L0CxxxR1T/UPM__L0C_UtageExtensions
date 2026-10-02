using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// FMOD 化されたラベル（Bgm / Ambience / Se）について、Unity 側の AssetFile ロードを
    /// CustomLoadManager.OnFindAsset 経由で差し止める
    /// ボイスは PCM 抽出のため実際に Unity 側でロードする必要があるので対象外にする
    /// （settingData が IAssetFileSoundSettingDataKey を実装するのは AdvSoundSettingData のみで、
    /// 　ボイス用の AdvVoiceSetting はこれを実装しないため、自然に対象から外れる）
    /// </summary>
    internal static class FmodAssetFileInterceptor
    {
        private static CustomLoadManager.FindAsset _handler;
        private static FmodSoundSettings _settings;

        public static void Register(FmodSoundSettings settings)
        {
            if (settings == null || !settings.SuppressUnityAudioLoad)
            {
                return;
            }

            CustomLoadManager customLoadManager = GetOrCreateCustomLoadManager();
            if (customLoadManager == null)
            {
                return;
            }

            _settings = settings;
            _handler = OnFindAsset;
            customLoadManager.OnFindAsset += _handler;
        }

        public static void Unregister()
        {
            if (_handler == null)
            {
                return;
            }

            CustomLoadManager customLoadManager = GetOrCreateCustomLoadManager();
            if (customLoadManager != null)
            {
                customLoadManager.OnFindAsset -= _handler;
            }
            _handler = null;
            _settings = null;
        }

        private static CustomLoadManager GetOrCreateCustomLoadManager()
        {
            AssetFileManager manager = AssetFileManager.GetInstance();
            if (manager == null)
            {
                Debug.LogError("[UtageFmod] AssetFileManager が見つからないため、Unity 側ロード抑止を有効化できません。");
                return null;
            }

            CustomLoadManager customLoadManager = manager.GetComponent<CustomLoadManager>();
            if (customLoadManager == null)
            {
                customLoadManager = manager.gameObject.AddComponent<CustomLoadManager>();
            }
            return customLoadManager;
        }

        private static void OnFindAsset(AssetFileManager manager, AssetFileInfo fileInfo, IAssetFileSettingData settingData, ref AssetFileBase asset)
        {
            if (_settings == null)
            {
                return;
            }

            if (settingData is IAssetFileSoundSettingDataKey keyedSetting && _settings.HasEventForLabel(keyedSetting.Key))
            {
                asset = new FmodDummyAssetFile(manager, fileInfo, settingData);
            }
        }
    }
}
