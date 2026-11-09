using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-05 | 조명별 오브젝트는 환경 조절기의 동일 초안을 참조합니다. 별도 프로필/폴백을 만들지 않습니다.
    [DisallowMultipleComponent, AddComponentMenu("JC/은행/은행 조명 조절기")]
    public sealed class JcBankLightControl : MonoBehaviour
    {
        [SerializeField, HideInInspector] JcBankEnvironment owner;
        [SerializeField, HideInInspector] int index;
        public JcBankEnvironment Owner => owner;
        public int Index => index;
        public bool IsBound => owner && owner.Draft && index >= 0 && index < owner.Draft.lights.Count;
        public void Bind(JcBankEnvironment environment, int lightIndex) { owner = environment; index = lightIndex; }
        public void ReloadLight()
        {
            if (!IsBound || !owner.Source || index >= owner.Source.lights.Count) return;
            // JC | 2026-11-05 | 이 조명만 복원해 다른 조명의 미저장 조절을 보존합니다.
            owner.Draft.lights[index] = JsonUtility.FromJson<JcBankEnvironmentProfile.CeilingLight>(JsonUtility.ToJson(owner.Source.lights[index]));
            owner.ApplyDraft();
        }
    }
}
