using System;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-06 | 은행 블랙불릿의 4개 스킬을 직접 저작한 월드 구도로 재생합니다.
    // 스킬 자체의 원근감 배율만 적용하며 기본뷰의 원근감·X 오프셋은 다시 적용하지 않습니다.
    [CreateAssetMenu(menuName = "JC/전투/은행 블랙불릿 스킬 카메라")]
    public sealed class JcBankSkillCameraProfile : ScriptableObject
    {
        [Serializable]
        public sealed class Shot
        {
            [Tooltip("켜면 이 스킬의 기본·강화형으로 해당 적 칸을 공격할 때 저장 구도를 사용합니다. 끄면 해당 조합만 기존 자동 연출을 사용합니다.")]
            public bool enabled;
            [Tooltip("원근감 배율 1의 기준 월드 위치입니다. 배율을 낮추면 시선 축 뒤로 이동합니다. 은행 본게임과 BankTestScene이 공유합니다.")]
            public Vector3 position;
            [Tooltip("카메라 월드 회전(X/Y/Z, 도)입니다. 자동 표적 추적으로 덮어쓰지 않습니다.")]
            public Vector3 rotation;
            [Range(10, 100), Tooltip("원근감 배율 1의 기준 수직 시야각(도)입니다. 배율을 낮추면 실제 시야각도 줄여 기준면 크기를 유지합니다.")]
            public float fieldOfView = 32.20423f;
            // JC | 2026-11-06 | 전열·후열 전체의 원근감 배율을 저장합니다. 조절 범위만 확장하며 기존 저장값은 변경하지 않습니다.
            [Range(.2f, 1f), Tooltip("1은 저장한 원래 원근감입니다. 낮을수록 먼 적의 상대 크기가 커집니다. 0.5는 기준면 깊이를 2배로 늘리고 화각을 줄입니다. 전열·후열 모두 편집창에서 조절할 수 있습니다.")]
            public float perspectiveStrength = 1f;
            [HideInInspector] public Vector3 perspectiveCenter;
            public Shot Copy() => (Shot)MemberwiseClone();
            JcBattleCameraPose Pose() => new JcBattleCameraPose {
                position = position, rotation = rotation, fieldOfView = fieldOfView, perspectiveStrength = perspectiveStrength
            };
            public void Capture(Camera camera, Vector3 center)
            {
                // JC | 2026-11-06 | 기존 공통 역변환으로 실제 카메라를 기준 포즈로 저장해 재저장 시 배율이 누적되지 않게 합니다.
                var pose = Pose(); pose.Capture(camera, center);
                if (perspectiveStrength < 1f && (pose.fieldOfView < 9.999f || pose.fieldOfView > 100.001f))
                    throw new InvalidOperationException("현재 원근감 배율의 FOV 범위를 벗어났습니다. 편집창의 시야각 슬라이더로 조정한 뒤 저장하세요.");
                position = perspectiveStrength == 1f ? camera.transform.position : pose.position;
                rotation = camera.transform.eulerAngles;
                fieldOfView = perspectiveStrength == 1f ? camera.fieldOfView : Mathf.Clamp(pose.fieldOfView, 10, 100);
                perspectiveCenter = center;
            }
            public void Resolve(out Vector3 p, out Quaternion q, out float fov)
            {
                // JC | 2026-11-06 | 배율 1에서는 기존 사용자 구도를 수치 보정 없이 그대로 반환합니다.
                if (perspectiveStrength == 1f) { p = position; q = Quaternion.Euler(rotation); fov = fieldOfView; return; }
                Pose().Resolve(perspectiveCenter, out p, out q, out fov);
            }
            public void Apply(Camera camera)
            {
                Resolve(out var p, out var q, out var fov);
                camera.transform.SetPositionAndRotation(p, q);
                camera.orthographic = false;
                camera.fieldOfView = fov;
            }
        }

        // JC | 2026-11-06 | 각 스킬을 적 전열 3칸·후열 3칸으로 분리합니다. 유닛 종류/화면상 순서가 아닌 전투 칸이 키입니다.
        [Serializable]
        public sealed class TargetShots
        {
            [Tooltip("0~2는 적 전열 Grid_2_0~2, 3~5는 적 후열 Grid_3_0~2입니다. 편집창에서 스킬과 타겟을 선택하여 저장하세요.")]
            public Shot[] targets = { new Shot(), new Shot(), new Shot(), new Shot(), new Shot(), new Shot() };
        }
        [Tooltip("1번 블랙 데빌 퍼펙트 에임 샷의 타겟 6칸 구도입니다.")] public TargetShots skill1 = new TargetShots();
        [Tooltip("2번 프론트 라인 브레이크 불릿의 타겟 6칸 구도입니다.")] public TargetShots skill2 = new TargetShots();
        [Tooltip("3번 트윈 트리거 더블 버스트의 타겟 6칸 구도입니다.")] public TargetShots skill3 = new TargetShots();
        [Tooltip("4번 데드엔드 라스트 트리거의 타겟 6칸 구도입니다.")] public TargetShots skill4 = new TargetShots();

        public Shot GetSlot(int skill, int target)
        {
            var group = skill switch { 0 => skill1, 1 => skill2, 2 => skill3, 3 => skill4, _ => null };
            return group?.targets != null && target >= 0 && target < 6 && target < group.targets.Length ? group.targets[target] : null;
        }

        // JC | 2026-11-06 | 기존 ASB 절대 좌표(적 전열 x=2/후열 x=3, 열 y=0~2)를 그대로 읽습니다.
        // 대상이 없거나 적 칸이 아니면 다른 타겟 구도를 대신 선택하지 않습니다.
        public static int TargetIndex(Vector2Int cell) => cell.x >= 2 && cell.x <= 3 && cell.y >= 0 && cell.y <= 2
            ? (cell.x - 2) * 3 + cell.y : -1;

        // JC | 2026-11-06 | 카탈로그를 새로 만들지 않고 기존 스킬 계열 판정을 그대로 사용합니다.
        public Shot FindEnabled(int skillIndex, Vector2Int targetCell)
        {
            int family = HeroSkillRules.FamilyId(skillIndex);
            int slot = family switch { 3010 => 0, 3020 => 1, 3030 => 2, 3040 => 3, _ => -1 };
            var shot = GetSlot(slot, TargetIndex(targetCell));
            return shot != null && shot.enabled ? shot : null;
        }
    }
}
