using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// Utage の SoundManager に FMOD 実装を差し込むエントリーポイント
    /// SoundManager と同じ GameObject に付ける
    /// </summary>
    [AddComponentMenu("UtageFmod/Fmod Sound Bootstrap")]
    [DefaultExecutionOrder(-100)] // SoundManager.System への最初のアクセスより先に差し込みを済ませておく必要がある
    public sealed class FmodSoundBootstrap : MonoBehaviour
    {
        [SerializeField] private SoundManager _soundManager;
        [SerializeField] private FmodSoundSettings _settings;

        private FmodSoundSystem _system;

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_soundManager == null)
            {
                Debug.LogError("[UtageFmod] FmodSoundBootstrap に SoundManager が紐付けられていません。", this);
            }
            if (_settings == null)
            {
                Debug.LogError("[UtageFmod] FmodSoundBootstrap に FmodSoundSettings が紐付けられていません。", this);
            }
#endif
            FmodAssetFileInterceptor.Register(_settings);

            if (_soundManager == null)
            {
                return;
            }
            _soundManager.OnCreateSoundSystem.AddListener(OnCreateSoundSystem);
        }

        private void OnDestroy()
        {
            if (_soundManager != null)
            {
                _soundManager.OnCreateSoundSystem.RemoveListener(OnCreateSoundSystem);
            }
            _system?.ReleaseAll();
            FmodAssetFileInterceptor.Unregister();
        }

        private void Update()
        {
            _system?.Update(Time.deltaTime);
        }

        private void OnCreateSoundSystem(SoundManager soundManager)
        {
            _system = new FmodSoundSystem(_settings);
            soundManager.System = _system;
        }
    }
}
