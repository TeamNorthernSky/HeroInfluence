using System;
using UnityEngine;

namespace JC.BattleTesting
{
    [CreateAssetMenu(menuName = "JC/전투/보스 무대 배경")]
    public sealed class JcBossBattleBackground : ScriptableObject
    {
        [Tooltip("전투 판정 없이 무대·바닥·기둥 외형만 포함하는 프리팹입니다.")]
        public GameObject visualPrefab;
        [Tooltip("보스전 이벤트 구역 번호입니다.")] public int zoneId = 2;
        [Tooltip("이 배경을 사용할 전투 키입니다. Start_ 접두어는 자동으로 제외합니다.")]
        public string battleKey = "BE490";

        public bool Matches(int zone, string key)
        {
            key = key?.Trim();
            if (key != null && key.StartsWith("Start_", StringComparison.Ordinal)) key = key.Substring(6);
            return zone == zoneId && !string.IsNullOrWhiteSpace(key) && key == battleKey;
        }
        public bool Matches(CombatContext context) => context != null && !context.IsTutorial &&
            Matches(context.EventBattle?.ZoneId ?? -1, context.EventBattle?.BattleKey);
        public bool Matches(JcEnemyPreset preset) => preset != null &&
            (!preset.IsEventBattle || Matches(preset.eventZoneId, preset.eventBattleKey));
        public static JcBossBattleBackground Load() => Resources.Load<JcBossBattleBackground>("JC_BossBattleBackground");
    }
}
