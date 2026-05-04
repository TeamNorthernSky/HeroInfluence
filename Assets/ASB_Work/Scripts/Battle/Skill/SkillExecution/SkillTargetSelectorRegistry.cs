using System.Collections.Generic;

namespace ASB.Work.Battle.SkillExecution
{
    public static class SkillTargetSelectorRegistry
    {
        private static readonly Dictionary<int, ITargetSelector> Selectors = new Dictionary<int, ITargetSelector>();

        public static ITargetSelector GetSelector(int skillIndex)
        {
            if (Selectors.TryGetValue(skillIndex, out ITargetSelector selector) && selector != null)
            {
                return selector;
            }

            return DefaultTargetSelector.Instance;
        }

        public static void Register(int skillIndex, ITargetSelector selector)
        {
            if (selector == null)
            {
                return;
            }

            Selectors[skillIndex] = selector;
        }
    }
}
