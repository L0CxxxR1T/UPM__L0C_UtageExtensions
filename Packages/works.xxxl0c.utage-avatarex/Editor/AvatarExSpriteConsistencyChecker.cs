using System.Collections.Generic;
using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.AvatarEx.Editor
{
    /// <summary>
    /// AvatarExData のパーツスプライトについて、UVリマップの前提が崩れやすい構成を検出する。
    /// Spec_AvatarEx.md 5.3節: AvatarData.RebuildSize の同一サイズ検証は実装バグで機能していないため、
    /// この検証で肩代わりする
    /// </summary>
    internal static class AvatarExSpriteConsistencyChecker
    {
        public static List<string> Check(AvatarExData data)
        {
            var issues = new List<string>();
            Vector2? firstSize = null;
            string firstSpriteName = null;

            foreach (AvatarData.Category category in data.categories)
            {
                foreach (Sprite sprite in category.Sprites)
                {
                    if (sprite == null)
                    {
                        continue;
                    }

                    if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)
                    {
                        issues.Add($"スプライト \"{sprite.name}\" は回転パックされています。マスクのUVリマップがズレる可能性があります。");
                    }

                    if (sprite.packed && sprite.packingMode == SpritePackingMode.Tight)
                    {
                        issues.Add($"スプライト \"{sprite.name}\" はタイトパックされています。マスクに隣接スプライトの画素が混ざる可能性があります。");
                    }

                    Vector2 unitSize = sprite.rect.size / sprite.pixelsPerUnit;
                    if (firstSize == null)
                    {
                        firstSize = unitSize;
                        firstSpriteName = sprite.name;
                    }
                    else if (Vector2.Distance(firstSize.Value, unitSize) > 0.01f)
                    {
                        issues.Add(
                            $"スプライト \"{sprite.name}\" のサイズが \"{firstSpriteName}\" と異なります"
                            + $"（{unitSize} vs {firstSize.Value}）。マスクのUVリマップで吸収されますが、"
                            + "極端な相違があると見た目が崩れる場合があります。");
                    }
                }
            }
            return issues;
        }
    }
}
