using System.Collections;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>기존 정적 투사체·감전 재료에 독립 재생/종료 계약을 제공합니다.</summary>
    public sealed class JcSimpleVisualPart : VfxEffect, ISkillEffectBehaviour, ISkillEffectHandle
    {
        public enum Motion { Hold, Projectile, Shock }
        [Tooltip("Hold는 생성 위치 유지, Projectile은 대상까지 이동, Shock는 감전 부품입니다.")]
        [SerializeField] private Motion motion;
        [Tooltip("표시할 자식 루트입니다. 프리팹 원본 재질은 수정하지 않습니다.")]
        [SerializeField] private GameObject visual;
        [Tooltip("독립 감전 재료. Shock일 때 사용합니다.")]
        [SerializeField] private LightningShock shock;
        [Tooltip("부품 유지/이동 시간(1배속 초)입니다.")]
        [SerializeField, Min(.01f)] private float duration = .5f;
        [Tooltip("출발 기준점에서 더할 월드 오프셋(m)입니다.")]
        [SerializeField] private Vector3 originOffset;
        [Tooltip("도착 기준점에서 더할 월드 오프셋(m)입니다.")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0,.8f,0);
        [Tooltip("포물선 높이(m)입니다. 0이면 직선입니다.")]
        [SerializeField] private float arcHeight;
        [Tooltip("전투가 Transform 이동을 직접 구동하는 투사체이면 켭니다. 생성 즉시 표시합니다.")]
        [SerializeField] private bool externallyDriven;
        private void Awake() { if (visual != null) visual.SetActive(externallyDriven); }
        public override void Play() => Play(transform, transform);
        public override void Play(Transform origin, Transform target)
        {
            Stop();
            if (origin == null || target == null) return;
            StartCoroutine(Run(origin.position + originOffset, target));
        }
        private IEnumerator Run(Vector3 start, Transform target)
        {
            IsPlaying = true;
            if (visual != null) visual.SetActive(true);
            if (motion == Motion.Shock && shock != null)
                shock.Init(target, targetOffset, 1.7f, duration / PlaybackSpeed, .25f / PlaybackSpeed, 18f);
            Vector3 end = target.position + targetOffset;
            for (float t=0; t<duration; t+=EffectDeltaTime)
            {
                float u=Mathf.Clamp01(t/duration);
                if (motion == Motion.Projectile)
                    transform.position=Vector3.Lerp(start,end,u)+Vector3.up*(4*arcHeight*u*(1-u));
                else if (motion == Motion.Hold) transform.position=start;
                yield return null;
            }
            if (visual != null) visual.SetActive(false);
            IsPlaying = false; RaiseFinished();
        }
        public override void Stop() { StopAllCoroutines(); if (visual != null) visual.SetActive(false); IsPlaying=false; }
        public void Play(SkillEffectContext ctx)
        {
            PlaybackSpeed = ctx.PlaybackSpeed;
            Play(ctx.SocketTransform != null ? ctx.SocketTransform : ctx.Caster.transform,
                ctx.PrimaryTarget != null ? ctx.PrimaryTarget.transform : ctx.Caster.transform);
        }
        public bool Signal(SkillEffectContext ctx) { Stop(); return true; }
        private void OnDisable() => Stop();
    }
}
