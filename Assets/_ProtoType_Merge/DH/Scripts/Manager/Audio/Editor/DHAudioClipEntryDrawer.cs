using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DHAudioClipEntry))]
public sealed class DHAudioClipEntryDrawer : PropertyDrawer
{
    private static bool IsBgm(SerializedProperty property) => property.propertyPath.StartsWith("bgmClips.");

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = property.isExpanded ? (IsBgm(property) ? 5 : 6) : 1;
        return lines * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            DrawField(ref row, property, "key", "Key");
            if (IsBgm(property))
            {
                DrawField(ref row, property, "introClip", "Intro Clip");
                DrawField(ref row, property, "clip", "Loop Clip");
                DrawField(ref row, property, "defaultVolume", "Default Volume");
            }
            else
            {
                DrawField(ref row, property, "clip", "Clip");
                DrawField(ref row, property, "defaultVolume", "Default Volume");
                DrawField(ref row, property, "loop", "Loop");
                DrawField(ref row, property, "allowOverlap", "Allow Overlap");
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }

    private static void DrawField(ref Rect row, SerializedProperty property, string name, string label)
    {
        row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(row, property.FindPropertyRelative(name), new GUIContent(label));
    }
}
