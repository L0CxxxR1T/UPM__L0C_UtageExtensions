using System.Collections.Generic;
using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// AvatarExData 単位でタグ逆引き辞書・ログ抑制フラグをキャッシュする
    /// OnPostRefresh は口パく中に約10Hzで発火するため、Sprite→Tag の逆引きは
    /// 毎回線形走査せずここで構築した辞書を引くだけにする
    /// </summary>
    internal sealed class AvatarExDataCache
    {
        static readonly Dictionary<AvatarExData, AvatarExDataCache> Instances = new Dictionary<AvatarExData, AvatarExDataCache>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instances.Clear();
        }

        public static AvatarExDataCache Get(AvatarExData data)
        {
            if (data == null)
            {
                return null;
            }
            if (!Instances.TryGetValue(data, out AvatarExDataCache cache))
            {
                cache = new AvatarExDataCache(data);
                Instances[data] = cache;
            }
            return cache;
        }

        readonly Dictionary<Sprite, string> spriteToTag = new Dictionary<Sprite, string>();
        readonly HashSet<string> loggedKeys = new HashSet<string>();

        AvatarExDataCache(AvatarExData data)
        {
            Build(data);
        }

        void Build(AvatarExData data)
        {
            foreach (AvatarData.Category category in data.categories)
            {
                foreach (Sprite sprite in category.Sprites)
                {
                    if (sprite == null)
                    {
                        continue;
                    }
                    if (spriteToTag.ContainsKey(sprite))
                    {
                        // 同一スプライトが複数カテゴリに属している構成（通常は起きない）。先勝ちにする
                        WarningOnce($"dup-sprite:{sprite.GetInstanceID()}",
                            $"スプライト \"{sprite.name}\" が複数のカテゴリに属しています。先に見つかったタグを優先します。", data);
                        continue;
                    }
                    spriteToTag[sprite] = category.Tag;
                }
            }
        }

        /// <summary>
        /// Sprite の参照同一性でタグを引く（Image.sprite.name 等の文字列一致は使わない）
        /// </summary>
        public string FindTag(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }
            spriteToTag.TryGetValue(sprite, out string tag);
            return tag;
        }

        /// <summary>
        /// key ごとに初回のみ警告ログを出す（このキャッシュ＝この AvatarExData の寿命の間だけ抑制）
        /// </summary>
        public void WarningOnce(string key, string message, Object context = null)
        {
            if (!loggedKeys.Add(key))
            {
                return;
            }
            AvatarExLog.Warning(message, context);
        }

        /// <summary>
        /// key ごとに初回のみエラーログを出す
        /// </summary>
        public void ErrorOnce(string key, string message, Object context = null)
        {
            if (!loggedKeys.Add(key))
            {
                return;
            }
            AvatarExLog.Error(message, context);
        }
    }
}
