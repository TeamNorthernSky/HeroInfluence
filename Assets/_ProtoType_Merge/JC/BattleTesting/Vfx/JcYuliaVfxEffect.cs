using System.Collections.Generic;
using UnityEngine;
using JC.VFX;

namespace JC.BattleTesting.Vfx
{
    public enum JcYuliaEffectKind { Pulse, Summon, Smoke, Recover, Veil, Supply, Charge, WheelBreak }

    // 대응: RenderFX/HeroSkill/_Shared/Scripts/VfxEffect 및 Lumina/FlareBombImpact.
    // 확정된 위치만 표시합니다. HP·턴·예약·대상 선정·피해·난수를 변경하지 않습니다.
    public sealed class JcYuliaVfxEffect : VfxEffect, ISkillEffectBehaviour
    {
        [Tooltip("표시할 VFX 부품입니다. 전투 규칙이나 피해 종류를 변경하지 않습니다.")]
        public JcYuliaEffectKind kind;
        [Tooltip("JC 전용 색·재질·시간 설정입니다. 실행 중 변경한 수치는 다음 재생에서 적용합니다.")]
        public JcYuliaVfxPreset preset;
        [Tooltip("켜면 장막 등 유지형 부품으로 재생합니다. 소유자가 Stop으로 회수합니다.")]
        public bool loop;

        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<Renderer> shells = new List<Renderer>();
        private ParticleSystem particles;
        private ParticleSystem fragments;
        private float elapsed, lifetime;
        private Transform origin, target;
        private Vector3 start, finish;
        private MaterialPropertyBlock block;

