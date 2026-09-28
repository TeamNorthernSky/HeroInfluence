using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillPresentationPreviewController))]
public class SkillPresentationPreviewControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (SkillPresentationPreviewController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview Control", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Actor", controller.ActorName);
        EditorGUILayout.LabelField("Target", controller.TargetName);
        EditorGUILayout.LabelField("State", controller.IsPlaying ? "실행 중" : "대기");

        using (new EditorGUI.DisabledScope(!Application.isPlaying || controller.IsPlaying))
        {
            if (GUILayout.Button("▶ Play Selected Skill", GUILayout.Height(30)))
            {
                controller.PlaySelectedSkill();
            }
        }

        DrawEnemyAttackPreview(controller);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Reset Preview"))
            {
                controller.ResetPreview();
            }
        }
    }

    private void DrawEnemyAttackPreview(SkillPresentationPreviewController controller)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Enemy Attack Preview", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode에서 생성된 적 공격자, 실제 보유 스킬, 플레이어 대상을 선택할 수 있습니다.",
                MessageType.Info);
            return;
        }

        IReadOnlyList<BattleCharactor> enemies = controller.PreviewEnemies;
        if (enemies == null || enemies.Count == 0)
        {
            EditorGUILayout.HelpBox("선택 가능한 프리뷰 적이 없습니다.", MessageType.Warning);
            return;
        }

        string[] enemyLabels = BuildUnitLabels(controller, enemies);
        int enemyIndex = FindUnitIndex(enemies, controller.SelectedEnemyActor);
        enemyIndex = Mathf.Clamp(enemyIndex, 0, enemies.Count - 1);

        EditorGUI.BeginChangeCheck();
        int newEnemyIndex = EditorGUILayout.Popup("Attacker", enemyIndex, enemyLabels);
        if (EditorGUI.EndChangeCheck())
        {
            controller.SetSelectedEnemyActor(enemies[newEnemyIndex]);
        }

        List<PreviewEnemySkillOption> skillOptions = controller.GetSelectedEnemySkillOptions();
        if (skillOptions.Count == 0)
        {
            EditorGUILayout.HelpBox("선택한 적에게 실행 가능한 클래스/무기 스킬이 없습니다.", MessageType.Warning);
        }
        else
        {
            string[] skillLabels = new string[skillOptions.Count];
            int skillIndex = 0;
            for (int i = 0; i < skillOptions.Count; i++)
            {
                PreviewEnemySkillOption option = skillOptions[i];
                skillLabels[i] = option.Label;
                if (option.Kind == controller.SelectedEnemySkillKind &&
                    option.SkillIndex == controller.SelectedEnemySkillIndex)
                {
                    skillIndex = i;
                }
            }

            EditorGUI.BeginChangeCheck();
            int newSkillIndex = EditorGUILayout.Popup("Skill", skillIndex, skillLabels);
            if (EditorGUI.EndChangeCheck())
            {
                PreviewEnemySkillOption selected = skillOptions[newSkillIndex];
                controller.SetSelectedEnemySkill(selected.Kind, selected.SkillIndex);
            }
        }

        IReadOnlyList<BattleCharactor> players = controller.PreviewPlayers;
        if (players == null || players.Count == 0)
        {
            EditorGUILayout.HelpBox("선택 가능한 프리뷰 플레이어가 없습니다.", MessageType.Warning);
        }
        else
        {
            string[] playerLabels = BuildUnitLabels(controller, players);
            int targetIndex = FindUnitIndex(players, controller.SelectedEnemyTarget);
            targetIndex = Mathf.Clamp(targetIndex, 0, players.Count - 1);

            EditorGUI.BeginChangeCheck();
            int newTargetIndex = EditorGUILayout.Popup("Target", targetIndex, playerLabels);
            if (EditorGUI.EndChangeCheck())
            {
                controller.SetSelectedEnemyTarget(players[newTargetIndex]);
            }
        }

        using (new EditorGUI.DisabledScope(!controller.CanPlaySelectedEnemySkill))
        {
            if (GUILayout.Button("▶ Play Selected Enemy Attack", GUILayout.Height(30)))
            {
                controller.PlaySelectedEnemySkill();
            }
        }
    }

    private static string[] BuildUnitLabels(
        SkillPresentationPreviewController controller,
        IReadOnlyList<BattleCharactor> units)
    {
        string[] labels = new string[units.Count];
        for (int i = 0; i < units.Count; i++)
        {
            labels[i] = controller.GetPreviewUnitLabel(units[i]);
        }
        return labels;
    }

    private static int FindUnitIndex(
        IReadOnlyList<BattleCharactor> units,
        BattleCharactor selected)
    {
        if (units == null || units.Count == 0)
        {
            return 0;
        }

        for (int i = 0; i < units.Count; i++)
        {
            if (ReferenceEquals(units[i], selected))
            {
                return i;
            }
        }
        return 0;
    }
}
