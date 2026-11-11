using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-11 | 두 정본의 선택과 공통 보호 설정을 모든 실제 전투가 공유합니다. 코어 연출은 적용 대상에서 제외합니다.
    public enum JcBlackBulletCameraCanon
    {
        [InspectorName("정본 1 · 직접 촬영한 저장 구도")] Authored = 1,
        [InspectorName("정본 2 · 배치·거리별 구도")] Adaptive = 2
    }

    [CreateAssetMenu(menuName = "JC/전투/블랙불릿 공통 카메라")]
    public sealed class JcBlackBulletCameraProfile : ScriptableObject
    {
        [InspectorName("사용할 정본"), Tooltip("모든 튜토리얼·일반·점령·은행·보스 전투가 같은 선택을 사용합니다. 플레이 중 선택 변경은 다음 공격부터 적용됩니다.")]
        public JcBlackBulletCameraCanon canon = JcBlackBulletCameraCanon.Adaptive;
        [InspectorName("정본 1 저장 구도"), Tooltip("기존 은행 편집 툴로 촬영한 24개 구도를 모든 전투가 공유합니다. 전장마다 별도 복사하지 않습니다.")]
        public JcBankSkillCameraProfile authoredProfile;
        // JC | 2026-11-11 | 실내의 기본 카메라가 정본2 목적지를 바꾸지 않도록 일반전 기준 뷰와 진형 원점을 보존합니다.
        [HideInInspector] public JcBattleCameraProfile referenceCamera;
        [HideInInspector] public Vector3 referencePlayerOrigin = new Vector3(0, .3f, -6);
        [Header("공통 전환")]
        [InspectorName("공격 앵글 전환 시간 (초)"), Min(0), Tooltip("1배속 연출 시간 기준입니다. 공격 중에는 Timeline과 함께 빨라지거나 느려지고 타격 정지/일시정지 때 멈춥니다. 0이면 즉시 도착합니다.")]
        public float transitionSeconds = .35f;
        // JC | 2026-11-11 | 별도 조절 오브젝트도 이 프로필의 동일 초안을 편집합니다. 기본1에서는 승인된 카메라를 그대로 유지합니다.
        [InspectorName("원근감 비율"), Range(.2f, 1f), Tooltip("1은 현재 모습입니다. 낮추면 카메라를 뒤로 이동하고 화각을 좁혀 가까운 인물과 먼 인물의 크기 차이를 완화합니다. JC_Perspective에서 플레이·일시정지 중 조절하고 명시적으로 저장합니다.")]
        public float perspectiveStrength = 1f;
        // JC | 2026-11-10 | 확대율 우선으로 시점을 낮추던 옵션을 실제 렌더링 픽셀 여백으로 교체합니다.
        [InspectorName("화면 경계 안전 여백 (픽셀)"), Range(0, 150), Tooltip("필수 표시 영역의 최소 여백입니다. 150픽셀 이내 확대는 강제하지 않으며 구도와 데미지 가독성을 우선합니다. 실제 렌더링 해상도 기준입니다.")]
        public float edgeMarginPixels = 20f;

        // JC | 2026-11-10 | 초기 구도 안의 이동 영역과 필수 표시 범위를 같은 계산에 제공합니다.
        [InspectorName("기준 구도 이동 비율 (좌우 / 상하)"), Tooltip("배치별 조절 전 기준 구도를 구할 때 사용하는 초기 화면 폭/높이 비율입니다. 기본 0.08/0.04. 이후 높이·회전은 아래 배치별 설정을 따릅니다.")]
        public Vector2 panFraction = new Vector2(.08f, .04f);
        // JC | 2026-11-10 | 기존 중앙 구도는 유지할 끝점으로 보존하고 거리별 높이 및 지정 구도 보간을 별도로 조절합니다.
        [Header("거리별 높이")]
        [InspectorName("먼 거리 높이 비율"), Range(.6f, 1f), Tooltip("먼 대상에서 기준 구도의 지면 상대 높이에 곱합니다. 기존 0.8을 유지하며 가까운 대상은 아래 0.9로 높아집니다.")]
        public float rightLayoutHeightRatio = .8f;
        // JC | 2026-11-10 | 최소 하강은 원래 높이의 90%이며 두 캐릭터의 월드 거리로 먼 거리 80%까지 부드럽게 연결합니다.
        [InspectorName("가까운 거리 높이 비율"), Range(.6f, 1f), Tooltip("기준 구도의 지면 상대 높이에 곱합니다. 기본 0.9이며 먼 거리 비율보다 낮아지지 않습니다.")]
        public float nearHeightRatio = .9f;
        [InspectorName("높이 보간 거리 (가까움 / 멂)"), Tooltip("두 캐릭터 사이의 월드 거리입니다. 기본 8~14.58은 일반 전투의 전열 간~후열 간 거리입니다. 가까움 이하는 90%, 멂 이상은 80%를 적용합니다.")]
        public Vector2 heightDistanceRange = new Vector2(8f, 14.58f);
        [Header("우측 블랙불릿 → 적 좌측열의 지정 구도")]
        // JC | 2026-11-10 | 사용자 일시정지 Transform의 X/Z와 회전을 보간 끝점으로 사용합니다. 좌측 시전자는 유지, 중앙은 절반입니다.
        [InspectorName("지정 카메라 위치 (X / Z)"), Tooltip("우측 후열→적 좌측 후열에서 직접 조절한 X 8.25 / Z -17.86입니다. 다른 배치에서는 인물 중점 이동을 반영합니다.")]
        public Vector2 crossColumnPosition = new Vector2(8.25f, -17.86f);
        [InspectorName("지정 기울기 / 좌우 회전"), Tooltip("기준 이미지의 X 26.066 / Y -30.6도입니다. 높이 보간으로 높아지면 같은 중심을 바라보는 고도각 차이를 X에 더합니다.")]
        public Vector2 crossColumnAngles = new Vector2(26.066f, -30.6f);
        [InspectorName("기준 인물 중점 (X / Z)"), Tooltip("우측 아군 후열과 적 자체 좌측 후열은 모두 월드 X +3.3입니다. Z -7.3/+7.28의 중점 -0.01을 기준으로 배치 이동을 반영합니다.")]
        public Vector2 crossColumnCenter = new Vector2(3.3f, -.01f);
        [Header("유지할 기존 구도의 기준")]

        [InspectorName("기준 시선 (상하 / 좌우 각도)"), Tooltip("사용자 기준 이미지의 X 24.191 / Y -29도입니다. 배치와 표시물 보호에 맞춰 허용 범위 안에서 보정합니다.")]
        public Vector2 referenceAngles = new Vector2(24.191f, -29f);
        [InspectorName("두 인물 중심 가로 위치"), Range(.35f, .65f), Tooltip("화면 폭 대비 두 인물 중심의 목표입니다. 0.5는 중앙이며 기본 0.54는 약간 오른쪽입니다. UI와 표시물 보호를 우선합니다.")]
        public float pairCenterX = .54f;
        [InspectorName("표적의 가로 간격 비율"), Range(-.15f, .2f), Tooltip("시전자에서 표적까지의 화면 폭 대비 가로 간격입니다. 0은 세로 정렬, 음수는 표적이 약간 왼쪽입니다. 기본 -0.02이며 표시물 보호에 따라 보정됩니다.")]
        public float pairHorizontalGap = -.02f;
        [InspectorName("수평 이동 한도 (월드)"), Min(0), Tooltip("승인된 초기 구도 주변에서 허용하는 수평 이동 반경입니다. 높이와 별도로 계산합니다.")]
        public float adaptiveTravel = 8;
        [InspectorName("회전 오프셋 한도 (기울기 / 좌우)"), Tooltip("기준 시선으로부터 허용하는 각도입니다. 두 인물 배치와 필수 표시 영역을 만족하는 범위에서만 사용합니다.")]
        public Vector2 adaptiveAngles = new Vector2(10, 24);
        [InspectorName("최대 FOV"), Range(30, 120), Tooltip("필수 표시 영역을 담을 수 없는 제한은 경고로 표시합니다. 제한을 몰래 넘겨 카메라를 돌리거나 이동하지 않습니다.")]
        public float maximumFov = 110;
        [InspectorName("UI 예약 비율 (좌 / 우 / 하 / 상)"), Tooltip("화면 크기 비율입니다. 기본 우측 0.15는 턴 목록, 상단 0.06은 메뉴를 위한 여유입니다. 픽셀 여백이 추가됩니다.")]
        public Vector4 uiInsets = new Vector4(.02f, .15f, .04f, .06f);
        [Header("주요 이펙트 보호 범위 · 월드 단위")]
        [InspectorName("1번 피격 효과 반경"), Min(0), Tooltip("주요 탄착 효과가 확장되는 반경입니다. 흩어지는 미세 파편 전체는 포함하지 않습니다.")]
        public float skill1ImpactRadius = 2f;
        [InspectorName("2번 피격 효과 반경"), Min(0)] public float skill2ImpactRadius = 2f;
        [InspectorName("3번 피격 효과 반경"), Min(0)] public float skill3ImpactRadius = 2.5f;
        [InspectorName("4번 피격 효과 반경"), Min(0)] public float skill4ImpactRadius = 2.5f;
        [InspectorName("투사체 경로 여유"), Min(0), Tooltip("발사 소켓에서 타깃까지의 직선 경로 주변 반경입니다.")]
        public float projectileRadius = .4f;
        [InspectorName("피격 자세 여유"), Min(0), Tooltip("적의 신체와 숫자 생성 위치에 확보할 이동 여유입니다.")]
        public float targetMotionMargin = .35f;
        [InspectorName("데미지 숫자 예약 자릿수"), Range(1, 12), Tooltip("실제 숫자를 미리 계산하지 않고 이 자릿수 및 MISS·치명타 아이콘에 필요한 공간을 확보합니다. 기본 6자리. 실제 생성된 숫자도 별도로 반영합니다.")]
        public int damageDigits = 6;
        public float ImpactRadius(int family) => Mathf.Max(0, family == 3010 ? skill1ImpactRadius : family == 3020 ? skill2ImpactRadius : family == 3030 ? skill3ImpactRadius : skill4ImpactRadius);

        // JC | 2026-11-10 | 강화형도 기존 FamilyId로 같은 슬롯을 사용하며 다른 캐릭터/스킬은 적용 대상이 아닙니다.
        public static bool Supports(int family) => family == 3010 || family == 3020 || family == 3030 || family == 3040;
    }
}
