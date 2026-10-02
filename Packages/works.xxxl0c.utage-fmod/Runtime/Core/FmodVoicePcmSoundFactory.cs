using FMOD;
using FMOD.Studio;
using FMODUnity;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.FMOD
{
    /// <summary>
    /// ボイス再生用ProgrammerInstrumentに、Utageがロード済みのAudioClipから抽出したPCMデータを流し込むファクトリ
    /// LoadType（Local / Server / StreamingAssets）に依存せず、AssetFileManagerが既にロードしたAudioClipをそのまま音源として使う
    /// </summary>
    internal static class FmodVoicePcmSoundFactory
    {
        /// <summary>
        /// FMODのEventInstanceにPCMユーザーデータを紐づけて生成
        /// </summary>
        public static bool TryCreateVoiceInstance(EventReference voiceEvent, SoundData data, out EventInstance instance)
        {
            instance = default;

            if (voiceEvent.IsNull)
            {
                UnityEngine.Debug.LogError("[UtageFmod] ボイス再生用のFMODイベント（FmodSoundSettings.VoiceEvent）が設定されていません。");
                return false;
            }

            AudioClip clip = data.Clip;
            if (clip == null)
            {
                UnityEngine.Debug.LogError($"[UtageFmod] ボイス「{data.Name}」のAudioClipが取得できません。\nUtage側のロードが完了しているか確認してください。");
                return false;
            }

            if (!TryCreateSoundFromClip(clip, out Sound sound))
            {
                return false;
            }

            try
            {
                instance = RuntimeManager.CreateInstance(voiceEvent);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[UtageFmod] ボイス再生用イベントの生成に失敗しました\n詳細: {e.Message}");
                sound.release();
                return false;
            }

            VoiceUserData userData = new VoiceUserData(sound);
            GCHandle handle = GCHandle.Alloc(userData);
            instance.setUserData(GCHandle.ToIntPtr(handle));
            instance.setCallback(VoiceEventCallback, EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND | EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND);
            return true;
        }

        private static bool TryCreateSoundFromClip(AudioClip clip, out Sound sound)
        {
            sound = default;

            int sampleCount = clip.samples * clip.channels;
            float[] samples = new float[sampleCount];
            if (!clip.GetData(samples, 0))
            {
                UnityEngine.Debug.LogError($"[UtageFmod] AudioClip「{clip.name}」からPCMデータを取得できませんでした。");
                return false;
            }

            byte[] pcmBytes = new byte[sampleCount * sizeof(float)];
            Buffer.BlockCopy(samples, 0, pcmBytes, 0, pcmBytes.Length);

            CREATESOUNDEXINFO exinfo = new CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                length = (uint)pcmBytes.Length,
                numchannels = clip.channels,
                defaultfrequency = clip.frequency,
                format = SOUND_FORMAT.PCMFLOAT,
            };

            RESULT result = RuntimeManager.CoreSystem.createSound(
                pcmBytes,
                MODE.OPENMEMORY | MODE.OPENRAW | MODE.CREATESAMPLE | MODE.LOOP_OFF,
                ref exinfo,
                out sound);

            if (result != RESULT.OK)
            {
                UnityEngine.Debug.LogError($"[UtageFmod] AudioClip「{clip.name}」のFMODSound生成に失敗しました。\n{result}");
                return false;
            }
            return true;
        }

        [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
        private static RESULT VoiceEventCallback(EVENT_CALLBACK_TYPE type, IntPtr eventInstancePtr, IntPtr parameterPtr)
        {
            EventInstance instance = new EventInstance(eventInstancePtr);
            RESULT userDataResult = instance.getUserData(out IntPtr userDataPtr);
            if (userDataResult != RESULT.OK || userDataPtr == IntPtr.Zero)
            {
                return RESULT.OK;
            }

            GCHandle handle = GCHandle.FromIntPtr(userDataPtr);

            if (type == EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND)
            {
                if (handle.Target is VoiceUserData userData)
                {
                    PROGRAMMER_SOUND_PROPERTIES properties = (PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(PROGRAMMER_SOUND_PROPERTIES));
                    properties.sound = userData.Sound.handle;
                    properties.subsoundIndex = -1;
                    Marshal.StructureToPtr(properties, parameterPtr, false);
                }
            }
            else if (type == EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND)
            {
                PROGRAMMER_SOUND_PROPERTIES properties = (PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(PROGRAMMER_SOUND_PROPERTIES));
                Sound sound = default;
                sound.handle = properties.sound;
                sound.release();
                handle.Free();
            }

            return RESULT.OK;
        }

        private sealed class VoiceUserData
        {
            public readonly Sound Sound;

            public VoiceUserData(Sound sound)
            {
                Sound = sound;
            }
        }
    }
}
