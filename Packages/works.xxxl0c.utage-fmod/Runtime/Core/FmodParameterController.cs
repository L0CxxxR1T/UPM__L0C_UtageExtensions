using System;
using System.Collections.Generic;
using FMOD;
using FMODUnity;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// FmodParam コマンドが操作する FMOD パラメータの補間処理をまとめて管理する
    /// グローバルパラメータは RuntimeManager.StudioSystem に、グループ対象は FmodSoundGroup 経由で再生中インスタンスに反映する
    /// </summary>
    internal sealed class FmodParameterController
    {
        /// <summary>進行中の float パラメータ補間1件分</summary>
        private sealed class Tween
        {
            public string GroupName; // null ならグローバル対象
            public string Label;     // null ならグループ内の全インスタンス対象
            public string ParamName;
            public float From;
            public float To;
            public float Duration;
            public float Elapsed;
        }

        private readonly Func<string, FmodSoundGroup> _getGroupIfExists;
        private readonly Dictionary<string, Tween> _tweens = new Dictionary<string, Tween>(StringComparer.Ordinal);

        public FmodParameterController(Func<string, FmodSoundGroup> getGroupIfExists)
        {
            _getGroupIfExists = getGroupIfExists;
        }

        /// <summary>
        /// float パラメータを設定する。duration が 0 以下なら即時設定、それ以外は補間を開始する
        /// </summary>
        public void SetParameter(string groupName, string label, string paramName, float value, float duration)
        {
            string key = MakeKey(groupName, label, paramName);
            _tweens.Remove(key);

            if (duration <= 0f)
            {
                ApplyImmediate(groupName, label, paramName, value, ignoreSeekSpeed: false);
                return;
            }

            float from = TryGetCurrentValue(groupName, label, paramName, out float current) ? current : 0f;
            _tweens[key] = new Tween
            {
                GroupName = groupName,
                Label = label,
                ParamName = paramName,
                From = from,
                To = value,
                Duration = duration,
                Elapsed = 0f,
            };
        }

        /// <summary>Labeled Parameter を設定する。補間には対応しないため常に即時設定</summary>
        public void SetParameterLabel(string groupName, string label, string paramName, string valueLabel)
        {
            string key = MakeKey(groupName, label, paramName);
            _tweens.Remove(key);

            if (groupName == null)
            {
                RESULT result = RuntimeManager.StudioSystem.setParameterByNameWithLabel(paramName, valueLabel);
                if (result != RESULT.OK)
                {
                    Debug.LogError($"[UtageFmod] グローバルパラメータ \"{paramName}\" へのラベル \"{valueLabel}\" 設定に失敗しました: {result}");
                }
                return;
            }

            FmodSoundGroup group = _getGroupIfExists(groupName);
            if (group == null || !group.HasAnyTargetInstance(label))
            {
                Debug.LogWarning($"[UtageFmod] FmodParam: 対象インスタンスが見つかりません（グループ \"{groupName}\" ラベル \"{label ?? "(全て)"}\"）。パラメータ \"{paramName}\" は設定されません。");
                return;
            }
            group.SetParameterLabel(label, paramName, valueLabel);
        }

        /// <summary>指定キーの補間が進行中か</summary>
        public bool IsTweening(string groupName, string label, string paramName)
        {
            return _tweens.ContainsKey(MakeKey(groupName, label, paramName));
        }

        /// <summary>補間を即座に完了させる（スキップ用）</summary>
        public void CompleteImmediately(string groupName, string label, string paramName)
        {
            string key = MakeKey(groupName, label, paramName);
            if (_tweens.TryGetValue(key, out Tween tween))
            {
                ApplyImmediate(tween.GroupName, tween.Label, tween.ParamName, tween.To, ignoreSeekSpeed: true);
                _tweens.Remove(key);
            }
        }

        public void Update(float deltaTime)
        {
            if (_tweens.Count == 0)
            {
                return;
            }

            List<string> finished = null;
            foreach (KeyValuePair<string, Tween> pair in _tweens)
            {
                Tween tween = pair.Value;
                tween.Elapsed += deltaTime;
                float t = Mathf.Clamp01(tween.Elapsed / tween.Duration);
                float value = Mathf.Lerp(tween.From, tween.To, t);
                ApplyImmediate(tween.GroupName, tween.Label, tween.ParamName, value, ignoreSeekSpeed: true);

                if (t >= 1f)
                {
                    (finished ??= new List<string>()).Add(pair.Key);
                }
            }

            if (finished != null)
            {
                foreach (string key in finished)
                {
                    _tweens.Remove(key);
                }
            }
        }

        private void ApplyImmediate(string groupName, string label, string paramName, float value, bool ignoreSeekSpeed)
        {
            if (groupName == null)
            {
                RESULT result = RuntimeManager.StudioSystem.setParameterByName(paramName, value, ignoreSeekSpeed);
                if (result != RESULT.OK)
                {
                    Debug.LogError($"[UtageFmod] グローバルパラメータ \"{paramName}\" の設定に失敗しました: {result}");
                }
                return;
            }

            FmodSoundGroup group = _getGroupIfExists(groupName);
            if (group == null || !group.HasAnyTargetInstance(label))
            {
                Debug.LogWarning($"[UtageFmod] FmodParam: 対象インスタンスが見つかりません（グループ \"{groupName}\" ラベル \"{label ?? "(全て)"}\"）。パラメータ \"{paramName}\" は設定されません。");
                return;
            }
            group.SetParameter(label, paramName, value, ignoreSeekSpeed);
        }

        private bool TryGetCurrentValue(string groupName, string label, string paramName, out float value)
        {
            if (groupName == null)
            {
                RESULT result = RuntimeManager.StudioSystem.getParameterByName(paramName, out value);
                return result == RESULT.OK;
            }

            FmodSoundGroup group = _getGroupIfExists(groupName);
            if (group == null)
            {
                value = 0f;
                return false;
            }
            return group.TryGetParameter(label, paramName, out value);
        }

        private static string MakeKey(string groupName, string label, string paramName)
        {
            return (groupName ?? string.Empty) + "|" + (label ?? string.Empty) + "|" + paramName;
        }
    }
}
