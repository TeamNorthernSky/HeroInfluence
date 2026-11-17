using System;
using UnityEngine;
namespace JC.BattleTesting
{
    // JC | 2026-11-17 | 기존 전문 프로필을 네 시간대별로 묶으며 은행과 보스의 원본 연결을 분리합니다.
    [CreateAssetMenu(menuName="JC/전투/실내 시간대 프로필")]
    public sealed class JcIndoorPeriodProfiles : ScriptableObject
    {
        public static readonly string[] Names = { "아침", "오후", "저녁", "새벽" };
        [Serializable] public sealed class Period
        {
            public JcBossEnvironmentProfile bossLighting;
            public JcBankEnvironmentProfile bankLighting;
            public Material glass;
            public JcBossSurfaceProfile surfaces;
            public JcBankMaterialProfile bankMaterials;
            public JcBankToonProfile toon;
            public JC.Environment.JcContactShadingProfile contact;
        }
        public Period[] periods = new Period[4];
        public Period Get(int index) => periods != null && index >= 0 && index < periods.Length ? periods[index] : null;
    }
}
