using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★루미나 직업 스킬 「플레어 봄」 개발용 임시 하네스.
    /// U = 플레어 봄 재생/중단 토글 (차징 오브 → 포물선 발사 → 탄착 버스트).
    /// 대상 = 적 더미 z_Fighter_JC_Test_Enemy. 오발(리스크) 검증은 target에 아군을 꽂아 재생.
    /// ★실 게임에선 이 하네스 대신 스킬 시스템이 FlareBombVfx.Play(caster, target) 호출.
    /// </summary>
    public class FlareBombDebugTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("플레어 봄 연출(FlareBombVfx). 씬 인스턴스 또는 프리팹.")]
        [SerializeField] private FlareBombVfx vfxPrefab;
        [Tooltip("시전자(좌측 상단에 오브가 소환되는 기준).")]
        [SerializeField] private Transform caster;
        [Tooltip("대상(탄착점). 오발 검증 시 아군으로 교체.")]
        [SerializeField] private Transform target;

        [Header("Keys")]
        [Tooltip("플레어 봄 재생/중단 토글")]
        [SerializeField] private KeyCode playKey = KeyCode.U;

        private FlareBombVfx _fx;

        private void Update()
        {
            if (Input.GetMouseButton(1)) return;   // 우클릭 홀드 = 카메라 조작 중 → 스킬 키 무시
            if (!Input.GetKeyDown(playKey)) return;
            if (vfxPrefab == null) return;

            if (_fx == null)
                _fx = vfxPrefab.gameObject.scene.IsValid() ? vfxPrefab : Instantiate(vfxPrefab);   // 씬 인스턴스면 그대로 사용

            if (_fx.IsPlaying) _fx.Stop();
            else _fx.Play(caster != null ? caster : transform, target != null ? target : transform);
        }
    }
}
