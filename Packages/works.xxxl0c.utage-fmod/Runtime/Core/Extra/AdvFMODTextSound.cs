using System.Collections.Generic;
using UnityEngine;
using Utage;
using UtageExtensions;

namespace XXXL0C.UtagePlugins.FMOD.Extra
{
    // 文字送り音を鳴らす
    // Utage標準のAdvTextSoundと同じ構造だが、Voiceグループで再生
    public sealed class AdvFMODTextSound : MonoBehaviour
    {
        [SerializeField] bool disable;
        public bool Disable { get => disable; set => disable = value; }

        public AdvEngine Engine => this.GetAdvEngineCacheFindIfMissing(ref engine);
        [SerializeField] AdvEngine engine;

        public enum Type { Time, CharacterCount }
        public Type type;

        public AudioClip defaultSound;

        [System.Serializable]
        public class SoundInfo
        {
            public string key;
            public AudioClip sound;
        }
        public List<SoundInfo> soundInfoList = new List<SoundInfo>();

        public int intervalCount = 3;
        public float intervalTime = 0.1f;
        float lastTime;
        int lastCharacterCount;

        void Awake()
        {
            Engine.Page.OnBeginPage.AddListener(OnBeginPage);
            Engine.Page.OnUpdateSendChar.AddListener(OnUpdateSendChar);
        }

        void OnBeginPage(AdvPage page)
        {
            lastTime = 0;
            lastCharacterCount = -intervalCount;
        }

        void OnUpdateSendChar(AdvPage page)
        {
            if (!CheckPlaySe(page))
            {
                return;
            }
            AudioClip sound = GetSe(page);
            if (sound == null)
            {
                return;
            }
            // Voiceグループで再生する。SoundPlayMode.Addを明示指定しないと
            // SoundManager.VoicePlayMode（既定Replay）が使われ、直前のボイスをフェードアウトしてしまう
            SoundData data = new SoundData(sound, SoundPlayMode.Add, 1f, false);
            SoundManager.GetInstance().PlayVoice(page.CharacterLabel, data, 0f, 0f);
            lastCharacterCount = page.CurrentTextLength;
            lastTime = page.Engine.Time.Time;
        }

        bool CheckPlaySe(AdvPage page)
        {
            if (Disable) return false;
            if (page.CurrentTextLength == lastCharacterCount) return false;

            switch (type)
            {
                case Type.Time:
                    if (page.Engine.Time.Time - lastTime > intervalTime)
                    {
                        return true;
                    }
                    break;
                case Type.CharacterCount:
                    if (page.CurrentTextLength >= lastCharacterCount + intervalCount)
                    {
                        return true;
                    }
                    break;
            }
            return false;
        }

        AudioClip GetSe(AdvPage page)
        {
            // キャラごとにSEを変える場合はキャラクターラベルを使う
            SoundInfo info = soundInfoList.Find(x => x.key == page.CharacterLabel);
            return (info != null) ? info.sound : defaultSound;
        }
    }
}
