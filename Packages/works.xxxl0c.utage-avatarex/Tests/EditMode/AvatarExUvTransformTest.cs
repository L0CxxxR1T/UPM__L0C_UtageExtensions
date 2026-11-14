using NUnit.Framework;
using UnityEngine;

namespace XXXL0C.UtagePlugins.AvatarEx.Tests.EditMode
{
    public sealed class AvatarExUvTransformTest
    {
        const float TOLERANCE = 1e-5f;

        static readonly Vector4 FULL = new Vector4(0f, 0f, 1f, 1f);

        [Test]
        public void 同じレイアウト同士なら恒等変換になる()
        {
            SpriteUvLayout layout = CreateLayout(FULL, FULL, Vector2.one);

            Vector4 xform = AvatarExUvTransform.CalcMaskTransform(layout, layout);

            AssertMapped(xform, new Vector2(0f, 0f), new Vector2(0f, 0f));
            AssertMapped(xform, new Vector2(1f, 1f), new Vector2(1f, 1f));
        }

        [Test]
        public void アトラス内の位置が違っても同じ見た目位置を指す()
        {
            SpriteUvLayout target = CreateLayout(FULL, new Vector4(0f, 0f, 0.5f, 0.5f), Vector2.one);
            SpriteUvLayout mask = CreateLayout(FULL, new Vector4(0.5f, 0.5f, 1f, 1f), Vector2.one);

            Vector4 xform = AvatarExUvTransform.CalcMaskTransform(target, mask);

            AssertMapped(xform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f));
            AssertMapped(xform, new Vector2(0.25f, 0.5f), new Vector2(0.75f, 1f));
        }

        [Test]
        public void マスクが小さいと中心そろえで対応する()
        {
            SpriteUvLayout target = CreateLayout(FULL, FULL, new Vector2(2f, 2f));
            SpriteUvLayout mask = CreateLayout(FULL, FULL, Vector2.one);

            Vector4 xform = AvatarExUvTransform.CalcMaskTransform(target, mask);

            AssertMapped(xform, new Vector2(0.25f, 0.25f), new Vector2(0f, 0f));
            AssertMapped(xform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            AssertMapped(xform, new Vector2(0.75f, 0.75f), new Vector2(1f, 1f));
        }

        [Test]
        public void 対象がトリムされていても描画範囲に合わせて対応する()
        {
            Vector4 trimmed = new Vector4(0.25f, 0.25f, 0.75f, 0.75f);
            SpriteUvLayout target = CreateLayout(trimmed, FULL, Vector2.one);
            SpriteUvLayout mask = CreateLayout(FULL, FULL, Vector2.one);

            Vector4 xform = AvatarExUvTransform.CalcMaskTransform(target, mask);

            AssertMapped(xform, new Vector2(0f, 0f), new Vector2(0.25f, 0.25f));
            AssertMapped(xform, new Vector2(1f, 1f), new Vector2(0.75f, 0.75f));
        }

        [Test]
        public void マスクがトリムされていても描画範囲に合わせて対応する()
        {
            Vector4 trimmed = new Vector4(0.25f, 0f, 0.75f, 1f);
            SpriteUvLayout target = CreateLayout(FULL, FULL, Vector2.one);
            SpriteUvLayout mask = CreateLayout(trimmed, new Vector4(0.5f, 0f, 1f, 1f), Vector2.one);

            Vector4 xform = AvatarExUvTransform.CalcMaskTransform(target, mask);

            AssertMapped(xform, new Vector2(0.25f, 0f), new Vector2(0.5f, 0f));
            AssertMapped(xform, new Vector2(0.75f, 1f), new Vector2(1f, 1f));
        }

        [Test]
        public void 部分矩形のスプライトから描画範囲とUVを取得できる()
        {
            Texture2D texture = new Texture2D(256, 128);
            Sprite sprite = Sprite.Create(texture, new Rect(64f, 0f, 64f, 128f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            try
            {
                bool ok = AvatarExUvTransform.TryGetLayout(sprite, out SpriteUvLayout layout);

                Assert.That(ok, Is.True);
                AssertVector(FULL, layout.DrawRect);
                AssertVector(new Vector4(0.25f, 0f, 0.5f, 1f), layout.OuterUv);
                Assert.That(layout.UnitSize.x, Is.EqualTo(0.64f).Within(TOLERANCE));
                Assert.That(layout.UnitSize.y, Is.EqualTo(1.28f).Within(TOLERANCE));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        static SpriteUvLayout CreateLayout(Vector4 drawRect, Vector4 outerUv, Vector2 unitSize)
        {
            return new SpriteUvLayout(drawRect, outerUv, unitSize);
        }

        static void AssertMapped(Vector4 xform, Vector2 mainUv, Vector2 expected)
        {
            Vector2 actual = new Vector2(mainUv.x * xform.x + xform.z, mainUv.y * xform.y + xform.w);
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(TOLERANCE), $"u: {mainUv}");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(TOLERANCE), $"v: {mainUv}");
        }

        static void AssertVector(Vector4 expected, Vector4 actual)
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(TOLERANCE), $"[{i}]");
            }
        }
    }
}
