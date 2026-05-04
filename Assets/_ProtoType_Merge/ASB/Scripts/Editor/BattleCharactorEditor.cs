using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleCharactor))]
public class BattleCharactorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var battleCharactor = (BattleCharactor)target;

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Force Refresh", GUILayout.Height(24f)))
            {
                Undo.RecordObject(battleCharactor, "BattleCharactor Force Refresh");
                battleCharactor.EditorForcePrototypeRefresh();
                EditorUtility.SetDirty(battleCharactor);
                serializedObject.Update();
            }
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "m_Script")
            {
                continue;
            }

            if (iterator.name == "finalStats")
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(iterator, true);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("CSV Skill Selection", EditorStyles.boldLabel);

        var enemyScript = battleCharactor.GetComponent<EnemyScript>();
        if (enemyScript != null && enemyScript.Data != null && !string.IsNullOrWhiteSpace(enemyScript.Data.Name))
        {
            string enemyName = enemyScript.Data.Name.Trim();
            SerializedProperty unitNameProp = serializedObject.FindProperty("unitName");
            if (unitNameProp != null && unitNameProp.stringValue != enemyName)
            {
                Undo.RecordObject(battleCharactor, "Sync unitName from EnemyScript");
                battleCharactor.SetUnitNameForSkillMatching(enemyScript.Data.Name);
                EditorUtility.SetDirty(battleCharactor);
            }

            serializedObject.Update();
        }

        battleCharactor.RefreshAvailableSkillsForInspector();
        List<SkillData> available = battleCharactor.availableSkills;
        if (available == null || available.Count == 0)
        {
            string resolvedUnitName = battleCharactor.UnitName;
            EditorGUILayout.HelpBox(
                $"해당 이름(unitName)과 매칭되는 스킬이 없거나, SkillManager/SkillDataLoader를 찾을 수 없습니다.\nunitName='{resolvedUnitName}'\n" +
                "씬에 매니저가 있거나 SkillDataLoader를 할당했는지 확인하세요.",
                MessageType.Info);
        }
        else
        {
            int currentSkillIndex = Mathf.Clamp(battleCharactor.SelectedSkillIndex, 0, available.Count - 1);
            string[] skillOptions = BuildSkillOptionLabels(available);
            EditorGUI.BeginChangeCheck();
            int nextSkillIndex = EditorGUILayout.Popup("Selected Skill", currentSkillIndex, skillOptions);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(battleCharactor, "Change Selected Skill");
                battleCharactor.SetSelectedSkillIndex(nextSkillIndex);
                battleCharactor.ResolveSelectedSkill(false);
                battleCharactor.RecalculateStats(applyCurrentHpClamp: false);
                EditorUtility.SetDirty(battleCharactor);
            }

            SkillData selectedSkill = battleCharactor.SelectedSkillData;
            if (selectedSkill == null)
            {
                battleCharactor.ResolveSelectedSkill(false);
                selectedSkill = battleCharactor.SelectedSkillData;
            }

            if (selectedSkill != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField("Skill Index", selectedSkill.skillIndex);
                    EditorGUILayout.TextField("Skill Name", selectedSkill.skillName);
                    EditorGUILayout.FloatField("Skill Value", selectedSkill.skillValue);
                }
            }
        }

        GUILayout.Space(10f);
        EditorGUILayout.LabelField("Weapon Settings", EditorStyles.boldLabel);

        battleCharactor.RefreshAvailableWeapons();
        battleCharactor.ResolveEquippedWeapon(false);
        List<WeaponData> weapons = battleCharactor.availableWeapons;
        if (weapons == null || weapons.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "해당 직업(unitName)에 장착 가능한 무기가 없거나, WeaponManager를 찾을 수 없습니다.",
                MessageType.Info);
        }
        else
        {
            string[] weaponOptions = BuildWeaponOptionLabels(weapons);
            int currentWeaponIndex = Mathf.Clamp(battleCharactor.EquippedWeaponIndex, 0, weapons.Count - 1);

            EditorGUI.BeginChangeCheck();
            int nextWeaponIndex = EditorGUILayout.Popup("Equipped Weapon", currentWeaponIndex, weaponOptions);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(battleCharactor, "Change Weapon");
                battleCharactor.SetEquippedWeaponIndex(nextWeaponIndex);
                EditorUtility.SetDirty(battleCharactor);
            }

            WeaponData equipped = battleCharactor.EquippedWeaponData;
            if (equipped != null)
            {
                EditorGUILayout.Space(4f);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.LabelField("Weapon", EditorStyles.boldLabel);
                    EditorGUILayout.TextField("Name", equipped.WeaponName ?? string.Empty);
                    string desc = string.IsNullOrWhiteSpace(equipped.WeaponDescription)
                        ? "—"
                        : equipped.WeaponDescription;
                    EditorGUILayout.TextField("Description", desc);
                    string skillName = string.IsNullOrWhiteSpace(equipped.WeaponSkillName)
                        ? "—"
                        : equipped.WeaponSkillName;
                    EditorGUILayout.TextField("Weapon Skill", skillName);
                }
            }
        }
    }

    private static string[] BuildSkillOptionLabels(List<SkillData> skills)
    {
        var labels = new string[skills.Count];
        for (int i = 0; i < skills.Count; i++)
        {
            var s = skills[i];
            if (s == null)
            {
                labels[i] = "(null)";
                continue;
            }

            string name = string.IsNullOrWhiteSpace(s.skillName) ? "Unnamed" : s.skillName;
            labels[i] = $"{s.skillIndex} - {name}";
        }

        return labels;
    }

    private static string[] BuildWeaponOptionLabels(List<WeaponData> weapons)
    {
        var labels = new string[weapons.Count];
        for (int i = 0; i < weapons.Count; i++)
        {
            labels[i] = BuildWeaponDropdownLabel(i, weapons[i]);
        }

        return labels;
    }

    private static string BuildWeaponDropdownLabel(int localIndex, WeaponData w)
    {
        if (w == null)
        {
            return $"[{localIndex}] (null)";
        }

        string displayName = string.IsNullOrWhiteSpace(w.WeaponName) ? "Unnamed" : w.WeaponName;
        var sb = new StringBuilder();
        sb.Append('[').Append(localIndex).Append("] ").Append(displayName);

        string bonus = FormatWeaponBonusInline(w);
        if (!string.IsNullOrEmpty(bonus))
        {
            sb.Append(" (").Append(bonus).Append(')');
        }

        return sb.ToString();
    }

    private static string FormatWeaponBonusInline(WeaponData w)
    {
        var parts = new List<string>(4);
        AppendBonus(parts, "HP", w.BonusHP);
        AppendBonus(parts, "ATK", w.BonusATK);
        AppendBonus(parts, "DEF", w.BonusDEF);
        AppendBonus(parts, "SPD", w.BonusSpeed);
        if (parts.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", parts);
    }

    private static void AppendBonus(List<string> parts, string label, float value)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return;
        }

        string sign = value > 0f ? "+" : string.Empty;
        parts.Add($"{label}:{sign}{value:0.##}");
    }
}
