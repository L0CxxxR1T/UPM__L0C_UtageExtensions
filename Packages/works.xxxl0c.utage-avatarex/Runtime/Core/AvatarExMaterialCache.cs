using UnityEngine;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// AvatarEx-BlendableUI マテリアルの生成・キャッシュ
    /// マスクを使わないパーツは BlendMode×描画先(通常Canvas/RenderTexture空間内) ごとの共有マテリアルを使い回し、
    /// マスクを使うパーツのみ個別インスタンスを生成する。OnPostRefresh（口パク中は約10Hz）から
    /// 呼ばれる前提のため、キャッシュ済みなら new Material しない
    /// </summary>
    internal static class AvatarExMaterialCache
    {
        const string ShaderName = "UtageExtension/AvatarEx/BlendableUI";
        const int BlendModeCount = 4; // AvatarExBlendMode の要素数

        static Shader cachedShader;
        static bool shaderLoadFailedLogged;
        static Material[,] shared; // [blendMode, isRenderTextureSpace(0/1)]

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cachedShader = null;
            shaderLoadFailedLogged = false;
            shared = null;
        }

        static Shader GetShader()
        {
            if (cachedShader != null)
            {
                return cachedShader;
            }
            cachedShader = Shader.Find(ShaderName);
            if (cachedShader == null && !shaderLoadFailedLogged)
            {
                shaderLoadFailedLogged = true;
                AvatarExLog.Error($"シェーダー '{ShaderName}' が見つかりません。Resources 配下に配置されているか確認してください。");
            }
            return cachedShader;
        }

        /// <summary>
        /// マスクを使わないパーツ用の共有マテリアルを取得する（存在しなければ生成してキャッシュ）
        /// </summary>
        public static Material GetShared(AvatarExBlendMode mode, bool isRenderTextureSpace)
        {
            Shader shader = GetShader();
            if (shader == null)
            {
                return null;
            }

            if (shared == null)
            {
                shared = new Material[BlendModeCount, 2];
            }

            int modeIndex = (int)mode;
            int rtIndex = isRenderTextureSpace ? 1 : 0;
            Material material = shared[modeIndex, rtIndex];
            if (material == null)
            {
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                ApplyBlendMode(material, mode, isRenderTextureSpace);
                material.SetFloat(AvatarExShaderIds.MaskCount, 0f);
                shared[modeIndex, rtIndex] = material;
            }
            return material;
        }

        /// <summary>
        /// マスクを使うパーツ用のマテリアルインスタンスを新規生成する（呼び出し側でキャッシュして使い回すこと）
        /// </summary>
        public static Material CreateMasked(AvatarExBlendMode mode, bool isRenderTextureSpace)
        {
            Shader shader = GetShader();
            if (shader == null)
            {
                return null;
            }
            Material material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            ApplyBlendMode(material, mode, isRenderTextureSpace);
            return material;
        }

        public static void ApplyBlendMode(Material material, AvatarExBlendMode mode, bool isRenderTextureSpace)
        {
            AvatarExShaderIds.ColorBlendPreset preset = AvatarExShaderIds.GetColorBlend(mode);
            material.SetFloat(AvatarExShaderIds.ColorOp, preset.ColorOp);
            material.SetFloat(AvatarExShaderIds.SrcBlendRGB, preset.SrcRGB);
            material.SetFloat(AvatarExShaderIds.DstBlendRGB, preset.DstRGB);

            AvatarExShaderIds.GetAlphaBlend(isRenderTextureSpace, out float srcA, out float dstA);
            material.SetFloat(AvatarExShaderIds.SrcBlendA, srcA);
            material.SetFloat(AvatarExShaderIds.DstBlendA, dstA);
        }
    }
}
