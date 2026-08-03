using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★루미나 직업 스킬 「프리즘 익스플로전」 개발용 임시 하네스.
    /// K 1회 = 차지 시작(대형 수정 소환). 차지 중 K = 발사 시퀀스(상승·조준 → 비행 → 진형 중앙 대폭발).
    /// 시퀀스 진행 중 K = 무시. ★실 게임에선 스킬 시스템이 Play(caster,_) → Launch(적 전체) 호출.
    /// </summary>
    public class PrismExplosionDebugTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("프리즘 익스플로전 연출(PrismExplosionVfx). 씬 인스턴스 또는 프리팹.")]
        [SerializeField] private PrismExplosionVfx vfxPrefab;
        [Tooltip("시전자(전방에 수정이 소환되는 기준).")]
        [SerializeField] private Transform caster;

        [Header("Target Scan (테스트베드 전용)")]
        [Tooltip("적 더미 식별용 이름 부분일치 문자열. 전원 평균 위치 = 진형 중앙 = 탄착점.")]
        [SerializeField] private string dummyNameContains = "z_Fighter_JC_Test_Enemy";

        [Header("Keys")]
        [Tooltip("차지 시작 → (차지 중) 발사 시퀀스")]
        [SerializeField] private KeyCode playKey = KeyCode.K;

        private PrismExplosionVfx _fx;

        private void Update()
        {
            if (Input.GetMouseButton(1)) return;   // 우클릭 홀드 = 카메라 조작 중 → 스킬 키 무시
            if (!Input.GetKeyDown(playKey)) return;
            if (vfxPrefab == null) return;

            if (_fx == null)
                _fx = vfxPrefab.gameObject.scene.IsValid() ? vfxPrefab : Instantiate(vfxPrefab);

            if (_fx.IsCharging) _fx.Launch(ScanTargets());
            else if (!_fx.IsPlaying) _fx.Play(caster != null ? caster : transform, caster != null ? caster : transform);
            // 시퀀스 진행 중에는 무시
        }

        private System.Collections.Generic.List<Transform> ScanTargets()
        {
            var found = new System.Collections.Generic.List<Transform>();
            if (string.IsNullOrEmpty(dummyNameContains)) return found;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.parent != null || t == caster) continue;
                if (!t.name.Contains(dummyNameContains)) continue;
                found.Add(t);
            }
            return found;
        }
    }
}
