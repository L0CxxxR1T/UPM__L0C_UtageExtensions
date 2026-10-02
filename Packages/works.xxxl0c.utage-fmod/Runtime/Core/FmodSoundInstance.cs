using FMOD;
using FMOD.Studio;
using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// FMOD の EventInstance 1本のラッパー
    /// フェードイン・アウト、実効音量の反映、リップシンク用メータリングを担当する
    /// </summary>
    internal sealed class FmodSoundInstance
    {
        private enum FadeState
        {
            None,
            FadingIn,
            FadingOut,
        }

        public string Label { get; }
        public AssetFile File { get; }
        public bool IsLoop { get; }
        public string Tag { get; }
        public SoundData Data { get; }

        public bool IsReleased { get; private set; }

        private EventInstance _eventInstance;
        private readonly float _dataVolume;
        private float _tagVolume = 1f;
        private float _fadeVolume;
        private float _fadeElapsed;
        private float _fadeDuration;
        private FadeState _fadeState;

        public FmodSoundInstance(EventInstance eventInstance, string label, SoundData data, float fadeInTime)
        {
            _eventInstance = eventInstance;
            Label = label;
            Data = data;
            File = data.File;
            IsLoop = data.IsLoop;
            Tag = data.Tag;
            _dataVolume = data.Volume;

            if (data.EnableIntroLoop)
            {
                UnityEngine.Debug.LogWarning($"[UtageFmod] ラベル \"{label}\" はイントロループ設定を持っていますが、FMOD 実装では未対応です。FMOD Studio 側のループリージョンで表現してください。");
            }

            _fadeVolume = fadeInTime > 0f ? 0f : 1f;
            ApplyVolume();
            _eventInstance.start();

            if (fadeInTime > 0f)
            {
                BeginFadeIn(fadeInTime);
            }
        }

        public bool IsPlaying
        {
            get
            {
                if (IsReleased)
                {
                    return false;
                }
                RESULT result = _eventInstance.getPlaybackState(out PLAYBACK_STATE state);
                if (result != RESULT.OK)
                {
                    return false;
                }
                return state != PLAYBACK_STATE.STOPPED && state != PLAYBACK_STATE.STOPPING;
            }
        }

        /// <summary>
        /// キャラ別音量（SoundManager.TaggedMasterVolume）の反映。VCA が対応づけられていない場合のフォールバック
        /// </summary>
        public void SetTagVolume(float volume)
        {
            _tagVolume = volume;
            ApplyVolume();
        }

        public void BeginFadeIn(float fadeTime)
        {
            if (fadeTime <= 0f)
            {
                _fadeVolume = 1f;
                _fadeState = FadeState.None;
                ApplyVolume();
                return;
            }
            _fadeDuration = fadeTime;
            _fadeElapsed = _fadeVolume * fadeTime;
            _fadeState = FadeState.FadingIn;
        }

        /// <summary>
        /// フェードアウトを開始する。完了すると自動的に停止・解放される
        /// </summary>
        public void BeginFadeOutAndRelease(float fadeTime)
        {
            if (IsReleased)
            {
                return;
            }
            if (fadeTime <= 0f)
            {
                StopImmediateAndRelease();
                return;
            }
            _fadeDuration = fadeTime;
            _fadeElapsed = (1f - _fadeVolume) * fadeTime;
            _fadeState = FadeState.FadingOut;
        }

        public void StopImmediateAndRelease()
        {
            if (IsReleased)
            {
                return;
            }
            _eventInstance.stop(STOP_MODE.IMMEDIATE);
            _eventInstance.release();
            IsReleased = true;
        }

        /// <summary>
        /// フェード進行と再生完了検知を行う。毎フレーム呼び出すこと
        /// </summary>
        /// <returns>解放済みで破棄してよい場合は true</returns>
        public bool Update(float deltaTime)
        {
            if (IsReleased)
            {
                return true;
            }

            if (_fadeState != FadeState.None)
            {
                _fadeElapsed += deltaTime;
                float t = _fadeDuration > 0f ? Mathf.Clamp01(_fadeElapsed / _fadeDuration) : 1f;
                _fadeVolume = _fadeState == FadeState.FadingIn ? t : 1f - t;
                ApplyVolume();

                if (t >= 1f)
                {
                    bool wasFadingOut = _fadeState == FadeState.FadingOut;
                    _fadeState = FadeState.None;
                    if (wasFadingOut)
                    {
                        StopImmediateAndRelease();
                        return true;
                    }
                }
            }

            if (!IsLoop && !IsPlaying)
            {
                StopImmediateAndRelease();
                return true;
            }

            return false;
        }

        /// <summary>
        /// リップシンク等で使う 0〜1 の音量。Utage 既定実装（SoundAudio.GetSamplesVolume）と同じ変換式
        /// </summary>
        public float GetSamplesVolume()
        {
            if (IsReleased || !IsPlaying)
            {
                return 0f;
            }

            RESULT result = _eventInstance.getChannelGroup(out ChannelGroup channelGroup);
            if (result != RESULT.OK || !channelGroup.hasHandle())
            {
                return 0f;
            }

            result = channelGroup.getDSP(CHANNELCONTROL_DSP_INDEX.HEAD, out DSP headDsp);
            if (result != RESULT.OK || !headDsp.hasHandle())
            {
                return 0f;
            }

            headDsp.setMeteringEnabled(false, true);
            result = headDsp.getMeteringInfo(System.IntPtr.Zero, out DSP_METERING_INFO metering);
            if (result != RESULT.OK || metering.numchannels <= 0)
            {
                return 0f;
            }

            float rms = 0f;
            for (int i = 0; i < metering.numchannels; i++)
            {
                float level = metering.rmslevel[i];
                rms += level * level;
            }
            rms = Mathf.Sqrt(rms / metering.numchannels);

            float dbVolume = 20.0f * Mathf.Log10(rms); // デシベルに変換する式（Utage の SoundAudio.GetSamplesVolume と同じ）
            return Mathf.Clamp01(Mathf.InverseLerp(-80.0f, 0.0f, dbVolume));
        }

        private void ApplyVolume()
        {
            if (IsReleased)
            {
                return;
            }
            _eventInstance.setVolume(_dataVolume * _tagVolume * _fadeVolume);
        }

        /// <summary>
        /// FMOD イベントローカルパラメータに float 値を設定する（FmodParam コマンド用）
        /// </summary>
        public void SetParameter(string paramName, float value, bool ignoreSeekSpeed)
        {
            if (IsReleased)
            {
                return;
            }
            RESULT result = _eventInstance.setParameterByName(paramName, value, ignoreSeekSpeed);
            if (result != RESULT.OK)
            {
                UnityEngine.Debug.LogError($"[UtageFmod] パラメータ \"{paramName}\" の設定に失敗しました（ラベル \"{Label}\"）: {result}");
            }
        }

        /// <summary>
        /// FMOD イベントローカルの Labeled Parameter を設定する（FmodParam コマンド用）
        /// </summary>
        public void SetParameterLabel(string paramName, string label)
        {
            if (IsReleased)
            {
                return;
            }
            RESULT result = _eventInstance.setParameterByNameWithLabel(paramName, label);
            if (result != RESULT.OK)
            {
                UnityEngine.Debug.LogError($"[UtageFmod] パラメータ \"{paramName}\" へのラベル \"{label}\" 設定に失敗しました（インスタンスラベル \"{Label}\"）: {result}");
            }
        }

        /// <summary>
        /// FMOD イベントローカルパラメータの現在値を取得する（補間の開始値取得用）
        /// </summary>
        public bool TryGetParameter(string paramName, out float value)
        {
            if (IsReleased)
            {
                value = 0f;
                return false;
            }
            RESULT result = _eventInstance.getParameterByName(paramName, out value);
            return result == RESULT.OK;
        }
    }
}
