using System;
using UnityEngine;

// JC | 2026-11-13 | 배지와 화살표는 하나의 설정을 공유하고 회복 표시만 별도 색/토글로 분리합니다.
public enum JcBattleArrowStyle
{
    [InspectorName("평면 · 점멸 윤곽선")] Flat,
    [InspectorName("기존 입체 · 그림자")] Legacy,
    [InspectorName("입체 · 그림자 없음 · 점멸 윤곽선")] BeveledFlashing
}

[Serializable]
public sealed class JcBattleTurnSlotConfiguration
{
    [Tooltip("화살표의 색상·질감만 교체합니다. 세 스타일 모두 같은 크기와 이동을 사용합니다.")]
    public JcBattleArrowStyle style;
    [Min(.1f), Tooltip("초상화 지름 대비 원의 안쪽 지름 비율입니다. 1이면 초상화 경계부터 덮습니다.")]
    public float innerDiameter = .98f;
    [Min(.1f), Tooltip("초상화 지름 대비 원의 바깥쪽 지름 비율입니다. 원래 배지 테두리가 보이면 조금 늘리세요.")]
    public float outerDiameter = 1.26f;
    [Tooltip("배지 중심으로부터 원과 화살표를 함께 옮기는 화면 픽셀 오프셋입니다. X는 오른쪽, Y는 위쪽입니다.")]
    public Vector2 centerOffset;
    // JC | 2026-11-13 | 초상화와 점멸 원 사이를 고정 색으로 구분하며 기존 원 형상은 변경하지 않습니다.
    [Range(0f, 6f), Tooltip("원 안쪽 구분선 두께입니다. 최종 화면 픽셀 단위이며 0이면 숨깁니다. 원의 크기·중심은 유지하고 초상화 쪽으로 그립니다. 공용 프로필 저장 대상입니다.")]
    public float innerSeparatorWidth = 1f;
    [ColorUsage(false), Tooltip("점멸하지 않는 안쪽 구분선 색입니다. 불투명도는 아래 별도 항목에서 조절합니다. 공격·회복 배지가 공유합니다.")]
    public Color innerSeparatorColor = new Color(.07843138f, .12941177f, .23921569f, 1f);
    [Range(0f, 1f), Tooltip("안쪽 구분선 불투명도입니다. 0이면 숨김, 1이면 완전 불투명입니다. 기존 원의 점멸에는 영향을 주지 않습니다.")]
    public float innerSeparatorOpacity = 1f;
    [Tooltip("원의 왼쪽 기준에서 화살표만 추가로 이동하는 화면 픽셀 오프셋입니다.")]
    public Vector2 arrowOffset;
    [Min(.1f), Tooltip("현재 화살표 크기 대비 배율입니다. 1이면 기존 크기를 유지합니다.")]
    public float arrowScale = 1f;
    [Min(0), Tooltip("화살표 좌우 왕복 이동량입니다. 화면 픽셀 단위이며 0이면 정지합니다.")]
    public float arrowTravel = 6f;
    // JC | 2026-11-13 | 평면/무그림자 입체의 같은 외곽을 안쪽으로 두껍게 하며 전체 크기는 유지합니다.
    [Range(0f, 6f), Tooltip("평면·무그림자 입체 윤곽선 두께입니다. 화살표 기본 메시 단위이며 크기 배율과 함께 확대됩니다. 0이면 테두리 없음, 기본 3입니다.")]
    public float arrowOutlineWidth = 3f;
    [Range(0f, 8f), Tooltip("평면·무그림자 입체 모서리의 둥근 반경입니다. 기본 메시 단위이며 0이면 기존 뾰족한 모양입니다. 기존 그림자 스타일에는 적용하지 않습니다.")]
    public float arrowCornerRadius;
    [Min(.2f), Tooltip("배지와 화살표 윤곽선 점멸 및 왕복의 공통 주기(초)입니다.")]
    public float cycleSeconds = 1.6f;
    [Tooltip("치료·부활 대상 선택 중 해당 아군 턴 배지와 화살표를 표시합니다. 기본은 꺼짐입니다.")]
    public bool showHealingTargets;
    [Tooltip("공격 대상 배지와 화살표 윤곽선의 기본 색입니다. 본체 질감 색은 스타일별로 유지됩니다.")]
    public Color enemyColor = new Color(1f, .08f, .06f, 1f);
    [Tooltip("공격 대상 배지와 화살표 테두리가 점멸할 때의 밝은 색입니다.")]
    public Color enemyFlash = Color.white;
    [Tooltip("회복 대상 배지와 화살표 윤곽선의 기본 파란색입니다. 화살표 본체는 파란 입체/평면 질감을 사용합니다.")]
    public Color healingColor = new Color(.12f, .4f, 1f, 1f);
    [Tooltip("회복 대상 배지와 화살표 윤곽선의 점멸 색입니다. 흰색을 권장합니다.")]
    public Color healingFlash = Color.white;
    public JcBattleTurnSlotConfiguration Copy() => JsonUtility.FromJson<JcBattleTurnSlotConfiguration>(JsonUtility.ToJson(this));
    public void Validate()
    {
        innerDiameter = Mathf.Max(.1f, innerDiameter);
        outerDiameter = Mathf.Max(innerDiameter + .01f, outerDiameter);
        arrowScale = Mathf.Max(.1f, arrowScale); arrowTravel = Mathf.Max(0, arrowTravel);
        cycleSeconds = Mathf.Max(.2f, cycleSeconds);
        innerSeparatorWidth = Mathf.Clamp(innerSeparatorWidth, 0f, 6f);
        innerSeparatorOpacity = Mathf.Clamp01(innerSeparatorOpacity);
        arrowOutlineWidth = Mathf.Clamp(arrowOutlineWidth, 0, 6);
        arrowCornerRadius = Mathf.Clamp(arrowCornerRadius, 0, 8);
    }
}

// JC | 2026-11-13 | JC_BattleUI_VFX/TurnSlotHover에서 플레이 중 편집하고 명시적 저장 시에만 공유 자산을 갱신합니다.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class JcBattleTurnSlotSettings : MonoBehaviour
{
    [Tooltip("본게임·튜토리얼·테스트씬이 공유하는 기존 화살표 프로필입니다.")]
    public JcBattleHoverArrowProfile profile;
    [SerializeField, Tooltip("현재 씬의 임시 미리보기입니다. 저장 버튼을 누르기 전에는 공유 프로필을 변경하지 않습니다.")]
    private JcBattleTurnSlotConfiguration draft;
    public JcBattleTurnSlotConfiguration Draft { get { if (draft == null) LoadProfile(); return draft; } }
    private void OnEnable() => LoadProfile();
    private void OnValidate() { draft?.Validate(); }
    public void LoadProfile() => draft = profile != null ? profile.settings.Copy() : new JcBattleTurnSlotConfiguration();
}
