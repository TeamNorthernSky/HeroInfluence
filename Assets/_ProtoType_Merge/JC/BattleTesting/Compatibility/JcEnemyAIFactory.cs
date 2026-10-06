// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/AI/EnemyAIFactory.cs
// 원본 객체: EnemyAIFactory -> JcEnemyAIFactory
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
namespace EnemyAI
{
    public static class JcEnemyAIFactory
    {
        // 원본 함수 대응: EnemyAIFactory.CreateAI (Assets/_ProtoType_Merge/ASB/Scripts/Battle/AI/EnemyAIFactory.cs)
        public static IEnemyAI CreateAI(int aiIndex)
        {
            switch (aiIndex)
            {
                case 20001:
                    return new JcEAI_20001();
                case 20002:
                    return new JcEAI_20002();
                case 20003:
                    return new JcEAI_20003();
                // 포탑(2턴 충전) + 4구역 율리아 진영(응축/자폭). 능력개방·소환·페이즈는 미룸.
                case 20004:
                    return new JcEAI_20004();
                case 40001:
                    return new JcEAI_40001();
                case 40002:
                    return new JcEAI_40002();
                case 40003:
                    return new JcEAI_40003();
                case 40005:
                    return new JcEAI_40005();
                // 보스/미니언 (구현지시서: 보스유닛_소환_창구스킬_페이즈AI). index는 데이터 스키마에 맞게 조정.
                case 20101:
                    return new EAI_20101();
                case 20102:
                    return new EAI_20102();
                default:
                    return new JcEAI_20001();
            }
        }

        // 원본 함수 대응: EnemyAIFactory.CreateAI (Assets/_ProtoType_Merge/ASB/Scripts/Battle/AI/EnemyAIFactory.cs)

        public static IEnemyAI CreateAI(string aiType)
        {
            string safe = string.IsNullOrWhiteSpace(aiType) ? string.Empty : aiType.Trim();
            if (int.TryParse(safe, out int aiIndex))
            {
                return CreateAI(aiIndex);
            }

            switch (safe)
            {
                case "JcEAI_20001":
                case "20001":
                case "Aggressive":
                    return new JcEAI_20001();
                case "JcEAI_20002":
                case "20002":
                    return new JcEAI_20002();
                case "JcEAI_20003":
                case "20003":
                    return new JcEAI_20003();
                default:
                    return new JcEAI_20001();
            }
        }
    }
}
