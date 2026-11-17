using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-17 | 추적 초안만 즉시 편집하고 명시 저장 때만 보스 공유 프로필에 기록합니다.
    [CustomEditor(typeof(JcBossFollowOffsetControls))]
    public sealed class JcBossFollowOffsetControlsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controls = ((JcBossFollowOffsetControls)target).Controls;
            if (!controls || !controls.Draft || !controls.Source)
            {
                EditorGUILayout.HelpBox("보스 전투 진입 시 JC_BossCamera 아래에 자동 연결됩니다.", MessageType.Info);
                return;
            }
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("공유 저장 프로필", controls.Source, typeof(JcBossCameraProfile), false);
            // JC | 2026-11-17 | 기존 공유값을 쓰는 저스티스 크래시의 추가 적용 범위와 0값의 동작도 안내합니다.
            EditorGUILayout.HelpBox("보스 투사체·돌진과 루미나 광역기, 저스티스 크래시의 초점을 왼쪽으로 이동합니다. 광역기는 실제 대상 열의 평균을 사용합니다. 크래시는 곡선 후반의 오른쪽 이탈도 같은 오프셋 크기 이내로 억제하며, 0이면 원래 초점을 추적합니다. 네코밍 쓰러지면 안돼는 아래 X/Y 위치 보정을 사용합니다.", MessageType.Info);
            var values = new SerializedObject(controls.Draft);
            values.Update();
            EditorGUILayout.PropertyField(values.FindProperty("followMiddleOffset"));
            EditorGUILayout.PropertyField(values.FindProperty("followRightOffset"));
            EditorGUILayout.PropertyField(values.FindProperty("nekoBarrageOffset"));
            // JC | 2026-11-17 | BPC가 매 추적 프레임 초안을 읽습니다. 기본뷰 적용을 호출하지 않아 시전 중 편집도 기존 보간을 따릅니다.
            values.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("플레이 중 다음 프레임부터 반영됩니다. 일시정지 중에는 프레임을 진행해야 보입니다. 종료 전에 저장 버튼을 누르면 본게임·보스 테스트씬에 공통 적용됩니다. 저장 전 편집은 임시입니다.", MessageType.None);
            if (GUILayout.Button(new GUIContent("현재 오프셋만 프로필에 저장", "중앙/오른쪽 초점 보정과 네코밍 X/Y 이동량을 보스 본게임·테스트 공유 프로필에 저장합니다.")))
            {
                Undo.RecordObject(controls.Source, "JC 보스 추적 오프셋 저장");
                CopyOffsets(controls.Draft, controls.Source);
                EditorUtility.SetDirty(controls.Source);
                AssetDatabase.SaveAssetIfDirty(controls.Source);
            }
            if (GUILayout.Button(new GUIContent("저장된 오프셋 다시 불러오기", "현재 임시 오프셋 편집을 버리고 공유 프로필의 저장값으로 되돌립니다.")))
            {
                Undo.RecordObject(controls.Draft, "JC 보스 추적 오프셋 불러오기");
                CopyOffsets(controls.Source, controls.Draft);
            }
        }

        // JC | 2026-11-17 | 오프셋 버튼은 카메라 구도·레이아웃 기준 등 다른 저장값을 덮어쓰지 않습니다.
        static void CopyOffsets(JcBossCameraProfile from, JcBossCameraProfile to)
        {
            to.followMiddleOffset = Mathf.Max(0f, from.followMiddleOffset);
            to.followRightOffset = Mathf.Max(0f, from.followRightOffset);
            // JC | 2026-11-17 | 위치 보정도 같은 명시 저장/불러오기 절차를 사용합니다.
            to.nekoBarrageOffset = from.nekoBarrageOffset;
        }
    }
}
