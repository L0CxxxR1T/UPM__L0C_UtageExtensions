using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Utage;

namespace XXXL0C.UtagePlugins.AvatarEx.Editor
{
    /// <summary>
    /// AvatarExData 専用インスペクタ。
    /// AvatarData.NovelAvatarDataInspector は editorForChildClasses 未指定のためサブクラスには
    /// 継承されず、このエディタが単独で有効になる（Spec_AvatarEx.md 4章）
    /// </summary>
    [CustomEditor(typeof(AvatarExData))]
    public sealed class AvatarExDataEditor : UnityEditor.Editor
    {
        AvatarExData Data => target as AvatarExData;

        // Apply による categories の再構築を検知するための直前スナップショット。
        // AvatarData.UpdateFunction の Apply 分岐は内部で EditorUtility.SetDirty を呼ばないため
        // （Reimport 分岐はボタン押下時点で呼んでいるが、Apply は呼んでいない）、
        // タグ構成が変わらない Apply（スプライトの差し替えのみ等）では asset が dirty にならず
        // 保存が漏れる可能性がある。ここで前回描画時との差分を見て補う
        string lastCategorySignature;

        public override void OnInspectorGUI()
        {
            // AvatarData 標準の UI（Apply / ReimportResources ボタン等）をそのまま描画する。
            // これは Utage の [UpdateFunction] ドロワー経由で GUIUtility.ExitGUI() を送出することがあるため、
            // try/catch で握り潰してはいけない（Apply/Reimport 自体が壊れる）
            // Apply 実行時はこの GUIUtility.ExitGUI() でここから下のコードごと処理が打ち切られるため、
            // 変化の検知は「前回の OnInspectorGUI 呼び出し時点の状態」との比較を次回描画で行う形になる
            base.OnInspectorGUI();

            AvatarExData data = Data;
            if (data == null)
            {
                return;
            }

            string signature = BuildCategorySignature(data);
            if (lastCategorySignature != null && lastCategorySignature != signature)
            {
                // 前回描画時から categories の中身が変わっている＝Apply が実行された
                EditorUtility.SetDirty(data);
            }
            lastCategorySignature = signature;

            SyncPartExtraSettings(data);
            DrawPartExtraSettingsUI(data);
            DrawConsistencyWarnings(data);
        }

        // categories の内容を比較用の文字列に変換する（タグ・並び順・スプライト構成の変化を検知するため）
        static string BuildCategorySignature(AvatarExData data)
        {
            var parts = new List<string>();
            foreach (AvatarData.Category category in data.categories)
            {
                var spriteIds = category.Sprites.Select(s => s == null ? 0 : s.GetInstanceID());
                parts.Add($"{category.Tag}:{category.SortOrder}:[{string.Join(",", spriteIds)}]");
            }
            return string.Join(";", parts);
        }

        // categories の現在のタグ一覧と PartExtraSettings を突き合わせ、差分がある場合のみ書き戻す
        void SyncPartExtraSettings(AvatarExData data)
        {
            var currentTags = new HashSet<string>();
            foreach (AvatarData.Category category in data.categories)
            {
                currentTags.Add(category.Tag);
            }

            var existingTags = new HashSet<string>();
            var toRemove = new List<AvatarExData.PartExtraSetting>();
            foreach (AvatarExData.PartExtraSetting setting in data.PartExtraSettings)
            {
                existingTags.Add(setting.Tag);
                if (!currentTags.Contains(setting.Tag))
                {
                    toRemove.Add(setting);
                }
            }

            var toAdd = new List<string>();
            foreach (string tag in currentTags)
            {
                if (!existingTags.Contains(tag))
                {
                    toAdd.Add(tag);
                }
            }

            if (toRemove.Count == 0 && toAdd.Count == 0)
            {
                return;
            }

            Undo.RecordObject(data, "Sync AvatarEx Part Settings");
            foreach (AvatarExData.PartExtraSetting setting in toRemove)
            {
                data.PartExtraSettings.Remove(setting);
            }
            foreach (string tag in toAdd)
            {
                data.PartExtraSettings.Add(new AvatarExData.PartExtraSetting(tag));
            }
            EditorUtility.SetDirty(data);
        }

        void DrawPartExtraSettingsUI(AvatarExData data)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("AvatarEx 拡張設定（ブレンドモード・クリッピングマスク）", EditorStyles.boldLabel);

