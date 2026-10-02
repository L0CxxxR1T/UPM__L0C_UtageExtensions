using System.Collections.Generic;
using UnityEngine;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// AvatarEx 用のログ出力ヘルパー
    /// OnPostRefresh は口パク中に約10Hzで発火するため、同じ警告を出し続けないよう
    /// キー単位で初回のみ出力する抑制機能を持つ
    /// </summary>
    internal static class AvatarExLog
    {
        const string Prefix = "[UtageAvatarEx] ";

        static readonly HashSet<string> WarnedKeys = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            WarnedKeys.Clear();
        }

        public static void Error(string message, Object context = null)
        {
            Debug.LogError(Prefix + message, context);
        }

        public static void Warning(string message, Object context = null)
        {
            Debug.LogWarning(Prefix + message, context);
        }

        /// <summary>
        /// key ごとに初回のみエラーログを出す
        /// </summary>
        public static void ErrorOnce(string key, string message, Object context = null)
        {
            if (!WarnedKeys.Add(key))
            {
                return;
            }
            Error(message, context);
        }

        /// <summary>
        /// key ごとに初回のみ警告ログを出す
        /// </summary>
        public static void WarningOnce(string key, string message, Object context = null)
        {
            if (!WarnedKeys.Add(key))
            {
                return;
            }
            Warning(message, context);
        }
    }
}
