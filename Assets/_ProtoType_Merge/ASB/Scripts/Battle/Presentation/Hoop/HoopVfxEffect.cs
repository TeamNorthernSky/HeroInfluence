using System.Collections.Generic;
using JC.VFX;
using UnityEngine;

public enum HoopEffectKind { Summon, Charge, WheelBreak }

/// <summary>
/// 굴렁쇠 전용 절차 생성 이펙트(소환진 / 충전 / 파괴 연기·파편). 표시만 하며 전투 상태를 바꾸지 않는다.
/// 원본: JcYuliaVfxEffect의 Summon·Charge·WheelBreak 분기. 재생 속도는 전투 배속을 매 프레임 따른다.
/// </summary>
public sealed class HoopVfxEffect : VfxEffect
{
    private const int LinePoints = 65;

    private HoopVfxPreset _preset;
    private HoopEffectKind _kind;
    private readonly List<LineRenderer> _lines = new List<LineRenderer>();
    private readonly List<Renderer> _shells = new List<Renderer>();
    private ParticleSystem _smoke;
    private ParticleSystem _fragments;
    private float _elapsed;
    private float _lifetime;
    private MaterialPropertyBlock _block;

    /// <summary>
    /// 이펙트를 생성해 즉시 재생한다. parent가 null이면 씬 루트에 생성한다
    /// — 시체 제거(Destroy)보다 오래 남아야 하는 파괴 이펙트는 굴렁쇠 자식으로 두지 않는다.
    /// </summary>
    public static HoopVfxEffect Spawn(HoopVfxPreset preset, HoopEffectKind kind, Vector3 position, Transform parent = null)
    {
        if (preset == null) return null;
        var go = new GameObject("Hoop_" + kind);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        var effect = go.AddComponent<HoopVfxEffect>();
        effect._preset = preset;
        effect._kind = kind;
        effect.Play();
        return effect;
    }

    public override void Play()
    {
        if (_preset == null) { Destroy(gameObject); return; }
        _elapsed = 0f;
        IsPlaying = true;
        _block ??= new MaterialPropertyBlock();
        _lifetime = _kind == HoopEffectKind.WheelBreak ? _preset.destroySeconds
            : _kind == HoopEffectKind.Summon ? _preset.summonSeconds
            : _preset.pulseSeconds;
        PlaybackSpeed = CurrentBattleSpeed();
        if (_lines.Count == 0 && _shells.Count == 0 && _smoke == null) Build();
        foreach (var r in _lines) r.enabled = true;
        foreach (var r in _shells) r.enabled = true;
        if (_smoke != null) { _smoke.Clear(); _smoke.Play(); }
        if (_fragments != null) { _fragments.Clear(); _fragments.Play(); }
        Draw(0f);
    }

