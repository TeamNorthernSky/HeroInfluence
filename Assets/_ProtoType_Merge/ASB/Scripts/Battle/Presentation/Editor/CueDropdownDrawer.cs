using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CueDropdownAttribute string을 [프리셋 드롭다운 | 커스텀 텍스트]로 표시.
/// 프리셋 선택 시 그 값 저장, 커스텀 선택 시 텍스트로 직접 입력.
/// </summary>
[CustomPropertyDrawer(typeof(CueDropdownAttribute))]
public class CueDropdownDrawer : PropertyDrawer
{
    private const string CustomOption = "<커스텀…>";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        string[] presets = PresentationCues.Presets;
        var options = new List<string>(presets) { CustomOption };

        string cur = property.stringValue ?? string.Empty;
        int idx = -1;
        for (int i = 0; i < presets.Length; i++)
        {
            if (string.Equals(presets[i], cur.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                idx = i;
                break;
            }
        }
        bool isCustom = idx < 0;
        if (isCustom)
        {
            idx = options.Count - 1;
        }

        EditorGUI.BeginProperty(position, label, property);
        Rect r = EditorGUI.PrefixLabel(position, label);
        float half = r.width * 0.5f;
        Rect popupRect = new Rect(r.x, r.y, half - 2f, r.height);
        Rect rightRect = new Rect(r.x + half, r.y, half, r.height);

        int newIdx = EditorGUI.Popup(popupRect, idx, options.ToArray());
        if (newIdx != idx && newIdx < presets.Length)
        {
            property.stringValue = presets[newIdx];
            isCustom = false;
        }
        else if (newIdx >= presets.Length)
        {
            isCustom = true;
        }

        if (isCustom)
        {
            property.stringValue = EditorGUI.TextField(rightRect, property.stringValue);
        }
        else
        {
            EditorGUI.LabelField(rightRect, property.stringValue);
        }

        EditorGUI.EndProperty();
    }
}
