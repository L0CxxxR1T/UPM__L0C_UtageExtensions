using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Utage;
using UtageExtensions;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// Utage の SoundGroup 相当。ラベル単位の再生管理（Add / Replay / NotPlaySame）と
    /// グループ全体の MultiPlay・音量フェードを担当する（MonoBehaviour ではない素の C# クラス）
    /// </summary>
    internal sealed class FmodSoundGroup
    {
        /// <summary>
        /// ラベル1つ分の再生状態。Utage の SoundAudioPlayer 相当
        /// </summary>
        private sealed class LabelPlayer
        {
            public string Label;
            public readonly List<FmodSoundInstance> AllInstances = new List<FmodSoundInstance>();
            public FmodSoundInstance Current;
            public FmodSoundInstance FadingOut;
            public SoundData PendingData;
            public float PendingFadeInTime;
            public float PendingDelayRemaining;

            public bool HasPending => PendingData != null;
        }

        public string GroupName { get; }

        public bool MultiPlay { get; set; }

        /// <summary>グループのマスターボリューム（SoundManager.SetMasterVolume 相当）</summary>
        public float MasterVolume { get; set; } = 1f;

        /// <summary>グループボリューム（フェード対応、SoundManager.SetGroupVolume 相当）</summary>
        public float GroupVolume
        {
            get => _groupVolume;
            set => _groupVolume = value;
        }

        public float GroupVolumeFadeTime { get; set; }

        /// <summary>フェードを考慮した現在のグループボリューム</summary>
        public float CurrentGroupVolume { get; private set; } = 1f;

        /// <summary>VCA に流し込む実効音量（MasterVolume × CurrentGroupVolume）</summary>
        public float EffectiveVcaVolume => MasterVolume * CurrentGroupVolume;

        public bool IsLoading => false; // FMOD 実装ではイベント再生開始が同期的なため、非同期ロード待ちの状態を持たない

        private readonly Func<string, SoundData, float, FmodSoundInstance> _instanceFactory;
        private readonly Dictionary<string, LabelPlayer> _players = new Dictionary<string, LabelPlayer>(StringComparer.Ordinal);
        private float _groupVolume = 1f;
        private float _groupVolumeVelocity;

        public FmodSoundGroup(string groupName, Func<string, SoundData, float, FmodSoundInstance> instanceFactory)
        {
            GroupName = groupName;
            _instanceFactory = instanceFactory;
        }

        public void Play(string label, SoundData data, float fadeInTime, float fadeOutTime)
        {
            if (!MultiPlay)
            {
                StopOtherLabels(label, fadeOutTime);
            }

            LabelPlayer player = GetOrCreatePlayer(label);
            switch (data.PlayMode)
            {
                case SoundPlayMode.Add:
                    PlayAdd(player, data, fadeInTime);
                    break;
                case SoundPlayMode.Replay:
                    PlayFade(player, data, fadeInTime, fadeOutTime, crossFade: true);
                    break;
                case SoundPlayMode.NotPlaySame:
                    if (IsSameFilePlaying(player, data))
                    {
                        return;
                    }
                    PlayFade(player, data, fadeInTime, fadeOutTime, crossFade: false);
                    break;
            }
        }

        public void Stop(string label, float fadeTime)
        {
            if (!_players.TryGetValue(label, out LabelPlayer player))
            {
                return;
            }
            StopPlayer(player, fadeTime);
        }

        public void StopAll(float fadeTime)
        {
            foreach (LabelPlayer player in _players.Values)
            {
                StopPlayer(player, fadeTime);
            }
        }

        public void StopAllLoop(float fadeTime)
        {
            foreach (LabelPlayer player in _players.Values)
            {
                if (player.Current != null && player.Current.IsLoop)
                {
                    StopPlayer(player, fadeTime);
                }
            }
        }

        public void StopAllIgnoreLoop(float fadeTime)
        {
            foreach (LabelPlayer player in _players.Values)
            {
                if (player.Current == null || !player.Current.IsLoop)
                {
                    StopPlayer(player, fadeTime);
                }
            }
        }

        public bool IsPlaying()
        {
            foreach (LabelPlayer player in _players.Values)
            {
                if (IsPlayerPlaying(player))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsPlaying(string label)
        {
            return _players.TryGetValue(label, out LabelPlayer player) && IsPlayerPlaying(player);
        }

        public bool IsPlaying(string label, AssetFile file)
        {
            if (!_players.TryGetValue(label, out LabelPlayer player))
            {
                return false;
            }
            foreach (FmodSoundInstance instance in player.AllInstances)
            {
                if (instance.IsPlaying && instance.File == file)
                {
                    return true;
                }
            }
            return false;
        }

        public float GetSamplesVolume(string label)
        {
            if (!_players.TryGetValue(label, out LabelPlayer player) || player.Current == null)
            {
                return 0f;
            }
            return player.Current.GetSamplesVolume();
        }

        /// <summary>
        /// キャラ別音量（VCA 未対応時のフォールバック）を該当インスタンスへ適用
        /// </summary>
        public void ApplyTagVolume(string tag, float volume)
        {
            foreach (LabelPlayer player in _players.Values)
            {
                foreach (FmodSoundInstance instance in player.AllInstances)
                {
                    if (string.Equals(instance.Tag, tag, StringComparison.Ordinal))
                    {
                        instance.SetTagVolume(volume);
                    }
                }
            }
        }

        /// <summary>
        /// FMOD パラメータを再生中インスタンスへ即時設定する（FmodParam コマンド用）。
        /// label が null なら全ラベル対象、それ以外は該当ラベルのみ
        /// </summary>
        public void SetParameter(string label, string paramName, float value, bool ignoreSeekSpeed)
        {
            foreach (FmodSoundInstance instance in EnumerateTargetInstances(label))
            {
                instance.SetParameter(paramName, value, ignoreSeekSpeed);
            }
        }

        /// <summary>Labeled Parameter を再生中インスタンスへ設定する（FmodParam コマンド用）</summary>
        public void SetParameterLabel(string label, string paramName, string valueLabel)
        {
            foreach (FmodSoundInstance instance in EnumerateTargetInstances(label))
            {
                instance.SetParameterLabel(paramName, valueLabel);
            }
        }

        /// <summary>
        /// 補間の開始値取得用。label 指定時はそのラベルの再生中インスタンス、未指定時は最初に見つかった再生中インスタンスから取得する
        /// </summary>
        public bool TryGetParameter(string label, string paramName, out float value)
        {
            foreach (FmodSoundInstance instance in EnumerateTargetInstances(label))
            {
                if (instance.TryGetParameter(paramName, out value))
                {
                    return true;
                }
            }
            value = 0f;
            return false;
        }

        /// <summary>label 指定時はそのラベル、未指定時は全ラベルの再生中インスタンスを列挙する</summary>
        public bool HasAnyTargetInstance(string label)
        {
            foreach (FmodSoundInstance instance in EnumerateTargetInstances(label))
            {
                return true;
            }
            return false;
        }

        private IEnumerable<FmodSoundInstance> EnumerateTargetInstances(string label)
        {
            if (!string.IsNullOrEmpty(label))
            {
                if (_players.TryGetValue(label, out LabelPlayer player))
                {
                    foreach (FmodSoundInstance instance in player.AllInstances)
                    {
                        if (instance.IsPlaying)
                        {
                            yield return instance;
                        }
                    }
                }
                yield break;
            }

            foreach (LabelPlayer player in _players.Values)
            {
                foreach (FmodSoundInstance instance in player.AllInstances)
                {
                    if (instance.IsPlaying)
                    {
                        yield return instance;
                    }
                }
            }
        }

        /// <summary>
        /// フェード進行・保留再生・終了インスタンスの回収。毎フレーム呼び出すこと
        /// </summary>
        public void Update(float deltaTime)
        {
            CurrentGroupVolume = UpdateFade(CurrentGroupVolume, GroupVolume, GroupVolumeFadeTime, ref _groupVolumeVelocity);

            foreach (LabelPlayer player in _players.Values)
            {
                UpdatePlayer(player, deltaTime);
            }
        }

        public void ReleaseAll()
        {
            foreach (LabelPlayer player in _players.Values)
            {
                foreach (FmodSoundInstance instance in player.AllInstances)
                {
                    instance.StopImmediateAndRelease();
                }
            }
            _players.Clear();
        }

        private void UpdatePlayer(LabelPlayer player, float deltaTime)
        {
            if (player.HasPending)
            {
                player.PendingDelayRemaining -= deltaTime;
                if (player.PendingDelayRemaining <= 0f)
                {
                    SoundData data = player.PendingData;
                    float fadeInTime = player.PendingFadeInTime;
                    player.PendingData = null;
                    player.Current = CreateAndTrack(player, data, fadeInTime);
                }
            }

            for (int i = player.AllInstances.Count - 1; i >= 0; i--)
            {
                FmodSoundInstance instance = player.AllInstances[i];
                if (instance.Update(deltaTime))
                {
                    player.AllInstances.RemoveAt(i);
                    if (player.Current == instance)
                    {
                        player.Current = null;
                    }
                    if (player.FadingOut == instance)
                    {
                        player.FadingOut = null;
                    }
                }
            }
        }

        private void PlayAdd(LabelPlayer player, SoundData data, float fadeInTime)
        {
            CreateAndTrack(player, data, fadeInTime);
        }

        private void PlayFade(LabelPlayer player, SoundData data, float fadeInTime, float fadeOutTime, bool crossFade)
        {
            // 前回のフェードアウト中インスタンスが残っていれば即座に打ち切る（Utage 版の GameObject.Destroy 相当）
            player.FadingOut?.StopImmediateAndRelease();
            player.FadingOut = null;
            player.PendingData = null;

            if (player.Current == null)
            {
                player.Current = CreateAndTrack(player, data, fadeInTime);
                return;
            }

            player.FadingOut = player.Current;
            player.Current = null;
            player.FadingOut.BeginFadeOutAndRelease(fadeOutTime);

            if (crossFade)
            {
                player.Current = CreateAndTrack(player, data, fadeInTime);
            }
            else
            {
                // フェードアウトの完了を待ってから鳴らす（Utage 版の Audio.Play(fadeInTime, fadeOutTime) 相当）
                player.PendingData = data;
                player.PendingFadeInTime = fadeInTime;
                player.PendingDelayRemaining = fadeOutTime;
            }
        }

        private FmodSoundInstance CreateAndTrack(LabelPlayer player, SoundData data, float fadeInTime)
        {
            // _instanceFactory は FMOD イベントが未登録の場合に null を返す（FmodSoundSystem.CreateInstance 参照）。
            // その場合はリストに加えず、「何も再生されていない」状態のまま呼び出し元へ返す
            FmodSoundInstance instance = _instanceFactory(player.Label, data, fadeInTime);
            if (instance != null)
            {
                player.AllInstances.Add(instance);
            }
            return instance;
        }

        private static bool IsSameFilePlaying(LabelPlayer player, SoundData data)
        {
            if (player.Current == null || !player.Current.IsPlaying)
            {
                return false;
            }
            // File 参照があればそれで、無ければ名前（AudioClip 直指定時）で同一性を判定する
            if (data.File != null || player.Current.File != null)
            {
                return player.Current.File == data.File;
            }
            return string.Equals(player.Current.Data.Name, data.Name, StringComparison.Ordinal);
        }

        private void StopPlayer(LabelPlayer player, float fadeTime)
        {
            player.PendingData = null;
            foreach (FmodSoundInstance instance in player.AllInstances)
            {
                instance.BeginFadeOutAndRelease(fadeTime);
            }
        }

        private static bool IsPlayerPlaying(LabelPlayer player)
        {
            foreach (FmodSoundInstance instance in player.AllInstances)
            {
                if (instance.IsPlaying)
                {
                    return true;
                }
            }
            return false;
        }

        private void StopOtherLabels(string exceptLabel, float fadeTime)
        {
            foreach (KeyValuePair<string, LabelPlayer> pair in _players)
            {
                if (!string.Equals(pair.Key, exceptLabel, StringComparison.Ordinal))
                {
                    StopPlayer(pair.Value, fadeTime);
                }
            }
        }

        private LabelPlayer GetOrCreatePlayer(string label)
        {
            if (!_players.TryGetValue(label, out LabelPlayer player))
            {
                player = new LabelPlayer { Label = label };
                _players.Add(label, player);
            }
            return player;
        }

        private static float UpdateFade(float from, float to, float fadeTime, ref float velocity)
        {
            if (fadeTime <= 0f)
            {
                velocity = 0f;
                return to;
            }
            if (Mathf.Abs(to - from) < 0.001f)
            {
                velocity = 0f;
                return to;
            }
            return Mathf.SmoothDamp(from, to, ref velocity, fadeTime);
        }

        private const int SAVE_VERSION = 0;

        /// <summary>セーブデータ用のバイナリ書き込み（ループ再生中のものだけ対象、Utage 既定実装と同じ方針）</summary>
        public void Write(BinaryWriter writer)
        {
            writer.Write(SAVE_VERSION);
            writer.Write(GroupVolume);

            // SoundData.EnableSave は internal で参照できないため、同じ条件（ファイルあり & ループ再生）を直接判定する
            List<KeyValuePair<string, SoundData>> savable = new List<KeyValuePair<string, SoundData>>();
            foreach (KeyValuePair<string, LabelPlayer> pair in _players)
            {
                SoundData data = pair.Value.Current?.Data;
                if (data != null && data.File != null && data.IsLoop)
                {
                    savable.Add(new KeyValuePair<string, SoundData>(pair.Key, data));
                }
            }

            writer.Write(savable.Count);
            foreach (KeyValuePair<string, SoundData> pair in savable)
            {
                writer.Write(pair.Key);
                writer.WriteBuffer(pair.Value.Write);
            }
        }

        /// <summary>セーブデータ用のバイナリ読み込み</summary>
        public void Read(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            if (version > SAVE_VERSION)
            {
                Debug.LogError($"[UtageFmod] FmodSoundGroup のセーブデータバージョンが不明です: {version}");
                return;
            }

            GroupVolume = reader.ReadSingle();
            CurrentGroupVolume = GroupVolume;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string label = reader.ReadString();
                SoundData data = new SoundData();
                reader.ReadBuffer(data.Read);
                Play(label, data, 0.1f, 0f);
            }
        }
    }
}
