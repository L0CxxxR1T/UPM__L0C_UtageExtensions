using FMODUnity;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// Utage のサウンドラベルと FMOD イベントの対応づけ、および VCA との連携を保持する設定アセット
    /// </summary>
    [CreateAssetMenu(menuName = "UtageFmod/Fmod Sound Settings", fileName = "FmodSoundSettings")]
    public sealed class FmodSoundSettings : ScriptableObject
    {
        /// <summary>
        /// Utage のサウンドラベル1つと FMOD イベントの対応
        /// </summary>
        [Serializable]
        public sealed class LabelEventEntry
        {
            [SerializeField] private string _label = string.Empty;
            [SerializeField] private EventReference _eventRef;

            public string Label => _label;
            public EventReference EventRef => _eventRef;
        }

        /// <summary>
        /// キー文字列（グループ名またはタグ名）と FMOD の VCA パスの対応
        /// </summary>
        [Serializable]
        public sealed class NamedVcaEntry
        {
            [SerializeField] private string _name = string.Empty;
            [SerializeField] private string _vcaPath = string.Empty;

            public string Name => _name;
            public string VcaPath => _vcaPath;
        }

        [Header("BGM / Ambience / SE のラベル対応")]
        [SerializeField] private List<LabelEventEntry> _labelEvents = new List<LabelEventEntry>();

        [Header("ボイス再生用の共通イベント（Programmer Instrument を1つ持つ）")]
        [SerializeField] private EventReference _voiceEvent;

        [Header("グループ音量を流し込む VCA")]
        [SerializeField] private List<NamedVcaEntry> _groupVcas = new List<NamedVcaEntry>();
        [SerializeField] private string _masterVcaPath = string.Empty;

        [Header("キャラ別音量（TaggedMasterVolume）を流し込む VCA。未設定のタグは再生中インスタンスへ直接反映する")]
        [SerializeField] private List<NamedVcaEntry> _taggedVcas = new List<NamedVcaEntry>();

        [Header("Unity 側の AudioClip ロード抑止")]
        [Tooltip("有効にすると、FMOD 化対象のサウンドについて Unity 側の AudioClip ロードを行わない")]
        [SerializeField] private bool _suppressUnityAudioLoad = true;

        public EventReference VoiceEvent => _voiceEvent;
        public string MasterVcaPath => _masterVcaPath;
        public bool SuppressUnityAudioLoad => _suppressUnityAudioLoad;

        [NonSerialized] private Dictionary<string, EventReference> _labelToEventCache;
        [NonSerialized] private Dictionary<string, string> _groupToVcaCache;
        [NonSerialized] private Dictionary<string, string> _taggedToVcaCache;

        /// <summary>
        /// SoundData からラベルを解決して対応する FMOD イベントを取得
        /// </summary>
        public bool TryGetEvent(SoundData data, out EventReference eventRef)
        {
            BuildLabelCacheIfNeeded();

            string key = ResolveLabel(data);
            if (!string.IsNullOrEmpty(key) && _labelToEventCache.TryGetValue(key, out eventRef))
            {
                return true;
            }

            Debug.LogWarning($"[UtageFmod] ラベル \"{key}\" に対応する FMOD イベントが登録されていません。");
            eventRef = default;
            return false;
        }

        /// <summary>
        /// ラベルに対応する FMOD イベントが登録済みか（警告ログを出さない静かな確認用。ロード抑止の判定に使う）
        /// </summary>
        public bool HasEventForLabel(string label)
        {
            BuildLabelCacheIfNeeded();
            return !string.IsNullOrEmpty(label) && _labelToEventCache.ContainsKey(label);
        }

        /// <summary>
        /// グループ名から対応する VCA パスを取得
        /// </summary>
        public bool TryGetGroupVcaPath(string groupName, out string vcaPath)
        {
            BuildLabelCacheIfNeeded();
            return _groupToVcaCache.TryGetValue(groupName, out vcaPath);
        }

        /// <summary>
        /// タグ名から対応する VCA パスを取得
        /// </summary>
        public bool TryGetTaggedVcaPath(string tag, out string vcaPath)
        {
            BuildLabelCacheIfNeeded();
            return _taggedToVcaCache.TryGetValue(tag, out vcaPath);
        }

        private static string ResolveLabel(SoundData data)
        {
            if (data.File != null && data.File.SettingData is IAssetFileSoundSettingDataKey keyedSetting)
            {
                return keyedSetting.Key;
            }
            return data.Name;
        }

        private void BuildLabelCacheIfNeeded()
        {
            if (_labelToEventCache != null)
            {
                return;
            }

            _labelToEventCache = new Dictionary<string, EventReference>(_labelEvents.Count, StringComparer.Ordinal);
            foreach (LabelEventEntry entry in _labelEvents)
            {
                if (string.IsNullOrEmpty(entry.Label))
                {
                    continue;
                }
                if (_labelToEventCache.ContainsKey(entry.Label))
                {
                    Debug.LogWarning($"[UtageFmod] ラベル \"{entry.Label}\" が重複登録されています。後勝ちで上書きします。");
                }
                _labelToEventCache[entry.Label] = entry.EventRef;
            }

            _groupToVcaCache = new Dictionary<string, string>(_groupVcas.Count, StringComparer.Ordinal);
            foreach (NamedVcaEntry entry in _groupVcas)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }
                _groupToVcaCache[entry.Name] = entry.VcaPath;
            }

            _taggedToVcaCache = new Dictionary<string, string>(_taggedVcas.Count, StringComparer.Ordinal);
            foreach (NamedVcaEntry entry in _taggedVcas)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }
                _taggedToVcaCache[entry.Name] = entry.VcaPath;
            }
        }

        private void OnEnable()
        {
            _labelToEventCache = null;
            _groupToVcaCache = null;
            _taggedToVcaCache = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _labelToEventCache = null;
            _groupToVcaCache = null;
            _taggedToVcaCache = null;
        }
#endif
    }
}
