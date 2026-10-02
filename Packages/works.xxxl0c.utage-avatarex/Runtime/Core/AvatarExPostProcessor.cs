using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utage;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// AvatarImage.OnPostRefresh を購読し、パーツ単位のブレンドモード・クリッピングマスクを
    /// 後処理で適用する。AvatarImage 自体は無改造のまま使う。
    /// OnPostRefresh は口パク中に約10Hzで発火するため、この中では new Material・線形走査・
    /// イテレータ確保を避け、キャッシュ済みの情報だけを使うこと
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarExPostProcessor : MonoBehaviour
    {
        // マスクを使うパーツ 1件分のマテリアルと、前回適用した状態（変更検知用）
        sealed class MaskedMaterialEntry
        {
            public Material Material;
            public Sprite LastTargetSprite;
            public readonly Sprite[] LastMaskSprites = new Sprite[AvatarExData.MaxClipMaskSources];
            public int LastMaskCount = -1;
            public bool LastInvert;
            public AvatarExBlendMode LastBlendMode;
            public bool Initialized;
        }

        AvatarImage avatar;
        bool isRenderTextureSpace;
        Transform childRoot;

        readonly List<Image> childImages = new List<Image>();
        readonly Dictionary<string, int> tagSlotCounter = new Dictionary<string, int>();
        readonly Dictionary<(string tag, int slot), MaskedMaterialEntry> maskedMaterials = new Dictionary<(string, int), MaskedMaterialEntry>();
        readonly Sprite[] maskSpriteBuffer = new Sprite[AvatarExData.MaxClipMaskSources];

        /// <summary>
        /// AdvGraphicObjectAvatarEx.AddGraphicComponentOnInit から呼ばれる
        /// </summary>
        public void Initialize(AvatarImage avatar, bool isRenderTextureSpace)
        {
            if (this.avatar != null)
            {
                this.avatar.OnPostRefresh.RemoveListener(OnPostRefresh);
            }
            this.avatar = avatar;
            this.isRenderTextureSpace = isRenderTextureSpace;
            if (this.avatar != null)
            {
                this.avatar.OnPostRefresh.AddListener(OnPostRefresh);
            }
        }

        void OnDestroy()
        {
            if (avatar != null)
            {
                avatar.OnPostRefresh.RemoveListener(OnPostRefresh);
            }
            foreach (MaskedMaterialEntry entry in maskedMaterials.Values)
            {
                if (entry.Material != null)
                {
                    Destroy(entry.Material);
                }
            }
            maskedMaterials.Clear();
        }

        void OnPostRefresh()
        {
            AvatarExData avatarExData = avatar.AvatarData as AvatarExData;
            if (avatarExData == null)
            {
                // 素の AvatarData（FileType=Avatar）が刺さっている場合は何もしない
                return;
            }

            AvatarExDataCache cache = AvatarExDataCache.Get(avatarExData);

            if (childRoot == null)
            {
                childRoot = transform.Find("childRoot");
            }
            if (childRoot == null)
            {
                return;
            }

            childImages.Clear();
            childRoot.GetComponentsInChildren(false, childImages);

            tagSlotCounter.Clear();
            foreach (Image image in childImages)
            {
                ApplyPart(avatarExData, cache, image);
            }
        }

        void ApplyPart(AvatarExData avatarExData, AvatarExDataCache cache, Image image)
        {
            string tag = cache.FindTag(image.sprite);
            if (tag == null)
            {
                // タグ不明パーツ（AvatarExData に紐づかないスプライト）は Utage 既定のまま
                return;
            }

            AvatarExData.PartExtraSetting setting = avatarExData.FindSetting(tag);
            if (setting == null)
            {
                return;
            }

            bool requestedMask = setting.ClipMaskSourceTags.Count > 0;
            AvatarExBlendMode blendMode = setting.BlendMode;

            if (blendMode == AvatarExBlendMode.Normal && !requestedMask)
            {
                // Utage の既定マテリアル（null や RenderTexture 用）をそのまま保持する
                image.enabled = true;
                return;
            }

            // 同一タグで複数 Image が同時に存在するケース（マスク対象に OptionTag を許容しているため）に
            // 備え、タグ内での出現順をスロット番号にしてマテリアルを個別管理する
            int slotIndex = tagSlotCounter.TryGetValue(tag, out int count) ? count : 0;
            tagSlotCounter[tag] = slotIndex + 1;

            if (!requestedMask)
            {
                image.enabled = true;
                Material sharedMaterial = AvatarExMaterialCache.GetShared(blendMode, isRenderTextureSpace);
                if (sharedMaterial != null)
                {
                    image.material = sharedMaterial;
                }
                return;
            }

            bool maskValid = AvatarExValidation.ValidateSetting(avatarExData, setting, out AvatarExValidation.Result errorResult, out string errorTag);
            if (!maskValid)
            {
                cache.ErrorOnce(
                    $"mask-invalid:{tag}",
                    $"タグ \"{tag}\" のマスク設定が無効です: {AvatarExValidation.ToMessage(errorResult, errorTag)} マスクなしで表示します。",
                    avatarExData);

                image.enabled = true;
                Material fallbackMaterial = AvatarExMaterialCache.GetShared(blendMode, isRenderTextureSpace);
                if (fallbackMaterial != null)
                {
                    image.material = fallbackMaterial;
                }
                return;
            }

            // マスクソースの現在表示中スプライトを集める（None のソースは除外＝alpha=0扱いと等価）
            int validCount = 0;
            for (int i = 0; i < setting.ClipMaskSourceTags.Count && validCount < AvatarExData.MaxClipMaskSources; i++)
            {
                if (TryGetCurrentSprite(setting.ClipMaskSourceTags[i], out Sprite sourceSprite) && sourceSprite != null)
                {
                    maskSpriteBuffer[validCount] = sourceSprite;
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                if (!setting.InvertMask)
                {
                    // 反転していない＝「マスクソースが何も無ければ隠す」が自然
                    image.enabled = false;
                    return;
                }

                // 反転時は 1-0=1 で全面表示になるべきなので、マスクなし扱いで通常表示する
                image.enabled = true;
                Material invertedFallback = AvatarExMaterialCache.GetShared(blendMode, isRenderTextureSpace);
                if (invertedFallback != null)
                {
                    image.material = invertedFallback;
                }
                return;
            }

            image.enabled = true;
            Material material = GetOrCreateMaskedMaterial(tag, slotIndex, blendMode, image.sprite, validCount, setting.InvertMask);
            if (material != null)
            {
                image.material = material;
            }
        }

        Material GetOrCreateMaskedMaterial(string tag, int slotIndex, AvatarExBlendMode blendMode, Sprite targetSprite, int maskCount, bool invert)
        {
            (string tag, int slotIndex) key = (tag, slotIndex);
            if (!maskedMaterials.TryGetValue(key, out MaskedMaterialEntry entry))
            {
                entry = new MaskedMaterialEntry { Material = AvatarExMaterialCache.CreateMasked(blendMode, isRenderTextureSpace) };
                maskedMaterials[key] = entry;
            }
            if (entry.Material == null)
            {
                return null;
            }

            bool changed = !entry.Initialized
                || entry.LastTargetSprite != targetSprite
                || entry.LastMaskCount != maskCount
                || entry.LastInvert != invert
                || entry.LastBlendMode != blendMode;

            if (!changed)
            {
                for (int i = 0; i < maskCount; i++)
                {
                    if (entry.LastMaskSprites[i] != maskSpriteBuffer[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
            {
                // 前回と完全一致。SetXxx を一切呼ばずスキップ（口パク定常状態での負荷対策）
                return entry.Material;
            }

            if (entry.LastBlendMode != blendMode || !entry.Initialized)
            {
                AvatarExMaterialCache.ApplyBlendMode(entry.Material, blendMode, isRenderTextureSpace);
            }

            Vector4 mainUvRect = AvatarExUvTransform.CalcUvRect(targetSprite);
            entry.Material.SetVector(AvatarExShaderIds.MainUvRect, mainUvRect);

            for (int i = 0; i < AvatarExData.MaxClipMaskSources; i++)
            {
                if (i < maskCount)
                {
                    Sprite maskSprite = maskSpriteBuffer[i];
                    entry.Material.SetTexture(AvatarExShaderIds.MaskTex[i], maskSprite.texture);
                    AvatarExUvTransform.TryCalcMaskTransform(targetSprite, maskSprite, out Vector4 xform, out Vector4 bounds);
                    entry.Material.SetVector(AvatarExShaderIds.MaskXform[i], xform);
                    entry.Material.SetVector(AvatarExShaderIds.MaskBounds[i], bounds);
                }
                entry.LastMaskSprites[i] = i < maskCount ? maskSpriteBuffer[i] : null;
            }

            entry.Material.SetFloat(AvatarExShaderIds.MaskCount, maskCount);
            entry.Material.SetFloat(AvatarExShaderIds.MaskInvert, invert ? 1f : 0f);

            entry.LastTargetSprite = targetSprite;
            entry.LastMaskCount = maskCount;
            entry.LastInvert = invert;
            entry.LastBlendMode = blendMode;
            entry.Initialized = true;

            return entry.Material;
        }

        /// <summary>
        /// 指定タグ（通常タグのみ、OptionTag不可。呼び出し前に検証済みであること）の
        /// 現在表示中スプライトを取得する。同一タグが複数Categoryに紐づく場合は先頭1件のみ対象とする。
        /// タグ自体が見つからなければfalse、見つかったが現在Noneパターンでspriteがない場合は
        /// trueかつsprite=nullを返す
        /// </summary>
        bool TryGetCurrentSprite(string tag, out Sprite sprite)
        {
            sprite = null;
            AvatarData data = avatar.AvatarData;
            AvatarPattern pattern = avatar.AvatarPattern;
            if (data == null)
            {
                return false;
            }

            foreach (AvatarData.Category category in data.categories)
            {
                if (category.Tag != tag)
                {
                    continue;
                }
                foreach (AvatarPattern.PartternData partternData in pattern.DataList)
                {
                    if (partternData.Tag != tag)
                    {
                        continue;
                    }
                    sprite = category.GetSprite(partternData.PatternName);
                    return true;
                }
                return false;
            }
            return false;
        }
    }
}
