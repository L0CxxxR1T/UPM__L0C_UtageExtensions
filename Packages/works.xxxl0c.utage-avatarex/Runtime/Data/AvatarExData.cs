using System;
using System.Collections.Generic;
using UnityEngine;
using Utage;

#if UNITY_EDITOR
using System.Reflection;
using System.Runtime.ExceptionServices;
#endif

namespace XXXL0C.UtagePlugins.AvatarEx
{
    /// <summary>
    /// AvatarData を拡張し、パーツ（Category＝タグ）単位のブレンドモード・クリッピングマスク設定を追加する
    /// FileType=AvatarEx として Character シートから参照される
    /// </summary>
    [CreateAssetMenu(menuName = "UtageExtension/AvatarExData")]
    public class AvatarExData : AvatarData
    {
        /// <summary>
        /// マスクソースとして指定できるタグ数の上限（シェーダー側のマスクスロット数と一致させる）
        /// </summary>
        public const int MaxClipMaskSources = 3;

        /// <summary>
        /// タグごとの拡張設定
        /// </summary>
        [Serializable]
        public class PartExtraSetting
        {
            // AvatarData.categories の Tag と対応
            public string Tag { get { return tag; } set { tag = value; } }
            [SerializeField]
            string tag;

            // 既定 Normal
            public AvatarExBlendMode BlendMode { get { return blendMode; } set { blendMode = value; } }
            [SerializeField]
            AvatarExBlendMode blendMode = AvatarExBlendMode.Normal;

            // 空リスト = マスクなし。最大 MaxClipMaskSources 件
            public List<string> ClipMaskSourceTags { get { return clipMaskSourceTags; } }
            [SerializeField]
            List<string> clipMaskSourceTags = new List<string>();

            // 合成後の結果に対して一括反転
            public bool InvertMask { get { return invertMask; } set { invertMask = value; } }
            [SerializeField]
            bool invertMask;

            public PartExtraSetting(string tag)
            {
                this.tag = tag;
            }
        }

        // タグ名をキーにした拡張設定の一覧（AvatarData.categories とはタグ名文字列で紐づく並行リスト）
        public List<PartExtraSetting> PartExtraSettings { get { return partExtraSettings; } }
        [SerializeField]
        List<PartExtraSetting> partExtraSettings = new List<PartExtraSetting>();

        /// <summary>
        /// タグ名から拡張設定を取得する。無ければ null
        /// </summary>
        public PartExtraSetting FindSetting(string tag)
        {
            return partExtraSettings.Find(x => x.Tag == tag);
        }

#if UNITY_EDITOR
        // Utage の属性ドロワー（GuiDrawerHelper.CallFunctionSub）は、対象の実行時型（this.GetType()）に
        // 対して Type.GetMethod で関数名を引く。Type.GetMethod は基底型の private メンバをヒットさせない
        // ため、AvatarData.cs の ReimportResources / OnApply / UpdateFunction / GetThis はすべて private
        // で、AvatarExData（派生クラス）のインスペクタでは無反応・無言で失敗する
        // （GuiDrawerHelper.cs の Assert.IsTrue(true, ...) は常に真なので警告も出ない）。
        // 同名メソッドをここに用意し、リフレクションで基底の private 実装へ委譲することで解決する。

        static readonly Dictionary<string, MethodInfo> BaseEditorMethods = new Dictionary<string, MethodInfo>();

        void ReimportResources() => InvokeBaseEditorMethod(nameof(ReimportResources));
        void OnApply() => InvokeBaseEditorMethod(nameof(OnApply));
        void UpdateFunction() => InvokeBaseEditorMethod(nameof(UpdateFunction));

        // NovelAvatarPatternDrawer（previewPattern 用）がこの名前を呼ぶ
        AvatarData GetThis() => this;

        void InvokeBaseEditorMethod(string name)
        {
            if (!BaseEditorMethods.TryGetValue(name, out MethodInfo method))
            {
                method = typeof(AvatarData).GetMethod(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                BaseEditorMethods[name] = method;
            }

            if (method == null)
            {
                AvatarExLog.Error(
                    $"AvatarData.{name} が見つかりません。Utage のバージョンが変わり、内部実装名が"
                    + "変更された可能性があります。AvatarExData のインスペクタ拡張の対応が必要です。",
                    this);
                return;
            }

            try
            {
                method.Invoke(this, null);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                // AvatarData.UpdateFunction は最後に GUIUtility.ExitGUI() で ExitGUIException を
                // 投げる。これは正常フローの一部（ApplyModifiedProperties が古い値で上書きするのを
                // 防ぐ）なので、TargetInvocationException でラップしたままにせず元の型で再送出する
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
        }
#endif
    }
}
