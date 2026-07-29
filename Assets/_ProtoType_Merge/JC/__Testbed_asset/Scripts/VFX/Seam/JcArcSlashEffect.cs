using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 호 참격 단발 이펙트. ASB 연출 규약(ISkillEffectBehaviour) 구현체.
    ///
    /// 캐릭터가 무슨 모션을 하든 상관없이, 몸체 중앙 기준의 가상 궤도에 참격이 발생한다.
    /// 형상·스윕은 전부 셰이더(Testbed/Justice/ArcSlash)가 그리고,
    /// 이 컴포넌트는 「스폰 위치를 잡고, 파티클 1장을 터뜨리고, 수명 뒤 정리」만 맡는다.
    ///
    /// ★소켓에 자식으로 붙이지 않는다(소켓 lossyScale 약 200배 — 스케일 오염 차단).
    /// 스폰 시점의 위치만 한 번 읽고 고정된다. 빌보드 쿼드라 회전 계산도 필요 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcArcSlashEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Tooltip("참격을 그리는 파티클(빌보드 쿼드 1장 버스트). 비우면 자식에서 탐색.")]
        [SerializeField] private ParticleSystem slashParticle;

        [Tooltip("스폰 지점(소켓) 기준 오프셋(m). 소켓의 200배 스케일은 무시하고 회전 없이 더한다.")]
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;

        [Tooltip("정리까지의 추가 여유(초). 파티클 수명에 더해진다.")]
        [SerializeField, Min(0f)] private float extraLinger = 0.2f;

        // 프리셋 바인더가 밀어넣는 값
        public Vector3 SpawnOffset { get => spawnOffset; set => spawnOffset = value; }

        private void Awake()
        {
            if (slashParticle == null)
            {
                slashParticle = GetComponentInChildren<ParticleSystem>(true);
            }
        }

        public void Play(SkillEffectContext ctx)
        {
            // 어떤 경우에도 소켓 자식이 되지 않는다(스케일 오염 차단).
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            if (ctx != null)
            {
                Transform socket = ctx.SocketTransform;
                // ★TransformPoint 금지 — 소켓 스케일이 오프셋에 곱해진다. 위치에 그대로 더한다.
                transform.position = (socket != null ? socket.position : ctx.SpawnPosition) + spawnOffset;
            }

            if (slashParticle != null)
            {
                slashParticle.Clear(true);
                slashParticle.Play(true);

                float life = slashParticle.main.startLifetime.constantMax;
                Destroy(gameObject, life + extraLinger);
            }
            else
            {
                Destroy(gameObject, extraLinger);
            }
        }
    }
}
