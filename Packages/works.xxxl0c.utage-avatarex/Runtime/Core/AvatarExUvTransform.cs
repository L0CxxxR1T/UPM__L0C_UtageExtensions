using UnityEngine;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// マスク用UVリマップの計算。
    /// AvatarData.RebuildSize の「全パーツ同一サイズ」検証は実装バグで機能していない
    /// （Spec_AvatarEx.md 5.3節）ため、パーツごとのアトラス内位置・実サイズの違いを
    /// シェーダー側のUVで吸収できるようにする
    /// </summary>
    internal static class AvatarExUvTransform
    {
        /// <summary>
        /// スプライトのアトラス内位置を正規化して返す（offset.xy, scale.zw）
        /// </summary>
        public static Vector4 CalcUvRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }
            Rect textureRect = sprite.textureRect;
            float texWidth = sprite.texture.width;
            float texHeight = sprite.texture.height;
            if (texWidth <= 0f || texHeight <= 0f)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }
            return new Vector4(
                textureRect.x / texWidth,
                textureRect.y / texHeight,
                textureRect.width / texWidth,
                textureRect.height / texHeight);
        }

        /// <summary>
        /// マスク対象パーツ(target)に対するマスクソース(mask)のUV変換を計算する。
        /// 両パーツは中心そろえで重なる前提（AvatarImage が全パーツを同一アンカー・pivot(0.5,0.5)・
        /// localPosition=zero で生成するため）で、実サイズの比率から中心そろえのアフィン変換を求める
        /// </summary>
        public static bool TryCalcMaskTransform(Sprite target, Sprite mask, out Vector4 xform, out Vector4 bounds)
        {
            xform = new Vector4(1f, 1f, 0f, 0f);
            bounds = new Vector4(0f, 0f, 1f, 1f);

            if (target == null || mask == null || target.texture == null || mask.texture == null)
            {
                return false;
            }

            Vector4 maskUvRect = CalcUvRect(mask);
            Vector2 maskOffset = new Vector2(maskUvRect.x, maskUvRect.y);
            Vector2 maskScale = new Vector2(maskUvRect.z, maskUvRect.w);

            Vector2 targetUnit = target.rect.size / target.pixelsPerUnit;
            Vector2 maskUnit = mask.rect.size / mask.pixelsPerUnit;
            if (targetUnit.x <= 0f || targetUnit.y <= 0f || maskUnit.x <= 0f || maskUnit.y <= 0f)
            {
                return false;
            }

            Vector2 ratio = new Vector2(maskUnit.x / targetUnit.x, maskUnit.y / targetUnit.y);
            Vector2 center = (Vector2.one - ratio) * 0.5f;

            Vector2 a = new Vector2(maskScale.x / ratio.x, maskScale.y / ratio.y);
            Vector2 b = maskOffset - Vector2.Scale(center, a);

            xform = new Vector4(a.x, a.y, b.x, b.y);
            bounds = new Vector4(maskOffset.x, maskOffset.y, maskOffset.x + maskScale.x, maskOffset.y + maskScale.y);
            return true;
        }
    }
}
