using UnityEngine;

// JC | 2026-11-12 | 6개 전투씬에서 공유하는 확대·피봇 저장값입니다. 조절 초안은 기존 씬 설정 컴포넌트가 별도로 보관합니다.
public sealed class BattleSkillButtonMotionProfile : ScriptableObject
{
    public const string ResourceName = "JC_BattleSkillButtonMotion";
    [HideInInspector] public float hoverScale = 1.04f;
    // JC | 2026-11-12 | 기본 너비는 현재 크기 1에서 0.6까지 줄이며 비확대 왼쪽 경계를 유지합니다.
    [HideInInspector] public float widthRatio = 1f;
    // JC | 2026-11-12 | 좌우 피봇도 공유 저장하며 기존 프로필은 중앙값으로 시작합니다.
    [HideInInspector] public float pivotX = .5f;
    [HideInInspector] public float pivotY = .5f;

    // JC | 2026-11-12 | 기획 원본 YT씬과 카탈로그, 시스템 버튼의 기존 확대 동작에는 적용하지 않습니다.
    public static bool SupportsScene(string sceneName) =>
        sceneName == "TmpBattleScene" || sceneName == "TutorialBattleScene" ||
        sceneName == "JC_Main12BattleTestScene" || sceneName == "JC_TutorialFirstBattleTestScene" ||
        sceneName == "JC_BossTestScene" || sceneName == "BankTestScene";
}
