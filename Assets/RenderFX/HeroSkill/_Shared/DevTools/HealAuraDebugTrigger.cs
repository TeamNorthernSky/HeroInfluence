using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「아픈 거 다 날아가라」(HealSkill).
    /// 개발용 임시 하네스: W로 힐 오라를 켜고/끈다(토글). (스킬 키 표준: W=힐 오라)
    /// 궤도 중심 = center(지정 Transform)의 위치 + yOffset. 지정 안 하면 자기 위치.
    /// ★실 게임에선 이 하네스 대신 ProjectileVfx.OnFinished(착지) → HealOrbitVfx.Play(착지점) 으로 연결.
    ///   (E 요소들이 다 완성되면 통합 단계에서 배선)
    /// </summary>
    public class HealAuraDebugTrigger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HealOrbitVfx auraPrefab;
        [Tooltip("궤도 중심으로 쓸 대상(더미/마커). 비우면 이 오브젝트 위치.")]
        [SerializeField] private Transform center;
        [SerializeField] private float centerYOffset = 0f;

        [SerializeField] private KeyCode toggleKey = KeyCode.W;

        private HealOrbitVfx _aura;

        private Vector3 CenterPos()
        {
            Vector3 p = center != null ? center.position : transform.position;
            p.y += centerYOffset;
            return p;
        }

        private void Update()
        {
            if (auraPrefab == null) return;
            if (Input.GetMouseButton(1)) return;   // 우클릭 홀드 = 카메라 조작 중 → 스킬 키 무시
            if (!Input.GetKeyDown(toggleKey)) return;

            if (_aura == null) _aura = Instantiate(auraPrefab);   // 씬 루트(lossyScale 1)

            if (_aura.IsPlaying) _aura.StopGraceful();   // 페이드아웃 거쳐 종료
            else _aura.Play(CenterPos());
        }
    }
}
