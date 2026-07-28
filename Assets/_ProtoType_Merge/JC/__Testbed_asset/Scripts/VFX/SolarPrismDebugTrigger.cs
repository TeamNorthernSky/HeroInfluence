using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★루미나 직업 스킬 「솔라 프리즘」 개발용 임시 하네스.
    /// J 1회 = 차지 시작(프리즘 소환 → 호버·자전·꼭지점 플레어·바닥광).
    /// 차지 중 J = 발사(스캔한 적 더미 열로 유닛별 발진 → 개별 폭발 → 종료).
    /// 발사 진행 중 J = 무시. ★실 게임에선 스킬 시스템이 Play(caster,_) → Launch(적 열) 호출.
    /// </summary>
    public class SolarPrismDebugTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("솔라 프리즘 연출(SolarPrismVfx). 씬 인스턴스 또는 프리팹.")]
        [SerializeField] private SolarPrismVfx vfxPrefab;
        [Tooltip("시전자(전방에 프리즘이 소환되는 기준).")]
        [SerializeField] private Transform caster;

        [Header("Target Scan (테스트베드 전용)")]
        [Tooltip("발사 대상 더미 식별용 이름 부분일치 문자열.")]
        [SerializeField] private string dummyNameContains = "z_Fighter_JC_Test_Enemy";

        [Header("Keys")]
        [Tooltip("차지 시작 → (차지 중) 발사")]
        [SerializeField] private KeyCode playKey = KeyCode.J;

        private SolarPrismVfx _fx;

        private void Update()
        {
            if (Input.GetMouseButton(1)) return;   // 우클릭 홀드 = 카메라 조작 중 → 스킬 키 무시
            if (!Input.GetKeyDown(playKey)) return;
            if (vfxPrefab == null) return;

            if (_fx == null)
                _fx = vfxPrefab.gameObject.scene.IsValid() ? vfxPrefab : Instantiate(vfxPrefab);

            if (_fx.IsCharging) _fx.Launch(ScanTargets());
            else if (!_fx.IsPlaying) _fx.Play(caster != null ? caster : transform, caster != null ? caster : transform);
            // 발사 진행 중에는 무시
        }

        private System.Collections.Generic.List<Transform> ScanTargets()
        {
            var found = new System.Collections.Generic.List<Transform>();
            if (string.IsNullOrEmpty(dummyNameContains)) return found;
            Vector3 from = caster != null ? caster.position : transform.position;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.parent != null || t == caster) continue;
                if (!t.name.Contains(dummyNameContains)) continue;
                found.Add(t);
            }
            found.Sort((a, b) => Vector3.Distance(a.position, from).CompareTo(Vector3.Distance(b.position, from)));
            return found;
        }
    }
}
