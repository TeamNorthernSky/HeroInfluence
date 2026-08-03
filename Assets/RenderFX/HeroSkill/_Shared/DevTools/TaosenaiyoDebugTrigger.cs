using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「쓰러지면 안돼」(Taosenaiyo, 부활).
    /// 개발용 임시 하네스: Y로 Taosenaiyo(부활 연출) 재생/우아한 정지 토글. (스킬 키 표준: 제작순 Q,W,E,R,T,Y)
    /// 대상 = Fighter_JC_Test(부활 대상 아군). 사망 판정 없음 — 순수 연출.
    /// ★실 게임에선 이 하네스 대신 스킬 시스템이 TaosenaiyoVfx.Play(caster, target) 호출.
    /// </summary>
    public class TaosenaiyoDebugTrigger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TaosenaiyoVfx taoPrefab;
        [Tooltip("시전자(손 소켓에서 유성 발사)")]
        [SerializeField] private Transform caster;
        [Tooltip("부활 대상 아군")]
        [SerializeField] private Transform target;

        [SerializeField] private KeyCode toggleKey = KeyCode.Y;

        private TaosenaiyoVfx _fx;

        private void Update()
        {
            if (Input.GetMouseButton(1)) return;   // 우클릭 홀드 = 카메라 조작 중 → 스킬 키 무시
            if (taoPrefab == null || !Input.GetKeyDown(toggleKey)) return;

            if (_fx == null) _fx = Instantiate(taoPrefab);   // 씬 루트(lossyScale 1)

            if (_fx.IsPlaying) _fx.StopGraceful();
            else _fx.Play(caster != null ? caster : transform, target != null ? target : transform);
        }
    }
}
