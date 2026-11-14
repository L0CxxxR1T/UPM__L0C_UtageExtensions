#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// WebGLでブラウザの自動再生ポリシーにより止まっているFMODの音声出力を、最初のユーザー入力で開始させる
    /// WebGL以外では何もしない
    /// </summary>
    internal static class FmodWebAudioUnlock
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void UtageFmod_RegisterAudioUnlock();
#endif

        /// <summary>入力イベントの監視を登録する。複数回呼んでも登録は1回だけ</summary>
        public static void Register()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            UtageFmod_RegisterAudioUnlock();
#endif
        }
    }
}
