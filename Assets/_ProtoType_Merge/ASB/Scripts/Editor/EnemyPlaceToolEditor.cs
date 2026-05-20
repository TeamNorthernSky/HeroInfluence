using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyPlaceTool))]
public class EnemyPlaceToolEditor : Editor
{
    private static readonly Quaternion RotationY180 = Quaternion.Euler(0f, 180f, 0f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);

        EnemyPlaceTool tool = (EnemyPlaceTool)target;
        if (tool == null)
        {
            return;
        }

        if (GUILayout.Button("하위 유닛 전체 180도 회전", GUILayout.Height(28f)))
        {
            int count = RotateAllChildBattleCharacters(tool.transform);
            Debug.Log($"[EnemyPlaceTool] BattleCharactor {count}개 처리 — localRotation = (0, 180, 0)");
        }
    }

    private static int RotateAllChildBattleCharacters(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        BattleCharactor[] characters = root.GetComponentsInChildren<BattleCharactor>(true);
        int count = 0;

        for (int i = 0; i < characters.Length; i++)
        {
            BattleCharactor character = characters[i];
            if (character == null)
            {
                continue;
            }

            Transform unitTransform = character.transform;
            Undo.RecordObject(unitTransform, "EnemyPlaceTool Rotate 180 Y");
            unitTransform.localRotation = RotationY180;
            EditorUtility.SetDirty(unitTransform);
            count++;
        }

        if (count > 0)
        {
            EditorUtility.SetDirty(root.gameObject);
        }

        return count;
    }
}
