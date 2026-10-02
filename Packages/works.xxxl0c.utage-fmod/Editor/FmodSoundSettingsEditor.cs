using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XXXL0C.UtagePlugins.FMOD;

namespace XXXL0C.UtagePlugins.FMOD.Editor
{
	/// <summary>
	/// FmodSoundSettings のカスタムインスペクタ
	/// ラベル未設定・重複ラベル・FMOD イベント未割り当てを検出して表示する
	/// </summary>
	[CustomEditor(typeof(FmodSoundSettings))]
	public sealed class FmodSoundSettingsEditor : UnityEditor.Editor
	{
		private const string LABEL_EVENTS_PROPERTY = "_labelEvents";
		private const string VOICE_EVENT_PROPERTY = "_voiceEvent";
		private const string LABEL_PROPERTY = "_label";
		private const string EVENT_REF_PROPERTY = "_eventRef";
		private const string GUID_PROPERTY = "Guid";

		public override VisualElement CreateInspectorGUI()
		{
			VisualElement root = new VisualElement();

			HelpBox validationBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning) { style = { display = DisplayStyle.None } };
			root.Add(validationBox);

			InspectorElement.FillDefaultInspector(root, serializedObject, this);

			root.TrackSerializedObjectValue(serializedObject, _ => RefreshValidation(validationBox));
			RefreshValidation(validationBox);

			return root;
		}

		private void RefreshValidation(HelpBox validationBox)
		{
			List<string> problems = new List<string>();

			SerializedProperty labelEvents = serializedObject.FindProperty(LABEL_EVENTS_PROPERTY);
			HashSet<string> seenLabels = new HashSet<string>();
			int emptyEventCount = 0;
			for (int i = 0; i < labelEvents.arraySize; i++)
			{
				SerializedProperty element = labelEvents.GetArrayElementAtIndex(i);
				string label = element.FindPropertyRelative(LABEL_PROPERTY).stringValue;
				SerializedProperty guidProperty = element.FindPropertyRelative(EVENT_REF_PROPERTY).FindPropertyRelative(GUID_PROPERTY);

				if (string.IsNullOrEmpty(label))
				{
					problems.Add($"[{i}] ラベルが未設定です。");
					continue;
				}
				if (!seenLabels.Add(label))
				{
					problems.Add($"ラベル \"{label}\" が重複しています。");
				}
				if (IsGuidEmpty(guidProperty))
				{
					emptyEventCount++;
				}
			}
			if (emptyEventCount > 0)
			{
				problems.Add($"FMOD イベント未設定のラベルが {emptyEventCount} 件あります。");
			}

			SerializedProperty voiceEvent = serializedObject.FindProperty(VOICE_EVENT_PROPERTY);
			SerializedProperty voiceGuid = voiceEvent.FindPropertyRelative(GUID_PROPERTY);
			if (IsGuidEmpty(voiceGuid))
			{
				problems.Add("ボイス再生用イベント（Voice Event）が未設定です。");
			}

			if (problems.Count == 0)
			{
				validationBox.style.display = DisplayStyle.None;
				return;
			}

			validationBox.style.display = DisplayStyle.Flex;
			validationBox.text = "検証エラー:\n" + string.Join("\n", problems);
		}

		private static bool IsGuidEmpty(SerializedProperty guidProperty)
		{
			SerializedProperty data1 = guidProperty?.FindPropertyRelative("Data1");
			SerializedProperty data2 = guidProperty?.FindPropertyRelative("Data2");
			if (data1 == null || data2 == null)
			{
				return true;
			}
			// FMOD.GUID は Data1〜Data4 の int で構成される。Data1/Data2 が 0 なら実質未設定とみなす
			return data1.intValue == 0 && data2.intValue == 0;
		}
	}
}
