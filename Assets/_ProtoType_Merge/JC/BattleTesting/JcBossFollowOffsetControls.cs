using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-17 | 부모 보스 카메라의 동일 초안을 편집하는 Inspector 진입점입니다. 설정 사본/추적 루프를 추가하지 않습니다.
    [DisallowMultipleComponent]
    [AddComponentMenu("JC/전투/보스 추적 오프셋 조절")]
    public sealed class JcBossFollowOffsetControls : MonoBehaviour
    {
        public JcBossCamera Controls => GetComponentInParent<JcBossCamera>();
    }
}