            foreach (AvatarExData.PartExtraSetting setting in data.PartExtraSettings)
            {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    EditorGUILayout.LabelField(setting.Tag, EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    AvatarExBlendMode newBlendMode = (AvatarExBlendMode)EditorGUILayout.EnumPopup("Blend Mode", setting.BlendMode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(data, "Change AvatarEx Blend Mode");
                        setting.BlendMode = newBlendMode;
                        EditorUtility.SetDirty(data);
                    }

                    DrawClipMaskSourceTags(data, setting);

                    EditorGUI.BeginChangeCheck();
                    bool invert = EditorGUILayout.Toggle("Invert Mask", setting.InvertMask);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(data, "Change AvatarEx Invert Mask");
                        setting.InvertMask = invert;
                        EditorUtility.SetDirty(data);
                    }

                    if (setting.ClipMaskSourceTags.Count > 0
                        && !AvatarExValidation.ValidateSetting(data, setting, out AvatarExValidation.Result errorResult, out string errorTag))
                    {
                        EditorGUILayout.HelpBox(AvatarExValidation.ToMessage(errorResult, errorTag), MessageType.Warning);
                    }
                }
            }
        }

        void DrawClipMaskSourceTags(AvatarExData data, AvatarExData.PartExtraSetting setting)
        {
            EditorGUILayout.LabelField($"Clip Mask Sources（最大{AvatarExData.MaxClipMaskSources}件・OR合成）");

            List<string> selectable = AvatarExValidation.EnumerateSelectableSourceTags(data).ToList();

            for (int i = 0; i < setting.ClipMaskSourceTags.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var options = new List<string> { "(なし)" };
                    options.AddRange(selectable);

                    string current = setting.ClipMaskSourceTags[i];
                    int currentIndex = options.IndexOf(current);
                    if (currentIndex < 0)
                    {
                        // 選択可能条件から外れた不正値でも、現在値として表示だけはする
                        options.Add(current);
                        currentIndex = options.Count - 1;
                    }

                    int newIndex = EditorGUILayout.Popup(currentIndex, options.ToArray());
                    if (GUILayout.Button("-", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(data, "Remove AvatarEx Clip Mask Source");
                        setting.ClipMaskSourceTags.RemoveAt(i);
                        EditorUtility.SetDirty(data);
                        GUIUtility.ExitGUI();
                    }
                    else if (newIndex != currentIndex)
                    {
                        Undo.RecordObject(data, "Change AvatarEx Clip Mask Source");
                        if (newIndex == 0)
                        {
                            setting.ClipMaskSourceTags.RemoveAt(i);
                        }
                        else
                        {
                            setting.ClipMaskSourceTags[i] = options[newIndex];
                        }
                        EditorUtility.SetDirty(data);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(setting.ClipMaskSourceTags.Count >= AvatarExData.MaxClipMaskSources))
            {
                if (GUILayout.Button("+ マスクソースを追加"))
                {
                    string firstAvailable = selectable.FirstOrDefault(t => !setting.ClipMaskSourceTags.Contains(t));
                    if (firstAvailable != null)
                    {
                        Undo.RecordObject(data, "Add AvatarEx Clip Mask Source");
                        setting.ClipMaskSourceTags.Add(firstAvailable);
                        EditorUtility.SetDirty(data);
                    }
                    GUIUtility.ExitGUI();
                }
            }
        }

        void DrawConsistencyWarnings(AvatarExData data)
        {
            List<string> issues = AvatarExSpriteConsistencyChecker.Check(data);
            if (issues.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("パーツスプライトの検証", EditorStyles.boldLabel);
            foreach (string issue in issues)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
        }

        // 素のプレビュー（単純重ね、ブレンド・マスクなし）自体は維持する（4.2節）
        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            AvatarExData data = Data;
            if (data == null)
            {
                return;
            }

            SerializedProperty previewPatternProperty = serializedObject.FindProperty("previewPattern");
            if (previewPatternProperty == null)
            {
                return;
            }

            AvatarPattern pattern = previewPatternProperty.boxedValue as AvatarPattern;
            if (pattern == null)
            {
                return;
            }

            data.OnPreviewGUI(r, background, pattern);
        }
    }
}
