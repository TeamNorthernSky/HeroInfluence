using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 타격 이펙트 진입점. ASB의 OneShotEffectBehaviour를 대체한다.
    ///
    /// 대체하는 이유: 섬광을 **시전자→타깃 방향**으로 눕혀야 하는데,
    /// OneShotEffectBehaviour는 파티클을 재생만 하고 방향 개념이 없다.
    /// 여기서는 Cue가 넘겨주는 SkillEffectContext의 Caster/PrimaryTarget으로 방향을 잡는다.
    ///
    /// 방향 구현: 섬광은 빌보드라 항상 카메라를 향한다. 그래서 월드 회전으로 눕히면
    /// 카메라 각도에 따라 옆면(edge-on)이 되어 사라질 수 있다.
    /// 대신 **화면 공간에서의 각도**를 구해 파티클의 startRotation으로 준다.
    /// 항상 정면을 향하면서도 화면상 방향은 타깃 쪽을 가리킨다.
    ///
    /// 위치: 스폰 지점(소켓) 기준으로 오프셋을 **빔 로컬 축**으로 적용한다.
    ///   z = 타깃 방향(전방) / x = 우측 / y = 월드 상방
    /// </summary>
    [DisallowMultipleComponent]
    public class JcImpactFlashDirector : MonoBehaviour, ISkillEffectBehaviour
    {
        [Tooltip("방향을 적용할 섬광 자식 이름.")]
        [SerializeField] private string flashChildName = "Flash";

        [Tooltip("스폰 지점 기준 오프셋. z=타깃 방향(전방), x=우측, y=상방.")]
        [SerializeField] private Vector3 offset = Vector3.zero;

        [Tooltip("화면 회전 부호를 뒤집는다. 섬광이 반대로 누우면 켠다.")]
        [SerializeField] private bool flipRotation;

        [Tooltip("방향 계산에서 높이차를 무시한다(수평면 기준).")]
        [SerializeField] private bool flattenDirection = true;

        [Tooltip("파티클이 모두 끝난 뒤 정리까지의 여유(초).")]
        [SerializeField, Min(0f)] private float extraLifetime = 0.3f;

        [SerializeField] private bool logLifecycle;

        public Vector3 Offset { get => offset; set => offset = value; }
        public bool FlipRotation { get => flipRotation; set => flipRotation = value; }

        private bool _played;

        private void Start()
        {
            // 피격 프리팹을 직접 생성하는 경로에서도 재생과 수명 정리를 시작합니다.
            if (!_played) Play(null);
        }

        public void Play(SkillEffectContext ctx)
        {
            _played = true;
            Vector3 spawn = ctx != null ? ctx.SpawnPosition : transform.position;

            // 시전자 → 타깃 방향
            Vector3 dir = transform.forward;
            if (ctx != null && ctx.Caster != null)
            {
                Vector3 from = ctx.Caster.transform.position;
                Vector3 to = ctx.PrimaryTarget != null ? ctx.PrimaryTarget.transform.position : ctx.TargetPosition;
                Vector3 d = to - from;
                if (flattenDirection) d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) dir = d.normalized;
            }

            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            if (right.sqrMagnitude < 1e-6f) right = transform.right;

            transform.position = spawn + dir * offset.z + right * offset.x + Vector3.up * offset.y;

            OrientFlash(dir);

            float longest = 0f;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
                var main = ps.main;
                float life = main.startLifetime.constantMax + main.duration;
                if (life > longest) longest = life;
            }

            if (logLifecycle)
            {
                Debug.Log($"[JcImpactFlashDirector] dir={dir:F2} pos={transform.position:F2} life={longest:F2}", this);
            }

            Destroy(gameObject, longest + extraLifetime);
        }

        /// <summary>
        /// 빌보드 섬광의 화면상 길이 축을 타깃 방향에 맞춘다.
        /// 월드 회전이 아니라 startRotation을 쓰는 이유는 클래스 주석 참조.
        /// </summary>
        private void OrientFlash(Vector3 worldDir)
        {
            Transform flash = transform.Find(flashChildName);
            var ps = flash != null ? flash.GetComponent<ParticleSystem>() : null;
            if (ps == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 p0 = cam.WorldToScreenPoint(transform.position);
            Vector3 p1 = cam.WorldToScreenPoint(transform.position + worldDir);
            Vector2 screenDir = (Vector2)(p1 - p0);
            if (screenDir.sqrMagnitude < 1e-6f) return;

            float angle = Mathf.Atan2(screenDir.y, screenDir.x);
            var main = ps.main;
            main.startRotation = flipRotation ? angle : -angle;   // 파티클 회전은 시계 방향 기준
        }
    }
}
