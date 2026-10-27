using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「널 위해 준비했어」(PawForYou) 계열.
    /// 개발용 임시 하네스: PawForYou 계열 2스킬 재생/우아한 정지 토글.
    /// E = PawForYou(금빛 발+녹색 광선, 빛기둥 블링크 워프)
    /// R = PawForYouMistake(은백 발+노랑 광선, 잔상 스트릭 워프)
    /// 대상 = 적 더미 z_Fighter_JC_Test_Enemy. 변형 색·워프 방식은 각 프리팹에 베이크(프리셋 참조).
    /// ★실 게임에선 이 하네스 대신 스킬 시스템이 PawForYouVfx.Play(caster, target) 호출.
    /// </summary>
    public class PawForYouDebugTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("PawForYou 프리팹(금빛+녹색, 빛기둥 블링크)")]
        [SerializeField] private PawForYouVfx pawForYouPrefab;
        [Tooltip("PawForYouMistake 프리팹(은백+노랑, 잔상 스트릭)")]
        [SerializeField] private PawForYouVfx pawMistakePrefab;
        [Tooltip("시전자(발이 우상단에 뜨는 기준)")]
        [SerializeField] private Transform caster;
        [Tooltip("대상(머리 위 워프·광선 피격)")]
        [SerializeField] private Transform target;

        [Header("Keys")]
        [Tooltip("PawForYou 재생/정지 토글")]
        [SerializeField] private KeyCode pawForYouKey = KeyCode.E;
        [Tooltip("PawForYouMistake 재생/정지 토글")]
        [SerializeField] private KeyCode pawMistakeKey = KeyCode.R;

        private PawForYouVfx _fxPaw, _fxMistake;

        private void Update()
        {
            if (Input.GetMouseButton(1)) return;   // 우클릭 홀드 = 카메라 조작 중 → 스킬 키 무시
            if (Input.GetKeyDown(pawForYouKey)) Toggle(ref _fxPaw, pawForYouPrefab);
            if (Input.GetKeyDown(pawMistakeKey)) Toggle(ref _fxMistake, pawMistakePrefab);
        }

        private void Toggle(ref PawForYouVfx fx, PawForYouVfx prefab)
        {
            if (prefab == null) return;
            if (fx == null) fx = Instantiate(prefab);   // 씬 루트(lossyScale 1)

            if (fx.IsPlaying) fx.StopGraceful();
            else fx.Play(caster != null ? caster : transform, target != null ? target : transform);
        }
    }
}
