using System.Collections.Generic;

namespace ASB.Work.Battle.SkillExecution
{
    public static class SkillTargetSelectorRegistry
    {
        // §5-9 skillKey(string) 기반. 현재 등록 없음(항상 기본 셀렉터).
        private static readonly Dictionary<string, ITargetSelector> Selectors = new Dictionary<string, ITargetSelector>();

        public static ITargetSelector GetSelector(string skillKey)
        {
            if (!string.IsNullOrEmpty(skillKey)
                && Selectors.TryGetValue(skillKey, out ITargetSelector selector)
                && selector != null)
            {
                return selector;
            }

            return DefaultTargetSelector.Instance;
        }

        public static void Register(string skillKey, ITargetSelector selector)
        {
            if (selector == null || string.IsNullOrEmpty(skillKey))
            {
                return;
            }

            Selectors[skillKey] = selector;
        }
    }
}
