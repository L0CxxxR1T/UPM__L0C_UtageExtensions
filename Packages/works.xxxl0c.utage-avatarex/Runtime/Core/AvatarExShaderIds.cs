using UnityEngine;
using UnityEngine.Rendering;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// AvatarEx-BlendableUI シェーダーのプロパティID・ブレンド値マッピングをまとめたもの
    /// </summary>
    internal static class AvatarExShaderIds
    {
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int SrcBlendRGB = Shader.PropertyToID("_SrcBlendRGB");
        public static readonly int DstBlendRGB = Shader.PropertyToID("_DstBlendRGB");
        public static readonly int SrcBlendA = Shader.PropertyToID("_SrcBlendA");
        public static readonly int DstBlendA = Shader.PropertyToID("_DstBlendA");
        public static readonly int ColorOp = Shader.PropertyToID("_ColorOp");
        public static readonly int MaskCount = Shader.PropertyToID("_MaskCount");
        public static readonly int MaskInvert = Shader.PropertyToID("_MaskInvert");

        public static readonly int[] MaskTex =
        {
            Shader.PropertyToID("_MaskTex0"),
            Shader.PropertyToID("_MaskTex1"),
            Shader.PropertyToID("_MaskTex2"),
        };

        public static readonly int[] MaskXform =
        {
            Shader.PropertyToID("_MaskXform0"),
            Shader.PropertyToID("_MaskXform1"),
            Shader.PropertyToID("_MaskXform2"),
        };

        public static readonly int[] MaskBounds =
        {
            Shader.PropertyToID("_MaskBounds0"),
            Shader.PropertyToID("_MaskBounds1"),
            Shader.PropertyToID("_MaskBounds2"),
        };

        public struct ColorBlendPreset
        {
            public float ColorOp;
            public float SrcRGB;
            public float DstRGB;
        }

        // BlendMode ごとの色前処理・RGBブレンド値マッピング（Spec_AvatarEx.md 5.1節の表に対応）
        public static ColorBlendPreset GetColorBlend(AvatarExBlendMode mode)
        {
            switch (mode)
            {
                case AvatarExBlendMode.Multiply:
                    return new ColorBlendPreset { ColorOp = 2f, SrcRGB = (float)BlendMode.DstColor, DstRGB = (float)BlendMode.Zero };
                case AvatarExBlendMode.Additive:
                    return new ColorBlendPreset { ColorOp = 1f, SrcRGB = (float)BlendMode.One, DstRGB = (float)BlendMode.One };
                case AvatarExBlendMode.Screen:
                    return new ColorBlendPreset { ColorOp = 1f, SrcRGB = (float)BlendMode.OneMinusDstColor, DstRGB = (float)BlendMode.One };
                case AvatarExBlendMode.Normal:
                default:
                    return new ColorBlendPreset { ColorOp = 0f, SrcRGB = (float)BlendMode.SrcAlpha, DstRGB = (float)BlendMode.OneMinusSrcAlpha };
            }
        }

        // Alpha側のブレンドはブレンドモードに依存せず、描画先（通常Canvas / RenderTexture空間内）だけで決まる
        // RenderTexture空間内は Utage/RenderTexture シェーダーと同一のアルファ式に合わせる
        public static void GetAlphaBlend(bool isRenderTextureSpace, out float srcA, out float dstA)
        {
            if (isRenderTextureSpace)
            {
                srcA = (float)BlendMode.Zero;
                dstA = (float)BlendMode.OneMinusSrcAlpha;
            }
            else
            {
                srcA = (float)BlendMode.One;
                dstA = (float)BlendMode.OneMinusSrcAlpha;
            }
        }
    }
}
