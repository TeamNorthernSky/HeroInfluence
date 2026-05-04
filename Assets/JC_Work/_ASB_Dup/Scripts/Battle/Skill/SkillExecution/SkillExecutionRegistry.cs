using System.Collections.Generic;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// skillIndex -> 커스텀 핸들러 매핑.
    /// 등록되지 않은 인덱스는 BattleManager 기본 실행 경로를 사용합니다.
    /// </summary>
    public static class SkillExecutionRegistry
    {
        private static readonly Dictionary<int, ISkillEffectHandler> Handlers = new Dictionary<int, ISkillEffectHandler>();
        private static bool s_initialized;

        static SkillExecutionRegistry()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 정적 초기화 보호: 1회만 등록.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (s_initialized)
            {
                return;
            }
            // 워리어
            s_initialized = true;
            Register(1010, new BleeedSkillHandler());
            Register(1020, new BleeedSkillHandler());
            Register(1030, new BleeedSkillHandler());
            Register(1040, new BleeedSkillHandler());
            Register(1050, new BleeedSkillHandler());
            Register(1060, new BleeedSkillHandler());
            Register(1060, new BleeedSkillHandler());
        }

        public static bool TryGetHandler(int skillIndex, out ISkillEffectHandler handler)
        {
            EnsureInitialized();
            return Handlers.TryGetValue(skillIndex, out handler);
        }

        private static void Register(int skillIndex, ISkillEffectHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            if (Handlers.ContainsKey(skillIndex))
            {
                UnityEngine.Debug.LogWarning($"[SkillExecutionRegistry] skillIndex {skillIndex} 중복 등록 무시.");
                return;
            }

            Handlers.Add(skillIndex, handler);
        }
    }
}
