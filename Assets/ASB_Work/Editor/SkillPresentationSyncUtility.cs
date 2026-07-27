using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillPresentationCatalog._bindings(private [SerializeField] 배열)를 에디터에서 안전하게 갱신합니다.
/// 런타임 클래스(SkillPresentationCatalog.cs)는 수정하지 않습니다.
/// </summary>
public static class SkillPresentationSyncUtility
{
    public static void UpsertBinding(SkillPresentationCatalog catalog, int skillIndex, SkillPresentationData data)
    {
        if (catalog == null || data == null)
        {
            return;
        }

        var so = new SerializedObject(catalog);
        SerializedProperty bindings = so.FindProperty("_bindings");
        if (bindings == null)
        {
            Debug.LogError("[SkillPresentationSyncUtility] _bindings 필드를 찾지 못했습니다.");
            return;
        }

        int targetIndex = -1;
        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty element = bindings.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("SkillIndex").intValue == skillIndex)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0)
        {
            targetIndex = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(targetIndex);
        }

        SerializedProperty targetElement = bindings.GetArrayElementAtIndex(targetIndex);
        targetElement.FindPropertyRelative("SkillIndex").intValue = skillIndex;
        targetElement.FindPropertyRelative("Presentation").objectReferenceValue = data;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }
}
