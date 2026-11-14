using UnityEngine;
using UnityEngine.Sprites;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// Image(Simple) がスプライトを描くときの描画範囲と、そこに貼られるUV範囲
    /// </summary>
    internal readonly struct SpriteUvLayout
    {
        /// <summary>Image の矩形を0〜1に正規化した座標での描画範囲（min.xy, max.zw）</summary>
        public readonly Vector4 DrawRect;

        /// <summary>描画範囲に貼られるテクスチャUV（min.xy, max.zw）</summary>
        public readonly Vector4 OuterUv;

        /// <summary>SetNativeSize 後の矩形サイズに比例する値（rect.size / pixelsPerUnit）</summary>
        public readonly Vector2 UnitSize;

        public SpriteUvLayout(Vector4 drawRect, Vector4 outerUv, Vector2 unitSize)
        {
            DrawRect = drawRect;
            OuterUv = outerUv;
            UnitSize = unitSize;
        }
    }

    /// <summary>
    /// マスク用UVリマップの計算。
    /// AvatarData.RebuildSize の「全パーツ同一サイズ」検証は実装バグで機能していない
    /// （Spec_AvatarEx.md 5.3節）ため、パーツごとのアトラス内位置・実サイズ・トリム量の違いを
    /// シェーダー側のUVで吸収できるようにする
    /// </summary>
    internal static class AvatarExUvTransform
    {
        /// <summary>
        /// スプライトの描画レイアウトを取得する。描画される領域が無いスプライトは false を返す
        /// </summary>
        public static bool TryGetLayout(Sprite sprite, out SpriteUvLayout layout)
        {
            layout = default;
            if (sprite == null || sprite.texture == null || sprite.pixelsPerUnit <= 0f)
            {
                return false;
            }

            Vector2 size = sprite.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return false;
            }

            // Image(Simple) と同じ関数で求める。トリムやアトラス詰めで textureRect が rect より
            // 小さいと、Image は padding 分だけ内側に描き、そこへ outerUV を貼る
            Vector4 padding = DataUtility.GetPadding(sprite);
            Vector4 drawRect = new Vector4(
                padding.x / size.x,
                padding.y / size.y,
                1f - padding.z / size.x,
                1f - padding.w / size.y);
            Vector4 outerUv = DataUtility.GetOuterUV(sprite);

            if (drawRect.z <= drawRect.x || drawRect.w <= drawRect.y
                || outerUv.z <= outerUv.x || outerUv.w <= outerUv.y)
            {
                return false;
            }

            layout = new SpriteUvLayout(drawRect, outerUv, size / sprite.pixelsPerUnit);
            return true;
        }

        /// <summary>
        /// マスク対象(target)のUVをマスクソース(mask)のUVへ写すアフィン変換（scale.xy, offset.zw）を返す。
        /// AvatarImage は全パーツを同一アンカー・pivot(0.5,0.5)・localPosition=zero で生成するため、
        /// 両者の Image 矩形は中心そろえで重なる
        /// </summary>
        public static Vector4 CalcMaskTransform(in SpriteUvLayout target, in SpriteUvLayout mask)
        {
            // target のUV → target 矩形の正規化座標
            Vector2 toTargetRectScale = Div(Size(target.DrawRect), Size(target.OuterUv));
            Vector2 toTargetRectOffset = Min(target.DrawRect) - Vector2.Scale(Min(target.OuterUv), toTargetRectScale);

            // target 矩形 → mask 矩形（中心そろえ）
            Vector2 toMaskRectScale = Div(target.UnitSize, mask.UnitSize);
            Vector2 toMaskRectOffset = (Vector2.one - toMaskRectScale) * 0.5f;

            // mask 矩形 → mask のUV
            Vector2 toMaskUvScale = Div(Size(mask.OuterUv), Size(mask.DrawRect));
            Vector2 toMaskUvOffset = Min(mask.OuterUv) - Vector2.Scale(Min(mask.DrawRect), toMaskUvScale);

            Vector2 scale = Vector2.Scale(Vector2.Scale(toTargetRectScale, toMaskRectScale), toMaskUvScale);
            Vector2 offset = Vector2.Scale(Vector2.Scale(toTargetRectOffset, toMaskRectScale) + toMaskRectOffset, toMaskUvScale)
                + toMaskUvOffset;
            return new Vector4(scale.x, scale.y, offset.x, offset.y);
        }

        static Vector2 Min(Vector4 rect)
        {
            return new Vector2(rect.x, rect.y);
        }

        static Vector2 Size(Vector4 rect)
        {
            return new Vector2(rect.z - rect.x, rect.w - rect.y);
        }

        static Vector2 Div(Vector2 a, Vector2 b)
        {
            return new Vector2(a.x / b.x, a.y / b.y);
        }
    }
}
