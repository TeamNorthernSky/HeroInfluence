using System;
using UnityEngine;
namespace JC.BattleTesting
{
    // JC | 2026-11-17 | 기존 전문 프로필을 네 시간대별로 묶으며 은행과 보스의 원본 연결을 분리합니다.
    [CreateAssetMenu(menuName="JC/전투/실내 시간대 프로필")]
    public sealed class JcIndoorPeriodProfiles : ScriptableObject
    {
        // JC | 2026-11-18 | 게임 시각과 별개인 미술 연출 구간 이름을 공통으로 표시합니다.
        public static readonly string[] Names = { "오전", "오후", "해질녘", "야간" };
        [InspectorName("보스 공통 실내 표면"), Tooltip("유리 외 실내 색상·반사·줄눈·재질 교체를 모든 시간대에 공통 적용합니다.")]
        public JcBossSurfaceProfile commonBossSurfaces;
        [Serializable] public sealed class Period
        {
            public JcBossEnvironmentProfile bossLighting;
            public JcBankEnvironmentProfile bankLighting;
            public Material glass;

            public JcBankMaterialProfile bankMaterials;
            public JcBankToonProfile toon;
            public JC.Environment.JcContactShadingProfile contact;
        }
        public Period[] periods = new Period[4];
        public Period Get(int index) => periods != null && index >= 0 && index < periods.Length ? periods[index] : null;
    }
}
