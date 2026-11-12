using UnityEngine;

// JC | 2026-11-12 | 본게임·튜토리얼·테스트씬의 화살표 스타일만 공유합니다. 크기·왕복 동작은 포함하지 않습니다.
public sealed class JcBattleHoverArrowProfile : ScriptableObject
{
    public const string ResourceName = "JC_BattleHoverArrowStyle";
    [Tooltip("켜면 기존 입체 색상·하이라이트·그림자, 끄면 평면 색상과 점멸 테두리입니다. 모든 전투씬이 공유하며 크기는 동일합니다.")]
    public bool useLegacyStyle;
}
