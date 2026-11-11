using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-11 | 부모의 동일 프로필 초안을 사용하는 원근감 전용 Inspector 진입점입니다. 별도 카메라 갱신 루프/설정 복제본은 없습니다.
    [DisallowMultipleComponent]
    [AddComponentMenu("JC/전투/블랙불릿 원근감 조절")]
    public sealed class JcBlackBulletPerspectiveControls : MonoBehaviour
    {
        public JcBlackBulletCameraControls Controls => GetComponentInParent<JcBlackBulletCameraControls>();

        // JC | 2026-11-11 | Inspector 변경 직후 기존 카메라 담당자만 호출합니다. 해당 공격이 없으면 다음 공격부터 적용됩니다.
        public void Reapply() => Controls?.Reapply();
    }
}