    public override void Stop()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        foreach (var r in _lines) if (r != null) r.enabled = false;
        foreach (var r in _shells) if (r != null) r.enabled = false;
        if (_smoke != null) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_fragments != null) _fragments.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        RaiseFinished();
        Destroy(gameObject);
    }

    private void OnDisable() { IsPlaying = false; }

    private void Update()
    {
        if (!IsPlaying) return;
        PlaybackSpeed = CurrentBattleSpeed();
        _elapsed += EffectDeltaTime;
        if (_smoke != null) { var main = _smoke.main; main.simulationSpeed = PlaybackSpeed; }
        if (_fragments != null) { var main = _fragments.main; main.simulationSpeed = PlaybackSpeed; }
        Draw(Mathf.Clamp01(_elapsed / Mathf.Max(.01f, _lifetime)));
        if (_elapsed >= _lifetime) Stop();
    }

    private static float CurrentBattleSpeed()
    {
        return BattleManager.Instance != null ? Mathf.Max(.01f, BattleManager.Instance.CurrentBattleSpeed) : 1f;
    }

    private void Build()
    {
        switch (_kind)
        {
            case HoopEffectKind.WheelBreak:
                BuildSmoke(); BuildFragments(); AddLine(); AddLine(); break;
            case HoopEffectKind.Charge:
                AddShell(); AddLine(); AddLine(); AddLine(); break;
            default:
                AddLine(); AddLine(); AddLine(); break;
        }
    }

    private void AddLine()
    {
        var go = new GameObject("EnergyStroke");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = _preset.energyMaterial;
        line.useWorldSpace = true;
        line.positionCount = LinePoints;
        line.numCapVertices = 3;
        line.widthMultiplier = _preset.lineWidth;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        _lines.Add(line);
    }

    private void AddShell()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ChargeEnergySurface";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * .27f;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = _preset.energyMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        _shells.Add(r);
    }

    private void BuildSmoke()
    {
        var go = new GameObject("DarkVioletSmoke");
        go.transform.SetParent(transform, false);
        _smoke = go.AddComponent<ParticleSystem>();
        _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _smoke.main;
        main.playOnAwake = false; main.loop = false; main.duration = .15f;
        main.startLifetime = _preset.destroySeconds; main.startSpeed = .38f; main.startSize = .5f;
        main.maxParticles = 24; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = _preset.smoke;
        var emission = _smoke.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, 18) });
        var shape = _smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .22f;
        var velocity = _smoke.velocityOverLifetime;
        velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .25f;
        var color = _smoke.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(.7f, 0), new GradientAlphaKey(.45f, .3f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var size = _smoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .4f), new Keyframe(1, 1.8f)));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _preset.smokeMaterial;
        renderer.sortingFudge = 1;
    }

    // 파괴 컷의 금속 파편. 외형 표시만 하며 물리 충돌이나 전투 난수를 사용하지 않는다.
    private void BuildFragments()
    {
        var go = new GameObject("WheelMetalFragments");
        go.transform.SetParent(transform, false);
        _fragments = go.AddComponent<ParticleSystem>();
        _fragments.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _fragments.useAutoRandomSeed = false;
        _fragments.randomSeed = 40005;
        var main = _fragments.main;
        main.playOnAwake = false; main.loop = false; main.duration = .1f;
        main.startLifetime = _preset.destroySeconds * .85f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(.7f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(.07f, .14f);
        main.maxParticles = 16;
        main.gravityModifier = .35f;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = _fragments.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, 12) });
        var shape = _fragments.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .3f;
        var rotation = _fragments.rotationOverLifetime;
        rotation.enabled = true; rotation.separateAxes = true;
        rotation.x = 2; rotation.y = 3; rotation.z = 4;
        var size = _fragments.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(.65f, 1), new Keyframe(1, 0)));
        var renderer = _fragments.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        renderer.sharedMaterial = _preset.frameMaterial;
    }

    private void Tint(Renderer renderer, Color color, float mode = 0f)
    {
        renderer.GetPropertyBlock(_block);
        _block.SetColor("_Color", color);
        _block.SetFloat("_Mode", mode);
        renderer.SetPropertyBlock(_block);
    }

    private void Draw(float progress)
    {
        Vector3 center = transform.position;
        float fade = Mathf.Sin(Mathf.PI * progress);
        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            Color color = i == 0 ? _preset.core : _preset.energy;
            color.a *= fade;
            line.startColor = line.endColor = Color.white;
            Tint(line, color);
            line.widthMultiplier = _preset.lineWidth * (i == 0 ? .55f : 1.4f);

            float radius = _kind == HoopEffectKind.Charge ? .14f
                : _kind == HoopEffectKind.Summon ? .35f + progress * .4f
                : .12f + progress * .55f;
            radius *= 1 + i * .18f;
            for (int n = 0; n < LinePoints; n++)
            {
                float a = n / (float)(LinePoints - 1) * Mathf.PI * 2;
                if (_kind == HoopEffectKind.Charge)
                {
                    // 서로 직교하는 세 고리. 이펙트 transform 기준.
                    Vector3 p = i == 0 ? new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0)
                        : i == 1 ? new Vector3(0, Mathf.Sin(a) * radius, Mathf.Cos(a) * radius)
                        : new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                    line.SetPosition(n, transform.TransformPoint(p));
                }
                else
                {
                    // 소환진은 8개 톱니 굴곡, 파괴는 퍼지는 원.
                    float teeth = _kind == HoopEffectKind.Summon ? 1 + .07f * Mathf.Cos(a * 8) : 1;
                    var p = new Vector3(Mathf.Cos(a) * radius * teeth, .025f + i * .025f, Mathf.Sin(a) * radius * teeth);
                    line.SetPosition(n, center + p);
                }
            }
        }

        foreach (var shell in _shells)
        {
            Color c = _preset.energy;
            c.a = (.65f + .1f * Mathf.Sin(_elapsed * 2)) * fade;
            Tint(shell, c, 1f);
        }
    }
}
