using UnityEngine;
namespace JC.BattleTesting
{
    // JC | 2026-11-09 | 은행·보스의 하위 광원도 동일한 편집기를 사용하며 주인의 한 초안만 수정합니다.
    public sealed class JcIndoorLightControl : MonoBehaviour
    {
        public JcBankEnvironment Bank { get; private set; }
        public JcBossEnvironment Boss { get; private set; }
        public int Index { get; private set; }
        public JcIndoorEnvironmentProfile Draft => Bank ? (JcIndoorEnvironmentProfile)Bank.Draft : Boss ? Boss.Draft : null;
        public JcIndoorEnvironmentProfile Source => Bank ? (JcIndoorEnvironmentProfile)Bank.Source : Boss ? Boss.Source : null;
        public void Bind(JcBankEnvironment owner,int index) { Bank=owner; Index=index; }
        public void Bind(JcBossEnvironment owner,int index) { Boss=owner; Index=index; }
        public void Apply() { if(Bank)Bank.ApplyDraft(); if(Boss)Boss.ApplyDraft(); }
    }
}
