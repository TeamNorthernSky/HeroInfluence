using UnityEngine;

namespace JC.VFX
{
    // Independent opening cue: the caster circle appears before the overhead barrage.
    public sealed class TaoCastCircleVfx : VfxEffect
    {
        [SerializeField] private TaoMagicCircle circle;
        [SerializeField] private TaoBarragePreset placement;
        [SerializeField, Min(.1f)] private float durationSeconds = 3.2f;
        [SerializeField, Min(.01f)] private float fadeSeconds = .3f;
        private float elapsed;

        public override void Play(Transform origin, Transform target)
        {
            if (circle == null || origin == null) { RaiseFinished(); return; }
            var p = placement != null ? placement.TransformSource : null;
            Vector3 hand = JcVfxPlacementPreset.Resolve(origin,
                p != null ? p.handSocketName : "Socket_R_VFX",
                p != null ? p.handOffset : new Vector3(0f, 0f, .15f));
            Vector3 face = target != null ? target.position - hand : origin.forward;
            face.y = 0f;
            circle.SetUpright(true);
            circle.SetFacing(face.sqrMagnitude > .0001f ? face : origin.forward);
            circle.SetEnvelope(1f);
            circle.Play(hand);
            elapsed = 0f;
            IsPlaying = true;
        }

        private void Update()
        {
            if (!IsPlaying) return;
            elapsed += EffectDeltaTime;
            circle.SetEnvelope(Mathf.Clamp01((durationSeconds - elapsed) / fadeSeconds));
            if (elapsed < durationSeconds) return;
            Stop();
            RaiseFinished();
        }

        public override void Stop()
        {
            IsPlaying = false;
            if (circle != null) circle.Stop();
        }
    }
}
