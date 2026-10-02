using System.Collections.Generic;
using Utage;

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// マスクソースタグ（ClipMaskSourceTags）の検証ロジック
    /// Runtime/Editor 双方から呼ぶため #if UNITY_EDITOR は含めない
    /// </summary>
    public static class AvatarExValidation
    {
        public enum Result
        {
            Ok,
            TagNotFound,
            IsOptionTag,
            MultiLevelReference,
        }

        /// <summary>
        /// 1つのマスクソースタグを検証する。同一タグが複数Categoryに紐づく場合は
        /// categories の並び順で最初に見つかったものを見る（2章の方針と一致させる）
        /// </summary>
        public static Result ValidateSourceTag(AvatarExData data, string sourceTag)
        {
            AvatarData.Category found = null;
            foreach (AvatarData.Category category in data.categories)
            {
                if (category.Tag == sourceTag)
                {
                    found = category;
                    break;
                }
            }
            if (found == null)
            {
                return Result.TagNotFound;
            }
            if (found.Tag == data.OptionTag)
            {
                return Result.IsOptionTag;
            }

            AvatarExData.PartExtraSetting sourceSetting = data.FindSetting(sourceTag);
            if (sourceSetting != null && sourceSetting.ClipMaskSourceTags.Count > 0)
            {
                return Result.MultiLevelReference;
            }
            return Result.Ok;
        }

        /// <summary>
        /// 設定内の全マスクソースタグを検証する。1つでもNGならfalseを返し、最初のエラーを out で返す
        /// （3.1節の方針: 部分適用はせず設定全体を無効にする）
        /// </summary>
        public static bool ValidateSetting(AvatarExData data, AvatarExData.PartExtraSetting setting, out Result firstError, out string errorTag)
        {
            firstError = Result.Ok;
            errorTag = null;
            if (setting == null)
            {
                return true;
            }
            foreach (string tag in setting.ClipMaskSourceTags)
            {
                Result result = ValidateSourceTag(data, tag);
                if (result != Result.Ok)
                {
                    firstError = result;
                    errorTag = tag;
                    return false;
                }
            }
            return true;
        }

        public static string ToMessage(Result result, string tag)
        {
            switch (result)
            {
                case Result.TagNotFound:
                    return $"ClipMaskSourceTag \"{tag}\" が見つかりません。";
                case Result.IsOptionTag:
                    return $"ClipMaskSourceTag \"{tag}\" はOptionTag（アクセサリ等）のためマスクソースに指定できません。";
                case Result.MultiLevelReference:
                    return $"ClipMaskSourceTag \"{tag}\" は自身もマスクを使用しているため多段参照になります。";
                default:
                    return null;
            }
        }

        /// <summary>
        /// マスクソースとして選択可能なタグを列挙する（Editorのドロップダウン用）
        /// </summary>
        public static IEnumerable<string> EnumerateSelectableSourceTags(AvatarExData data)
        {
            var seen = new HashSet<string>();
            foreach (AvatarData.Category category in data.categories)
            {
                if (category.Tag == data.OptionTag)
                {
                    continue;
                }
                if (!seen.Add(category.Tag))
                {
                    continue;
                }
                AvatarExData.PartExtraSetting setting = data.FindSetting(category.Tag);
                if (setting != null && setting.ClipMaskSourceTags.Count > 0)
                {
                    continue;
                }
                yield return category.Tag;
            }
        }
    }
}
