using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「우리 다 같이 힘내자」(LetsFightingLove).
    /// 개발용 임시 하네스: T로 LetsFightingLove 재생/우아한 정지 토글. (스킬 키 표준: 제작순 Q,W,E,R,T)
    /// 대상 = 아군 표준 Blaster_JC_Test. 연쇄 후보 = chainCandidates 배열(시전자·본 대상은 내부 제외).
    /// ★실 게임에선 이 하네스 대신 스킬 시스템이 SetChainCandidates + Play(caster, target) 호출.
    /// </summary>
    public class LetsFightingLoveDebugTrigger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LetsFightingLoveVfx lflPrefab;
        [Tooltip("시전자(양손 소켓에서 차징)")]
        [SerializeField] private Transform caster;
        [Tooltip("본 대상(합체 투사체가 날아가는 아군)")]
        [SerializeField] private Transform target;
        [Tooltip("연쇄 후보(씬의 아군 전부 넣으면 됨 — 시전자/본 대상은 자동 제외)")]
        [SerializeField] private Transform[] chainCandidates;

        [SerializeField] private KeyCode toggleKey = KeyCode.T;

        private LetsFightingLoveVfx _fx;

        private void Update()
        {
            if (Input.GetMouseButton(1)) return;   // 우클릭 홀드 = 카메라 조작 중 → 스킬 키 무시
            if (lflPrefab == null || !Input.GetKeyDown(toggleKey)) return;

            if (_fx == null) _fx = Instantiate(lflPrefab);   // 씬 루트(lossyScale 1)

            if (_fx.IsPlaying) _fx.StopGraceful();
            else
            {
                _fx.SetChainCandidates(chainCandidates);
                _fx.Play(caster != null ? caster : transform, target != null ? target : transform);
            }
        }
    }
}
