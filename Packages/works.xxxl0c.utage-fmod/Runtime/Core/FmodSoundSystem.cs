using FMOD.Studio;
using FMODUnity;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Utage;
using UtageExtensions;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// Utage のサウンド再生を FMOD Studio に差し替えるシステム本体
    /// SoundManager.OnCreateSoundSystem 経由で SoundManager.System に差し込んで使う（FmodSoundBootstrap 参照）
    /// </summary>
    public sealed class FmodSoundSystem : SoundManagerSystemInterface, SoundManagerSystemInterfaceEx
    {
        private readonly FmodSoundSettings _settings;
        private readonly Dictionary<string, FmodSoundGroup> _groups = new Dictionary<string, FmodSoundGroup>(StringComparer.Ordinal);
        private readonly Dictionary<string, VCA> _vcaCache = new Dictionary<string, VCA>(StringComparer.Ordinal);
        private readonly FmodParameterController _parameterController;
        private List<string> _saveStreamNameList;
        private SoundManager _soundManager;
        private VCA _masterVca;

        public FmodSoundSystem(FmodSoundSettings settings)
        {
            _settings = settings;
            _parameterController = new FmodParameterController(GetGroupIfExists);
        }

        public void Init(SoundManager soundManager, List<string> saveStreamNameList)
        {
            _soundManager = soundManager;
            _saveStreamNameList = saveStreamNameList;

            if (_settings == null)
            {
                Debug.LogError("[UtageFmod] FmodSoundSettings が設定されていません。FmodSoundBootstrap のインスペクタを確認してください。");
                return;
            }

            if (!string.IsNullOrEmpty(_settings.MasterVcaPath))
            {
                _masterVca = ResolveVca(_settings.MasterVcaPath);
            }
        }

        /// <summary>
        /// 毎フレームの更新（フェード進行・インスタンス回収・VCA への音量反映）。FmodSoundBootstrap から呼ばれる
        /// </summary>
        public void Update(float deltaTime)
        {
            foreach (KeyValuePair<string, FmodSoundGroup> pair in _groups)
            {
                pair.Value.Update(deltaTime);
                ApplyGroupVcaVolume(pair.Key, pair.Value);
            }
            _parameterController.Update(deltaTime);
        }

        public void ReleaseAll()
        {
            foreach (FmodSoundGroup group in _groups.Values)
            {
                group.ReleaseAll();
            }
            _groups.Clear();
        }

        private FmodSoundGroup GetGroupAndCreateIfMissing(string groupName)
        {
            if (!_groups.TryGetValue(groupName, out FmodSoundGroup group))
            {
                group = new FmodSoundGroup(groupName, (label, data, fadeInTime) => CreateInstance(groupName, label, data, fadeInTime));
                _groups.Add(groupName, group);
            }
            return group;
        }

        private FmodSoundInstance CreateInstance(string groupName, string label, SoundData data, float fadeInTime)
        {
            EventInstance eventInstance;
            bool isVoice = string.Equals(groupName, SoundManager.IdVoice, StringComparison.Ordinal);

            if (isVoice)
            {
                if (!FmodVoicePcmSoundFactory.TryCreateVoiceInstance(_settings.VoiceEvent, data, out eventInstance))
                {
                    return null;
                }
            }
            else if (_settings.TryGetEvent(data, out EventReference eventRef))
            {
                try
                {
                    eventInstance = RuntimeManager.CreateInstance(eventRef);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UtageFmod] ラベル \"{label}\" の FMOD イベント生成に失敗しました: {e.Message}");
                    return null;
                }
            }
            else
            {
                return null;
            }

            return new FmodSoundInstance(eventInstance, label, data, fadeInTime);
        }

        // ================= SoundManagerSystemInterface =================

        public void Play(string groupName, string label, SoundData soundData, float fadeInTime, float fadeOutTime)
        {
            FmodSoundGroup group = GetGroupAndCreateIfMissing(groupName);
            group.Play(label, soundData, fadeInTime, fadeOutTime);
        }

        public void Stop(string groupName, string label, float fadeTime)
        {
            GetGroupIfExists(groupName)?.Stop(label, fadeTime);
        }

        public bool IsPlaying(string groupName, string label)
        {
            return GetGroupIfExists(groupName)?.IsPlaying(label) ?? false;
        }

        public AudioSource GetAudioSource(string groupName, string label)
        {
            // FMOD 実装では Unity の AudioSource を経由しないため常に null（AdvScenarioSound 側は null 許容）
            return null;
        }

        public float GetSamplesVolume(string groupName, string label)
        {
            return GetGroupIfExists(groupName)?.GetSamplesVolume(label) ?? 0f;
        }

        public void StopGroup(string groupName, float fadeTime)
        {
            GetGroupIfExists(groupName)?.StopAll(fadeTime);
        }

        public void StopGroupIgnoreLoop(string groupName, float fadeTime)
        {
            GetGroupIfExists(groupName)?.StopAllIgnoreLoop(fadeTime);
        }

        public void StopAll(float fadeTime)
        {
            foreach (FmodSoundGroup group in _groups.Values)
            {
                group.StopAll(fadeTime);
            }
        }

        public void StopAllLoop(float fadeTime)
        {
            foreach (FmodSoundGroup group in _groups.Values)
            {
                group.StopAllLoop(fadeTime);
            }
        }

        public float GetMasterVolume(string groupName)
        {
            return GetGroupAndCreateIfMissing(groupName).MasterVolume;
        }

        public void SetMasterVolume(string groupName, float volume)
        {
            GetGroupAndCreateIfMissing(groupName).MasterVolume = volume;
        }

        public float GetGroupVolume(string groupName)
        {
            return GetGroupAndCreateIfMissing(groupName).GroupVolume;
        }

        public void SetGroupVolume(string groupName, float volume, float fadeTime = 0)
        {
            FmodSoundGroup group = GetGroupAndCreateIfMissing(groupName);
            group.GroupVolume = volume;
            group.GroupVolumeFadeTime = fadeTime;
        }

        public bool IsMultiPlay(string groupName)
        {
            return GetGroupAndCreateIfMissing(groupName).MultiPlay;
        }

        public void SetMultiPlay(string groupName, bool multiPlay)
        {
            GetGroupAndCreateIfMissing(groupName).MultiPlay = multiPlay;
        }

        public void WriteSaveData(BinaryWriter writer)
        {
            List<string> targets = _saveStreamNameList ?? new List<string>();
            writer.Write(targets.Count);
            foreach (string groupName in targets)
            {
                writer.Write(groupName);
                writer.WriteBuffer(GetGroupAndCreateIfMissing(groupName).Write);
            }
        }

        public void ReadSaveDataBuffer(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string groupName = reader.ReadString();
                reader.ReadBuffer(GetGroupAndCreateIfMissing(groupName).Read);
            }
        }

        public bool IsLoading
        {
            get
            {
                foreach (FmodSoundGroup group in _groups.Values)
                {
                    if (group.IsLoading)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        // ================= SoundManagerSystemInterfaceEx =================

        public bool IsPlaying(string groupName)
        {
            return GetGroupIfExists(groupName)?.IsPlaying() ?? false;
        }

        public bool IsPlaying(string groupName, string label, AssetFile file)
        {
            return GetGroupIfExists(groupName)?.IsPlaying(label, file) ?? false;
        }

        public void OnChangedMasterVolume()
        {
            if (_masterVca.hasHandle() && _soundManager != null)
            {
                _masterVca.setVolume(_soundManager.MasterVolume);
            }
        }

        public void OnChangedTaggedMasterVolume(SoundManager.TaggedMasterVolume taggedMasterVolume)
        {
            if (_settings != null && _settings.TryGetTaggedVcaPath(taggedMasterVolume.Tag, out string vcaPath))
            {
                VCA vca = ResolveVca(vcaPath);
                if (vca.hasHandle())
                {
                    vca.setVolume(taggedMasterVolume.Volume);
                }
                return;
            }

            // VCA が対応づけられていなければ、タグ・ラベルが一致する再生中インスタンスへ直接反映する
            foreach (FmodSoundGroup group in _groups.Values)
            {
                group.ApplyTagVolume(taggedMasterVolume.Tag, taggedMasterVolume.Volume);
                foreach (string label in taggedMasterVolume.Labels)
                {
                    group.ApplyTagVolume(label, taggedMasterVolume.Volume);
                }
            }
        }

        // ================= FmodParam コマンド用 =================

        /// <summary>
        /// FMOD パラメータを設定する（FmodParam コマンドから呼ばれる）。
        /// groupName が null ならグローバルパラメータ、それ以外は該当グループの再生中インスタンスが対象
        /// duration が 0 以下なら即時設定、それ以外は補間する
        /// </summary>
        public void SetParameter(string groupName, string label, string paramName, float value, float duration)
        {
            _parameterController.SetParameter(groupName, label, paramName, value, duration);
        }

        /// <summary>Labeled Parameter を設定する（補間非対応、常に即時設定）</summary>
        public void SetParameterLabel(string groupName, string label, string paramName, string valueLabel)
        {
            _parameterController.SetParameterLabel(groupName, label, paramName, valueLabel);
        }

        /// <summary>指定パラメータの補間が進行中か（WaitType による終了待ちで使う）</summary>
        public bool IsParameterTweening(string groupName, string label, string paramName)
        {
            return _parameterController.IsTweening(groupName, label, paramName);
        }

        /// <summary>進行中の補間があれば即座に完了させる（スキップ用）</summary>
        public void SkipParameterTween(string groupName, string label, string paramName)
        {
            _parameterController.CompleteImmediately(groupName, label, paramName);
        }

        private FmodSoundGroup GetGroupIfExists(string groupName)
        {
            return _groups.TryGetValue(groupName, out FmodSoundGroup group) ? group : null;
        }

        private void ApplyGroupVcaVolume(string groupName, FmodSoundGroup group)
        {
            if (_settings == null || !_settings.TryGetGroupVcaPath(groupName, out string vcaPath))
            {
                return;
            }
            VCA vca = ResolveVca(vcaPath);
            if (vca.hasHandle())
            {
                vca.setVolume(group.EffectiveVcaVolume);
            }
        }

        private VCA ResolveVca(string vcaPath)
        {
            if (string.IsNullOrEmpty(vcaPath))
            {
                return default;
            }
            if (_vcaCache.TryGetValue(vcaPath, out VCA cached))
            {
                return cached;
            }
            try
            {
                VCA vca = RuntimeManager.GetVCA(vcaPath);
                _vcaCache[vcaPath] = vca;
                return vca;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UtageFmod] VCA \"{vcaPath}\" の取得に失敗しました: {e.Message}");
                return default;
            }
        }
    }
}
