namespace EnemyAI
{
    public static class EnemyAIFactory
    {
        public static IEnemyAI CreateAI(int aiIndex)
        {
            switch (aiIndex)
            {
                case 20001:
                    return new EAI_20001();
                case 20002:
                    return new EAI_20002();
                case 20003:
                    return new EAI_20003();
                default:
                    return new EAI_20001();
            }
        }

        public static IEnemyAI CreateAI(string aiType)
        {
            string safe = string.IsNullOrWhiteSpace(aiType) ? string.Empty : aiType.Trim();
            if (int.TryParse(safe, out int aiIndex))
            {
                return CreateAI(aiIndex);
            }

            switch (safe)
            {
                case "EAI_20001":
                case "20001":
                case "Aggressive":
                    return new EAI_20001();
                case "EAI_20002":
                case "20002":
                    return new EAI_20002();
                case "EAI_20003":
                case "20003":
                    return new EAI_20003();
                default:
                    return new EAI_20001();
            }
        }
    }
}
