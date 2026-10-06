// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillTargetSelectorRegistry.cs
// 원본 객체: SkillTargetSelectorRegistry -> JcSkillTargetSelectorRegistry
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System.Collections.Generic;

namespace ASB.Work.Battle.SkillExecution
{
    public static class JcSkillTargetSelectorRegistry
    {
        // §5-9 skillKey(string) 기반. 현재 등록 없음(항상 기본 셀렉터).
        private static readonly Dictionary<string, JcITargetSelector> Selectors = new Dictionary<string, JcITargetSelector>();

        // 원본 함수 대응: SkillTargetSelectorRegistry.GetSelector (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillTargetSelectorRegistry.cs)

        public static JcITargetSelector GetSelector(string skillKey)
        {
            if (!string.IsNullOrEmpty(skillKey)
                && Selectors.TryGetValue(skillKey, out JcITargetSelector selector)
                && selector != null)
            {
                return selector;
            }

            return JcDefaultTargetSelector.Instance;
        }

        // 원본 함수 대응: SkillTargetSelectorRegistry.Register (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillTargetSelectorRegistry.cs)

        public static void Register(string skillKey, JcITargetSelector selector)
        {
            if (selector == null || string.IsNullOrEmpty(skillKey))
            {
                return;
            }

            Selectors[skillKey] = selector;
        }
    }
}
