using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// AnimatorStateDropdownAttribute가 붙은 string 필드를 base AnimatorController의 state 이름 드롭다운으로 표시.
/// 목록에 없는 기존 값은 보존하고, &lt;None&gt;은 빈 문자열로 저장한다.
/// </summary>
[CustomPropertyDrawer(typeof(AnimatorStateDropdownAttribute))]
public class AnimatorStateDropdownDrawer : PropertyDrawer
{
    private const string NoneLabel = "<None>";
    private const string MissingSuffix = "  (목록 없음)";

    private static string _cachedControllerName;
    private static string[] _cachedStates;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var attr = (AnimatorStateDropdownAttribute)attribute;
        string[] states = GetStates(attr.ControllerName);

        string current = property.stringValue;
        var options = new List<string> { NoneLabel };
        options.AddRange(states);

        int index;
        if (string.IsNullOrEmpty(current))
        {
            index = 0;
        }
        else
        {
            index = options.IndexOf(current);
            if (index < 0)
            {
                // 목록에 없는 값(레거시/수동입력)은 보존 항목으로 추가해 데이터 손실 방지.
                options.Add(current + MissingSuffix);
                index = options.Count - 1;
            }
        }

        EditorGUI.BeginProperty(position, label, property);
        int newIndex = EditorGUI.Popup(position, label.text, index, options.ToArray());
        if (newIndex != index)
        {
            if (newIndex == 0)
            {
                property.stringValue = string.Empty;
            }
            else
            {
                string picked = options[newIndex];
                property.stringValue = picked.EndsWith(MissingSuffix) ? current : picked;
            }
        }
        EditorGUI.EndProperty();
    }

    private static string[] GetStates(string controllerName)
    {
        if (_cachedStates != null && _cachedControllerName == controllerName)
        {
            return _cachedStates;
        }

        var list = new List<string>();
        string[] guids = AssetDatabase.FindAssets($"{controllerName} t:AnimatorController");
        if (guids.Length > 0)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (ctrl != null)
            {
                foreach (AnimatorControllerLayer layer in ctrl.layers)
                {
                    CollectStates(layer.stateMachine, layer.name, list);
                }
            }
        }

        _cachedStates = list.ToArray();
        _cachedControllerName = controllerName;
        return _cachedStates;
    }

    private static void CollectStates(AnimatorStateMachine sm, string path, List<string> outList)
    {
        if (sm == null)
        {
            return;
        }

        foreach (ChildAnimatorState s in sm.states)
        {
            if (s.state == null)
            {
                continue;
            }

            string statePath = path + "." + s.state.name;
            if (!outList.Contains(statePath))
            {
                outList.Add(statePath);
            }
        }

        foreach (ChildAnimatorStateMachine sub in sm.stateMachines)
        {
            CollectStates(sub.stateMachine, path + "." + sub.stateMachine.name, outList);
        }
    }
}
