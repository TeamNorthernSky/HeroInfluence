using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★루미나 직업 스킬 「체인 라이팅」 개발용 임시 하네스.
    /// O = 체인 라이팅 재생/중단 토글 (머즐 구체 → 본볼트 → 감전 → 연쇄 볼트 → 감전).
    /// 연쇄 대상 = primary 주변 반경(1셀 대각 포함 근사) 내 더미 중 **최근접 1명**(기획: 무조건 1명에게만 연쇄).
    /// ★실 게임에선 그리드를 아는 스킬 시스템이 ChainLightningVfx.Play(caster, target, chainTargets) 호출.
    /// </summary>
    public class ChainLightningDebugTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("체인 라이팅 연출(ChainLightningVfx). 씬 인스턴스 또는 프리팹.")]
        [SerializeField] private ChainLightningVfx vfxPrefab;
        [Tooltip("시전자(전방에 머즐 구체가 뜨는 기준).")]
        [SerializeField] private Transform caster;
        [Tooltip("주 대상(본볼트 도착점·연쇄 스캔 중심).")]
        [SerializeField] private Transform target;

        [Header("Chain Scan (테스트베드 전용)")]
        [Tooltip("연쇄 스캔 반경(m). 1셀 대각 포함 ≈ 셀 간격 × 1.5.")]
        [SerializeField] private float chainRadius = 2.2f;
        [Tooltip("더미 식별용 이름 부분일치 문자열.")]
        [SerializeField] private string dummyNameContains = "Justice";

        [Header("Keys")]
        [Tooltip("체인 라이팅 재생/중단 토글")]
        [SerializeField] private KeyCode playKey = KeyCode.O;

        private ChainLightningVfx _fx;

        private void Update()
        {
            if (Input.GetMouseButton(1)) return;   // 우클릭 홀드 = 카메라 조작 중 → 스킬 키 무시
            if (!Input.GetKeyDown(playKey)) return;
            if (vfxPrefab == null) return;

            if (_fx == null)
                _fx = vfxPrefab.gameObject.scene.IsValid() ? vfxPrefab : Instantiate(vfxPrefab);

            if (_fx.IsPlaying) { _fx.Stop(); return; }

            var primary = target != null ? target : transform;
            _fx.Play(caster != null ? caster : transform, primary, ScanChainTargets(primary));
        }

        private List<Transform> ScanChainTargets(Transform primary)
        {
            // 기획: 인접셀(대각 포함) 중 무조건 1명에게만 연쇄 → 반경 내 최근접 1명
            var found = new List<Transform>();
            if (string.IsNullOrEmpty(dummyNameContains)) return found;
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.parent != null) continue;                       // 루트만(더미 본체)
                if (t == primary || t == caster) continue;
                if (!t.name.Contains(dummyNameContains)) continue;
                float d = Vector3.Distance(t.position, primary.position);
                if (d > chainRadius || d >= bestDist) continue;
                best = t;
                bestDist = d;
            }
            if (best != null) found.Add(best);
            return found;
        }
    }
}
