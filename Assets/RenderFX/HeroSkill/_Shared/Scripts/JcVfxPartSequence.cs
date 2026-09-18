using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>독립 부품을 시간표로 조립합니다. 대상 선정과 전투 수치는 호출자가 소유합니다.</summary>
    public sealed class JcVfxPartSequence : VfxEffect
    {
        public event Action Impacted;
        public enum Anchor { Caster, Target, PawCaster, PawTarget }
        public enum Timing { Fixed, ShotLaunch, ShotImpact, PawDeparture, PawArrival }
        public Transform MuzzleSocket { get; set; }
        [Serializable] public sealed class Step
        {
            [Tooltip("독립 재생할 VFX 부품 프리팹입니다.")] public VfxEffect prefab;
            [Tooltip("이 조립의 시작부터 부품 호출까지의 시간(1배속 초)입니다.")]
            [Min(0)] public float delay;
            [Tooltip("Fixed는 아래 지연, 나머지는 연결 프리셋의 시간에 지연을 더합니다.")] public Timing timing;
            [Tooltip("사격의 발사·착탄 시간 기준입니다.")] public KAimShotPreset shotTiming;
            [Tooltip("발의 등장·유지·워프 시간 기준입니다.")] public PawMasterPreset pawTiming;
            [Tooltip("부품의 생성 위치와 출발 기준입니다.")] public Anchor anchor;
            [Tooltip("기준점에서 더할 월드 오프셋(m)입니다.")] public Vector3 offset;
            [Tooltip("0이면 자연 종료 대기, 양수이면 이 시간(1배속 초) 뒤 강제 중단합니다.")]
            [Min(0)] public float lifetime;
        }
        [Tooltip("기본/강화 조립마다 독립된 부품과 호출 시간을 저장합니다.")]
        [SerializeField] private Step[] steps = Array.Empty<Step>();
        private readonly List<VfxEffect> running = new List<VfxEffect>();
        private IReadOnlyList<VfxTarget> targets;
        private int pending;
        private bool impacted;
        public override void SetTargets(IReadOnlyList<VfxTarget> values) => targets = values;
        public override void Play(Transform origin, Transform target)
        {
            Stop();
            if (origin == null || target == null) return;
            impacted = false;
            IsPlaying = true;
            pending = steps.Length;
            foreach (var step in steps) StartCoroutine(RunPart(step, origin, target));
            StartCoroutine(WaitForParts());
        }
        private IEnumerator Delay(float seconds)
        {
            for (float t = 0; t < seconds; t += EffectDeltaTime) yield return null;
        }
        private IEnumerator RunPart(Step step, Transform origin, Transform target)
        {
            if (step.prefab == null) { pending--; yield break; }
            yield return Delay(ResolveDelay(step));
            if (origin == null || target == null) { pending--; yield break; }
            var anchor = step.anchor == Anchor.Caster || step.anchor == Anchor.PawCaster ? origin : target;
            var position = anchor.position;
            if (step.prefab is PawForYouVfx pawPart && pawPart.PawPreset != null)
            {
                var placement = pawPart.PawPreset.TransformSource;
                if (step.anchor == Anchor.PawCaster)
                    position = JcVfxPlacementPreset.Resolve(origin, placement.spawnSocketName, placement.spawnOffset);
                if (step.anchor == Anchor.PawTarget)
                    position = JcVfxPlacementPreset.ResolveWorld(target, placement.headSocketName, placement.headOffset);
            }
            var effect = Instantiate(step.prefab, position + step.offset, anchor.rotation);
            running.Add(effect);
            effect.PlaybackSpeed = PlaybackSpeed;
            effect.SetTargets(targets);
            if (effect is PawForYouVfx paw) paw.OnTargetImpacted += OnImpact;
            if (effect is KAimShotVfx shot) shot.SetMuzzleSocket(MuzzleSocket);
            effect.Play(anchor, target);
            // 안전 상한은 연출 판정을 대신하지 않고, 잘못 설정된 부품의 잔존만 방지합니다.
            float limit = step.lifetime > 0 ? step.lifetime : 15f;
            for (float t = 0; effect != null && effect.IsPlaying && t < limit; t += EffectDeltaTime)
                yield return null;
            if (effect != null)
            {
                if (effect is PawForYouVfx p) p.OnTargetImpacted -= OnImpact;
                effect.Stop();
                Destroy(effect.gameObject);
            }
            running.Remove(effect);
            pending--;
        }
        private void OnImpact() { if (impacted) return; impacted = true; Impacted?.Invoke(); }
        public static float ResolveDelay(Step step)
        {
            float delay = step.delay;
            if (step.shotTiming != null && step.timing == Timing.ShotLaunch) delay += step.shotTiming.launchDelay;
            if (step.shotTiming != null && step.timing == Timing.ShotImpact) delay += step.shotTiming.impactBurst.startDelay;
            if (step.pawTiming != null && (step.timing == Timing.PawDeparture || step.timing == Timing.PawArrival))
            {
                delay += step.pawTiming.appearTime + step.pawTiming.holdTime;
                if (step.timing == Timing.PawArrival) delay += step.pawTiming.warpOutTime + step.pawTiming.warpTravelTime;
            }
            return Mathf.Max(0, delay);
        }
        private IEnumerator WaitForParts()
        {
            while (pending > 0) yield return null;
            IsPlaying = false;
            RaiseFinished();
        }
        public override void Stop()
        {
            StopAllCoroutines();
            foreach (var effect in running)
            {
                if (effect == null) continue;
                if (effect is PawForYouVfx paw) paw.OnTargetImpacted -= OnImpact;
                effect.Stop();
                Destroy(effect.gameObject);
            }
            running.Clear(); pending = 0; IsPlaying = false;
        }
        private void OnDisable() => Stop();
    }
}