        public void Play(SkillEffectContext context)
        {
            PlaybackSpeed = Mathf.Max(.01f, context?.PlaybackSpeed ?? 1);
            Play(context?.SocketTransform, context?.PrimaryTarget != null ? context.PrimaryTarget.transform : null);
        }
        public override void Play() => Play(null, null);
        public override void Play(Transform from, Transform to)
        {
            if (preset == null) { Destroy(gameObject); return; }
            origin = from; target = to; start = from != null ? from.position : transform.position;
            finish = to != null ? to.position + Vector3.up * .8f : start;
            elapsed = 0; IsPlaying = true; block ??= new MaterialPropertyBlock();
            lifetime = kind == JcYuliaEffectKind.Smoke || kind == JcYuliaEffectKind.WheelBreak ? preset.destroySeconds :
                kind == JcYuliaEffectKind.Summon ? preset.summonSeconds :
                kind == JcYuliaEffectKind.Recover ? preset.recoverSeconds :
                kind == JcYuliaEffectKind.Supply ? preset.supplySeconds : preset.pulseSeconds;
            if (lines.Count == 0 && shells.Count == 0 && particles == null) Build();
            foreach (var r in lines) r.enabled = true;
            foreach (var r in shells) r.enabled = true;
            if (particles != null) { particles.Clear(); particles.Play(); }
            if (fragments != null) { fragments.Clear(); fragments.Play(); }
            Draw(0);
        }
        private void Build()
        {
            switch (kind)
            {
                case JcYuliaEffectKind.Veil:
                    Shell(); for (int i = 0; i < 3; i++) Line(); break;
                case JcYuliaEffectKind.Supply:
                    Line(); Line(); break;
                case JcYuliaEffectKind.Smoke:
                    Smoke(); Line(); break;
                case JcYuliaEffectKind.WheelBreak:
                    Smoke(); Fragments(); Line(); Line(); break;
                case JcYuliaEffectKind.Charge:
                    Shell(); Line(); Line(); Line(); break;
                default:
                    Line(); Line(); Line(); break;
            }
        }
        private LineRenderer Line()
        {
            var go = new GameObject("EnergyStroke"); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = preset.energyMaterial;
            line.useWorldSpace = true; line.positionCount = 65; line.numCapVertices = 3;
            line.widthMultiplier = preset.lineWidth; line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            lines.Add(line); return line;
        }
        private void Shell()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "VeilEnergySurface";
            Destroy(go.GetComponent<Collider>()); go.transform.SetParent(transform, false);
            go.transform.localPosition = kind == JcYuliaEffectKind.Charge ? Vector3.zero : Vector3.up * preset.veilRadius.y;
            go.transform.localScale = kind == JcYuliaEffectKind.Charge ? Vector3.one * .27f : preset.veilRadius * 2;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = preset.energyMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            shells.Add(r);
        }
        private void Smoke()
        {
            var go = new GameObject("DarkVioletSmoke"); go.transform.SetParent(transform, false);
            particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false; main.loop = false; main.duration = .15f;
            main.startLifetime = preset.destroySeconds; main.startSpeed = .38f; main.startSize = .5f;
            main.maxParticles = 24; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = preset.smoke;
            var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 18) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .22f;
            var velocity = particles.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .25f;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)}, new[]{new GradientAlphaKey(.7f,0),new GradientAlphaKey(.45f,.3f),new GradientAlphaKey(0,1)}); color.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.4f),new Keyframe(1,1.8f)));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = preset.smokeMaterial;
            r.sortingFudge = 1;
        }
        // motion_09 파괴 컷의 금속 파편. 외형 표시만 하며 물리 충돌이나 전투 난수를 사용하지 않습니다.
        private void Fragments()
        {
            var go = new GameObject("WheelMetalFragments"); go.transform.SetParent(transform, false);
            fragments = go.AddComponent<ParticleSystem>();
            fragments.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fragments.useAutoRandomSeed = false; fragments.randomSeed = 40005;
            var main = fragments.main; main.playOnAwake = false; main.loop = false; main.duration = .1f;
            main.startLifetime = preset.destroySeconds * .85f; main.startSpeed = new ParticleSystem.MinMaxCurve(.7f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.07f, .14f); main.maxParticles = 16;
            main.gravityModifier = .35f; main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = fragments.emission; emission.rateOverTime = 0; emission.SetBursts(new[]{new ParticleSystem.Burst(0, 12)});
            var shape = fragments.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .3f;
            var rotation = fragments.rotationOverLifetime; rotation.enabled = true; rotation.separateAxes = true;
            rotation.x = 2; rotation.y = 3; rotation.z = 4;
            var size = fragments.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0,1),new Keyframe(.65f,1),new Keyframe(1,0)));
            var renderer = fragments.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx"); renderer.sharedMaterial = preset.frameMaterial;
        }
        private void Update()
        {
            if (!IsPlaying) return;
            elapsed += EffectDeltaTime;
            if (particles != null) { var main = particles.main; main.simulationSpeed = PlaybackSpeed; }
            if (fragments != null) { var main = fragments.main; main.simulationSpeed = PlaybackSpeed; }
            Draw(loop ? elapsed : Mathf.Clamp01(elapsed / Mathf.Max(.01f, lifetime)));
            if (!loop && elapsed >= lifetime) Stop();
        }
        private void Tint(Renderer renderer, Color color, float mode = 0)
        {
            renderer.GetPropertyBlock(block); block.SetColor("_Color", color); block.SetFloat("_Mode", mode); renderer.SetPropertyBlock(block);
        }
        private void Draw(float progress)
        {
            Vector3 center = transform.position;
            float fade = loop ? .55f + .15f * Mathf.Sin(elapsed * 3) : Mathf.Sin(Mathf.PI * progress);
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i]; Color color = i == 0 ? preset.core : preset.energy; color.a *= fade;
                line.startColor = line.endColor = Color.white; Tint(line, color);
                line.widthMultiplier = preset.lineWidth * (i == 0 ? .55f : 1.4f);
                if (kind == JcYuliaEffectKind.Supply)
                {
                    Vector3 a = origin != null ? origin.position : start;
                    Vector3 b = target != null ? target.position + Vector3.up * .9f : finish;
                    float travel = loop ? 1 : Mathf.Clamp01(progress * 3);
                    for (int n = 0; n < 65; n++)
                    {
                        float t = n / 64f; Vector3 p = Vector3.Lerp(a,b,t*travel);
                        Vector3 jitter = Vector3.up * Mathf.Sin(t*47+elapsed*31+i)*.055f;
                        line.SetPosition(n,p+jitter*Mathf.Sin(t*Mathf.PI));
                    }
                    continue;
                }
                for (int n = 0; n < 65; n++)
                {
                    float a = n / 64f * Mathf.PI * 2;
                    Vector3 p;
                    if (kind == JcYuliaEffectKind.Veil)
                    {
                        var radii = preset.veilRadius;
                        p = i == 0 ? new Vector3(Mathf.Cos(a)*radii.x, radii.y+Mathf.Sin(a)*radii.y,0) :
                            i == 1 ? new Vector3(0,radii.y+Mathf.Sin(a)*radii.y,Mathf.Cos(a)*radii.z) :
                            new Vector3(Mathf.Cos(a)*radii.x,radii.y+Mathf.Sin(elapsed*2)*.13f,Mathf.Sin(a)*radii.z);
                    }
                    else
                    {
                        float radius = kind == JcYuliaEffectKind.Charge ? .14f :
                            kind == JcYuliaEffectKind.Summon ? .35f+progress*.4f :
                            kind == JcYuliaEffectKind.Recover ? .3f+progress*.2f : .12f+progress*.55f;
                        radius *= 1+i*.18f;
                        float teeth = kind == JcYuliaEffectKind.Summon ? (1+.07f*Mathf.Cos(a*8)) : 1;
                        if (kind == JcYuliaEffectKind.Charge)
                            p = i == 0 ? new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0) :
                                i == 1 ? new Vector3(0,Mathf.Sin(a)*radius,Mathf.Cos(a)*radius) :
                                new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                        else p = new Vector3(Mathf.Cos(a)*radius*teeth,kind == JcYuliaEffectKind.Recover ? progress*.8f : .025f+i*.025f,Mathf.Sin(a)*radius*teeth);
                    }
                    line.SetPosition(n,kind == JcYuliaEffectKind.Charge ? transform.TransformPoint(p) : center+p);
                }
            }
            foreach (var shell in shells)
            {
                Color c = preset.energy; c.a = (.65f+.1f*Mathf.Sin(elapsed*2))*(loop?1:fade); Tint(shell,c,1);
            }
        }
        public override void Stop()
        {
            if (!IsPlaying) return;
            IsPlaying = false; foreach (var r in lines) if(r!=null) r.enabled=false;
            foreach(var r in shells) if(r!=null) r.enabled=false;
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (fragments != null) fragments.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            RaiseFinished(); Destroy(gameObject);
        }
        private void OnDisable() { IsPlaying = false; }
    }
}
