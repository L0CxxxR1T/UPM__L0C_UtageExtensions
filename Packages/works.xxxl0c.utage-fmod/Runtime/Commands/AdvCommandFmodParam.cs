using System.Globalization;
using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// カスタムコマンド：FMOD パラメータの設定
    /// Arg1=パラメータ名 / Arg2=値（floatかラベル文字列） / Arg3=対象（Global(既定)/Bgm/Ambience/Se/Voice）
    /// Arg4=ラベル（対象がグループの時のみ、省略時はグループ内の再生中インスタンス全部）
    /// Arg6=補間時間（秒、既定0=即時） / WaitType=補間完了待ち（既定NoWait）
    /// </summary>
    public sealed class AdvCommandFmodParam : AdvCommandWaitBase, IAdvCommandEffect, IAdvCommandUpdateWait
    {
        private readonly string _paramName;
        private readonly bool _isLabelValue;
        private readonly float _floatValue;
        private readonly string _labelValue;
        private readonly string _groupName; // null = グローバル対象
        private readonly string _instanceLabel; // null = グループ内の全ラベル対象
        private readonly float _duration;

        private AdvEngine _engine;
        private FmodSoundSystem _system;

        public AdvCommandFmodParam(StringGridRow row) : base(row)
        {
            _paramName = ParseCell<string>(AdvColumnName.Arg1);

            string rawValue = ParseCell<string>(AdvColumnName.Arg2);
            if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _floatValue))
            {
                _isLabelValue = false;
            }
            else
            {
                _isLabelValue = true;
                _labelValue = rawValue;
            }

            string scope = ParseCellOptional<string>(AdvColumnName.Arg3, "Global");
            _groupName = ResolveGroupName(scope);

            string label = ParseCellOptional<string>(AdvColumnName.Arg4, "");
            _instanceLabel = string.IsNullOrEmpty(label) ? null : label;

            _duration = ParseCellOptional<float>(AdvColumnName.Arg6, 0f);

            if (_isLabelValue && _duration > 0f)
            {
                Debug.LogWarning(ToErrorString($"FmodParam: ラベル値のパラメータ \"{_paramName}\" は補間に対応していません。即時設定にフォールバックします。"));
            }

            WaitType = ParseCellOptional(AdvColumnName.WaitType, AdvCommandWaitType.NoWait);
        }

        private static string ResolveGroupName(string scope)
        {
            switch (scope)
            {
                case "Global":
                    return null;
                case "Bgm":
                    return SoundManager.IdBgm;
                case "Ambience":
                    return SoundManager.IdAmbience;
                case "Se":
                    return SoundManager.IdSe;
                case "Voice":
                    return SoundManager.IdVoice;
                default:
                    Debug.LogError($"FmodParam: Arg3 の対象指定が不明です（\"{scope}\"）。Global として扱います。");
                    return null;
            }
        }

        protected override void OnStart(AdvEngine engine, AdvScenarioThread thread)
        {
            _engine = engine;
            _system = engine.SoundManager.System as FmodSoundSystem;
            if (_system == null)
            {
                Debug.LogError(ToErrorString("FmodParam: SoundManager.System が FmodSoundSystem ではありません。FmodSoundBootstrap が正しく設定されているか確認してください。"));
                return;
            }

            if (_isLabelValue)
            {
                _system.SetParameterLabel(_groupName, _instanceLabel, _paramName, _labelValue);
            }
            else
            {
                _system.SetParameter(_groupName, _instanceLabel, _paramName, _floatValue, _duration);
            }
        }

        public bool UpdateCheckWait()
        {
            if (_system == null)
            {
                return false;
            }
            return _system.IsParameterTweening(_groupName, _instanceLabel, _paramName);
        }

        public void OnEffectSkip()
        {
            _system?.SkipParameterTween(_groupName, _instanceLabel, _paramName);
        }

        public void OnEffectFinalize()
        {
            _engine = null;
            _system = null;
        }
    }
}
