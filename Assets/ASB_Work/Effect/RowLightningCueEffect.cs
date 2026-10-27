using System.Collections;
using System.Collections.Generic;
using ASB.Work.BattleGrid;
using GridCellRef = ASB.Work.BattleGrid.GridCell;
using JC.VFX;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>줄 번개를 깔 방향. 좌표 x = 전후(아군 0~1 / 적군 2~3), y = 좌우.</summary>
public enum RowLightningAxis
{
    /// <summary>주 대상과 같은 x의 칸 전부(좌우 한 줄). 율리아 「나락의 폭풍」.</summary>
    SameX,
    /// <summary>주 대상과 같은 y의 칸 전부 — 아군 끝 줄부터 적군 끝 줄까지. 율리아 「공멸의 궤적」.</summary>
    SameY
}

/// <summary>
/// 율리아 줄 번개(400011 나락의 폭풍 / 400012 공멸의 궤적). 주 대상이 선 줄을 가로질러 번개 줄을 깔고,
/// 유지한 뒤 서서히 사라진다. 2020 ChainLightningSkill의 볼트 렌더러·감전 템플릿을 복제해 쓰며 원본은 바꾸지 않는다.
/// <para>
/// 아크 프리팹(기획 Y05·Y06, Ultra Mega E02)이 있으면 줄마다 VFX 전기 아크를 띄운다.
/// 선택적으로 볼트 렌더러를 겹쳐 밝은 중심선과 보라색 외곽 빛을 별도로 그린다.
/// 아크 끝점은 긋기 시간 동안 시작점에서 끝점으로 뻗고, 유지 후 우아한 정지로 사라진다.
/// 시작 구체(Y06 전열 구체)가 있으면 시작점에 먼저 띄운 뒤 originLeadTime 뒤에 긋기 시작한다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class RowLightningCueEffect : MonoBehaviour, ISkillEffectBehaviour
{
    private static readonly int ProgressID = Shader.PropertyToID("_Progress");
    private static readonly int SeedID = Shader.PropertyToID("_Seed");
    private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
    private static readonly int WidthID = Shader.PropertyToID("_Width");
    private static readonly int CoreWidthID = Shader.PropertyToID("_CoreWidth");
    private static readonly int GlowWidthID = Shader.PropertyToID("_GlowWidth");
    private static readonly int EmissionID = Shader.PropertyToID("_Emission");
    private static readonly int CoreColorID = Shader.PropertyToID("_ColorCore");
    private static readonly int GlowColorID = Shader.PropertyToID("_ColorGlow");
    private static readonly int BodyColorID = Shader.PropertyToID("_ColorBody");
    private static readonly int CenterColorID = Shader.PropertyToID("_ColorCenter");

    [Header("2020 ChainLightningSkill 재료")]
    [Tooltip("ChainLightningSkill 프리팹의 ChainBolt 렌더러(줄 1개 템플릿).")]
    [SerializeField] private Renderer _boltTemplate;
    [Tooltip("ChainLightningSkill 프리팹의 LightningShock(대상 감전). 비우면 감전을 생략한다.")]
    [SerializeField] private LightningShock _shockTemplate;

    [Header("Ultra Mega 전기 아크 (선택 — 비우면 위 볼트 렌더러)")]
    [Tooltip("줄마다 띄울 전기 아크(VisualEffectGraphEffect + ElectricArcEndpoints, Loop).")]
    [SerializeField] private VfxEffect _arcPrefab;
    [Tooltip("긋기 전에 시작점에 띄울 구체(기획 Y06 전열 구체). 비우면 생략.")]
    [SerializeField] private VfxEffect _originOrbPrefab;
    [Header("율리아 납품 이펙트 (기존 Cue/대상 판정 유지)")]
    [SerializeField] private GameObject _nativeStormPrefab;
    [SerializeField] private UliaVfx.UliaSavedEffect _nativeTrajectoryPrefab;
    [Tooltip("납품 나락의 폭풍 파티클 재생 속도. 1보다 크면 이펙트와 유지 시간이 함께 짧아진다.")]
    [SerializeField, Min(0.1f)] private float _nativeStormPlaybackMultiplier = 1f;
    [Tooltip("시작 구체가 뜬 뒤 긋기 시작까지의 시간.")]
    [SerializeField, Min(0f)] private float _originLeadTime = 0.12f;
    [Tooltip("아크 위에 2020 볼트를 겹쳐 중심선과 외곽 광을 그린다. 아크는 가지와 불꽃으로 사용한다.")]
    [SerializeField] private bool _layerBoltWithArcs;
    [Tooltip("아크 프리팹의 선 굵기 배율. 0이면 프리팹 설정을 유지한다.")]
    [SerializeField, Min(0f)] private float _arcThicknessScale;

    [Header("배치")]
    [Tooltip("SameX = 대상과 같은 x의 좌우 한 줄, SameY = 대상과 같은 y로 아군 끝~적군 끝.")]
    [SerializeField] private RowLightningAxis _axis = RowLightningAxis.SameX;
    [SerializeField, Min(1)] private int _lineCount = 3;
    [Tooltip("줄 사이 간격(m). 줄과 수직인 방향으로 나란히 놓는다.")]
    [SerializeField, Min(0f)] private float _lineSpacing = 0.3f;
    [Tooltip("양 끝을 칸 바깥으로 늘리는 길이(각 끝 칸의 인접 칸 간격 배수). 0.5 = 끝 칸의 바깥 경계, 2.5 = 가상 칸 2개의 바깥 경계.")]
    [SerializeField, Min(0f)] private float _endExtension = 0.5f;
    [Tooltip("시전자 쪽(시작) 끝만 따로 늘리는 길이. 음수면 _endExtension과 같다.")]
    [SerializeField] private float _startExtension = -1f;
    [Tooltip("주 대상 진영의 칸만 잇는다(SameY에서 의미, 기획 Y06: 대상 진영 전열 → 라인 끝). 좌표 x 0~1 아군 / 2~3 적군.")]
    [SerializeField] private bool _targetSideOnly;
    [Tooltip("칸 바닥에서 줄까지의 높이(m).")]
    [SerializeField] private float _height = 0.8f;
    [Tooltip("줄 1개의 굵기 = 줄과 수직인 칸 간격 × 이 비율. 0이면 2020 머티리얼 굵기를 그대로 쓴다.")]
    [SerializeField, Min(0f)] private float _widthRatio;
    [Tooltip("볼트 중심선 폭(볼트 전체 반폭에 대한 비율). 0이면 머티리얼 값.")]
    [SerializeField, Min(0f)] private float _boltCoreWidth;
    [Tooltip("볼트 외곽 빛 폭(볼트 전체 반폭에 대한 비율). 0이면 머티리얼 값.")]
    [SerializeField, Min(0f)] private float _boltGlowWidth;
    [Tooltip("볼트 발광 강도. 0이면 머티리얼 값.")]
    [SerializeField, Min(0f)] private float _boltEmission;
    [Tooltip("아크와 겹쳐 그리는 볼트의 밝은 중심선 색.")]
    [SerializeField, ColorUsage(false, true)] private Color _boltCoreColor = new Color(1.2f, 0.65f, 1.6f, 1f);
    [Tooltip("아크와 겹쳐 그리는 볼트의 보라색 외곽 빛.")]
    [SerializeField, ColorUsage(false, true)] private Color _boltGlowColor = new Color(0.85f, 0.12f, 1.8f, 1f);

    [Header("시간(초, 전투 배속 반영)")]
    [SerializeField, Min(0.01f)] private float _drawTime = 0.08f;
    [Tooltip("줄마다 긋기 시작을 늦추는 간격.")]
    [SerializeField, Min(0f)] private float _lineStagger = 0.05f;
    [SerializeField, Min(0f)] private float _holdTime = 0.3f;
    [SerializeField, Min(0.01f)] private float _fadeTime = 1f;
    [SerializeField, Min(0.01f)] private float _flickerRate = 18f;

    [Header("감전")]
    [SerializeField] private bool _shockTargets = true;
    [SerializeField, Min(0.01f)] private float _shockSize = 1.7f;
    [SerializeField, Min(0.05f)] private float _shockDuration = 1f;
    [SerializeField, Min(0.01f)] private float _shockFadeTime = 0.25f;

    [Header("색 덮어쓰기 (2020 원본 머티리얼은 바꾸지 않음)")]
    [Tooltip("켜면 아래 색을 프로퍼티 블록으로 덮어쓴다. 기본값 = 2020 파란 팔레트를 색상환 +52°(보라)로 돌리고 " +
             "밝기 ×0.85 — 색끼리의 색상·채도·밝기 차이는 원본과 같다.")]
    [SerializeField] private bool _overrideColors;
    [Tooltip("번개·감전 아우라 코어(_ColorCore).")]
    [SerializeField, ColorUsage(false, true)] private Color _coreColor = new Color(1.481f, 1.275f, 1.658f, 1f);
    [Tooltip("번개·감전 아우라 글로우(_ColorGlow).")]
    [SerializeField, ColorUsage(false, true)] private Color _glowColor = new Color(0.986f, 0.323f, 1.318f, 1f);
    [Tooltip("감전 배경 바깥(_ColorBody).")]
    [SerializeField, ColorUsage(false, true)] private Color _shockBodyColor = new Color(0.209f, 0.060f, 0.289f, 1f);
    [Tooltip("감전 배경 중심(_ColorCenter).")]
    [SerializeField, ColorUsage(false, true)] private Color _shockCenterColor = new Color(0.642f, 0.238f, 0.935f, 1f);

    private readonly List<LightningShock> _shocks = new List<LightningShock>();
    private MaterialPropertyBlock _mpb;
    private float _speed = 1f;
    private float _halfWidth = -1f;   // < 0 = 머티리얼 값 유지
    private bool _nativeStormPlaying;

    private void OnDisable() => _nativeStormPlaying = false;

    public void Play(SkillEffectContext ctx)
    {
        _speed = ctx != null ? Mathf.Max(0.01f, ctx.PlaybackSpeed) : 1f;
        bool useArcs = _arcPrefab != null;
        if (!TryResolveRow(ctx, out Vector3 start, out Vector3 end, out float crossSpacing))
        {
            Destroy(gameObject);
            return;
        }

        if (_nativeStormPrefab != null)
        {
            StartCoroutine(RunNativeStorm(ctx, (start + end) * 0.5f - Vector3.up * _height));
            return;
        }

        if (_nativeTrajectoryPrefab != null)
        {
            StartCoroutine(RunNativeTrajectory(ctx, start, end));
            return;
        }

        if (!useArcs && _boltTemplate == null)
        {
            Destroy(gameObject);
            return;
        }

        // 셰이더 _Width는 반폭(m)이므로 총 굵기의 절반을 넣는다.
        _halfWidth = _widthRatio > 0f ? crossSpacing * _widthRatio * 0.5f : -1f;
        if (useArcs)
        {
            StartCoroutine(RunArcs(ctx, start, end));
            return;
        }

        StartCoroutine(Run(ctx, start, end));
    }

    private IEnumerator RunNativeStorm(SkillEffectContext ctx, Vector3 center)
    {
        _nativeStormPlaying = true;
        GameObject strike = Instantiate(_nativeStormPrefab, center + Vector3.up * 0.02f,
            _nativeStormPrefab.transform.rotation, transform);
        strike.transform.localScale = _nativeStormPrefab.transform.localScale;
        ParticleSystem[] particles = strike.GetComponentsInChildren<ParticleSystem>(true);
        float stormSpeed = Mathf.Max(0.1f, _nativeStormPlaybackMultiplier);
        foreach (ParticleSystem ps in particles)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.simulationSpeed *= stormSpeed * _speed;
        }

        foreach (ParticleSystem ps in particles)
            if ((ps.transform == strike.transform || ps.name == "shot_controller" || ps.name == "hit_controller")
                && ps.gameObject.activeInHierarchy)
                ps.Play(false);

        PresentationSfxEvents.PlayEffectImpact(ctx?.Presentation, 0);
        SpawnShocks(ctx);
        SustainStormHitAnimations(ctx?.Targets);
        yield return Wait(2f / stormSpeed);
        while (_shocks.Exists(shock => shock != null)) yield return null;
        _nativeStormPlaying = false;
        Destroy(gameObject);
    }

    // 첫 피해의 Hit/넉백은 전투 파이프라인이 한 번만 실행한다. 이후에는 폭풍이 살아 있는 동안
    // Animator의 Hit 포즈만 이어 붙여 피해·피격음·넉백을 다시 발생시키지 않는다.
    private void SustainStormHitAnimations(IReadOnlyList<BattleCharactor> targets)
    {
        if (targets == null) return;
        var seen = new HashSet<BattleCharactor>();
        for (int i = 0; i < targets.Count; i++)
        {
            BattleCharactor target = targets[i];
            if (target != null && seen.Add(target)) StartCoroutine(SustainStormHitAnimation(target));
        }
    }

    private IEnumerator SustainStormHitAnimation(BattleCharactor target)
    {
        // storm_lines와 Impact 마커가 같은 시각이다. 마커 실행 순서와 무관하게 실제 Hit 진입을 기다린다.
        float waited = 0f;
        while (_nativeStormPlaying && target != null && !target.IsDead && waited < .25f)
        {
            CharactorAnimationController controller = target.Anim;
            Animator animator = controller != null ? controller.Animator : null;
            if (animator != null && animator.isActiveAndEnabled && IsHitPlayingOrEntering(animator)) break;
            waited += Time.deltaTime * _speed;
            yield return null;
        }

        while (_nativeStormPlaying && target != null && !target.IsDead)
        {
            CharactorAnimationController controller = target.Anim;
            Animator animator = controller != null ? controller.Animator : null;
            if (animator == null || !animator.isActiveAndEnabled || !IsHitPlayingOrEntering(animator)) yield break;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            bool replay = animator.IsInTransition(0)
                ? current.IsName("Hit") && animator.GetNextAnimatorStateInfo(0).IsName("Idle")
                : current.IsName("Hit") && current.normalizedTime >= .85f;
            if (replay) controller.PlayState("Hit", .03f);
            yield return null;
        }
        // 마지막 Hit 클립은 자연스럽게 끝난다. 다른 동작이나 사망 애니메이션은 덮어쓰지 않는다.
    }

    private static bool IsHitPlayingOrEntering(Animator animator)
    {
        return animator.GetCurrentAnimatorStateInfo(0).IsName("Hit")
            || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Hit"));
    }

    private IEnumerator RunNativeTrajectory(SkillEffectContext ctx, Vector3 start, Vector3 end)
    {
        UliaVfx.UliaSavedEffect shot = Instantiate(_nativeTrajectoryPrefab, start, Quaternion.identity, transform);
        shot.playOnEnable = false;
        shot.effectCamera = Camera.main;
        shot.endTarget = null;
        shot.localEndPoint = shot.transform.InverseTransformPoint(end);
        // 기존 Timeline이 충전 Cue를 재생한 뒤 이 이펙트를 호출하므로 발사 시각만 맞춘다.
        shot.trajectoryChargeSeconds = 0.01f;
        shot.Replay();
        yield return Wait(_drawTime);
        PresentationSfxEvents.PlayEffectImpact(ctx?.Presentation, 0);
        SpawnShocks(ctx);
        yield return Wait(2f);
        while (_shocks.Exists(shock => shock != null)) yield return null;
        Destroy(gameObject);
    }

    private IEnumerator RunArcs(SkillEffectContext ctx, Vector3 start, Vector3 end)
    {
        Vector3 side = LineSide(start, end);
        int count = Mathf.Max(1, _lineCount);

        VfxEffect orb = null;
        if (_originOrbPrefab != null)
        {
            orb = Instantiate(_originOrbPrefab, start, Quaternion.identity);
            orb.PlaybackSpeed = _speed;
            orb.Play();
            yield return Wait(_originLeadTime);
        }

        // 줄마다 시작·끝 끝점을 만들고 아크를 묶는다. 끝 끝점을 시작점에서 끝점으로 옮겨 "뻗는" 모습을 낸다.
        var heads = new Transform[count];
        var arcs = new VfxEffect[count];
        bool layerBolts = _layerBoltWithArcs && _boltTemplate != null;
        var bolts = layerBolts ? new Renderer[count] : null;
        if (layerBolts) _mpb = new MaterialPropertyBlock();
        var from = new Vector3[count];
        var to = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = LineOffset(side, i, count, _lineSpacing);
            from[i] = start + offset;
            to[i] = end + offset;
            if (layerBolts)
            {
                bolts[i] = Instantiate(_boltTemplate, transform);
                AimBolt(bolts[i].transform, from[i], to[i]);
                Push(bolts[i], 0f, 0f, i);
                bolts[i].gameObject.SetActive(true);
            }
            // 끝점은 이 오브젝트 아래에 둔다 — 아크가 정지하며 사라질 때까지 남아 있다가 마지막에 같이 정리된다.
            Transform tail = CreateEndpoint("ArcTail", from[i]);
            heads[i] = CreateEndpoint("ArcHead", from[i]);
            arcs[i] = Instantiate(_arcPrefab, from[i], Quaternion.identity, transform);
            if (_arcThicknessScale > 0f && !Mathf.Approximately(_arcThicknessScale, 1f))
                ScaleArcThickness(arcs[i], _arcThicknessScale);
            arcs[i].PlaybackSpeed = _speed;
            arcs[i].Play(tail, heads[i]);
        }

        float drawEnd = _lineStagger * (count - 1) + _drawTime;
        bool shocked = false;
        for (float t = 0f; t < drawEnd + _holdTime; t += Time.deltaTime * _speed)
        {
            for (int i = 0; i < count; i++)
            {
                float progress = ArcDrawProgress(t, i, _lineStagger, _drawTime);
                if (heads[i] != null) heads[i].position = Vector3.Lerp(from[i], to[i], progress);
                if (layerBolts) Push(bolts[i], progress, t >= _lineStagger * i ? 1f : 0f, i);
            }

            if (!shocked && t >= _drawTime)
            {
                shocked = true;
                PresentationSfxEvents.PlayEffectImpact(ctx?.Presentation, 0);
                SpawnShocks(ctx);
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (heads[i] != null) heads[i].position = to[i];
            if (layerBolts) Push(bolts[i], 1f, 1f, i);
            if (arcs[i] != null) arcs[i].Stop();
        }
        if (orb != null) orb.Stop();

        // 아크는 남은 입자가 사라지면 스스로 파괴된다(VisualEffectGraphEffect). 끝점이 먼저 사라지면 아크가 즉시 멈추므로 기다린다.
        for (float t = 0f; t < _fadeTime && (layerBolts || System.Array.Exists(arcs, arc => arc != null)); t += Time.deltaTime * _speed)
        {
            if (layerBolts)
                for (int i = 0; i < count; i++) Push(bolts[i], 1f, 1f - Mathf.Clamp01(t / _fadeTime), i);
            yield return null;
        }
        if (layerBolts)
            for (int i = 0; i < count; i++)
                if (bolts[i] != null) bolts[i].gameObject.SetActive(false);
        while (_shocks.Exists(shock => shock != null)) yield return null;
        Destroy(gameObject);
    }

    private static void ScaleArcThickness(VfxEffect arc, float scale)
    {
        foreach (VisualEffect effect in arc.GetComponentsInChildren<VisualEffect>(true))
            if (effect.HasFloat("Thickness"))
                effect.SetFloat("Thickness", effect.GetFloat("Thickness") * scale);
    }

    private Transform CreateEndpoint(string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        return go.transform;
    }

    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime * _speed) yield return null;
    }

    /// <summary>줄 i의 긋기 진행도(0~1). 줄마다 stagger만큼 늦게 시작한다.</summary>
    public static float ArcDrawProgress(float elapsed, int index, float stagger, float drawTime)
    {
        return Mathf.Clamp01((elapsed - stagger * index) / Mathf.Max(0.0001f, drawTime));
    }

    /// <summary>줄과 수직인 수평 방향. 줄이 수직이거나 길이가 없으면 오른쪽.</summary>
    public static Vector3 LineSide(Vector3 start, Vector3 end)
    {
        Vector3 flat = end - start;
        flat.y = 0f;
        return flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
    }

    /// <summary>count개 줄을 중앙 기준으로 spacing 간격으로 나란히 놓는 오프셋.</summary>
    public static Vector3 LineOffset(Vector3 side, int index, int count, float spacing)
    {
        return side * ((index - (count - 1) * 0.5f) * spacing);
    }

    private IEnumerator Run(SkillEffectContext ctx, Vector3 start, Vector3 end)
    {
        _mpb = new MaterialPropertyBlock();
        Vector3 side = LineSide(start, end);

        int count = Mathf.Max(1, _lineCount);
        var bolts = new Renderer[count];
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = LineOffset(side, i, count, _lineSpacing);
            Renderer bolt = Instantiate(_boltTemplate, transform);
            AimBolt(bolt.transform, start + offset, end + offset);
            Push(bolt, 0f, 0f, i);
            bolt.gameObject.SetActive(true);
            bolts[i] = bolt;
        }

        float total = _lineStagger * (count - 1) + _drawTime + _holdTime + _fadeTime;
        bool shocked = false;
        for (float t = 0f; t < total; t += Time.deltaTime * _speed)
        {
            for (int i = 0; i < count; i++)
            {
                // 줄마다 긋기 → 유지 → 페이드. 시작 전 줄은 숨긴다.
                float local = t - _lineStagger * i;
                float progress = Mathf.Clamp01(local / _drawTime);
                float opacity = local < 0f ? 0f : 1f - Mathf.Clamp01((local - _drawTime - _holdTime) / _fadeTime);
                Push(bolts[i], progress, opacity, i);
            }

            if (!shocked && t >= _drawTime)
            {
                shocked = true;
                // 번개 줄이 다 그어져 감전이 시작되는 순간(1회).
                PresentationSfxEvents.PlayEffectImpact(ctx?.Presentation, 0);
                SpawnShocks(ctx);
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (bolts[i] != null) bolts[i].gameObject.SetActive(false);

        // LightningShock은 지속 시간이 끝나면 스스로 파괴된다.
        while (_shocks.Exists(shock => shock != null)) yield return null;
        Destroy(gameObject);
    }

    private void SpawnShocks(SkillEffectContext ctx)
    {
        if (!_shockTargets || _shockTemplate == null || ctx?.Targets == null) return;
        for (int i = 0; i < ctx.Targets.Count; i++)
        {
            BattleCharactor target = ctx.Targets[i];
            if (target == null || target.IsDead) continue;
            LightningShock shock = Instantiate(_shockTemplate, transform);
            shock.Init(target.transform, Vector3.up * _height, _shockSize,
                _shockDuration / _speed, _shockFadeTime / _speed, _flickerRate);
            if (_overrideColors) TintShock(shock);
            _shocks.Add(shock);
        }
    }

    // LightningShock은 매 프레임 기존 프로퍼티 블록을 읽어 _Seed/_Opacity만 바꾸므로 여기서 넣은 색이 유지된다.
    private void TintShock(LightningShock shock)
    {
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in shock.GetComponentsInChildren<Renderer>(true))
        {
            Material material = r.sharedMaterial;
            if (material == null) continue;
            r.GetPropertyBlock(block);
            if (material.HasProperty(CoreColorID)) block.SetColor(CoreColorID, _coreColor);
            if (material.HasProperty(GlowColorID)) block.SetColor(GlowColorID, _glowColor);
            if (material.HasProperty(BodyColorID)) block.SetColor(BodyColorID, _shockBodyColor);
            if (material.HasProperty(CenterColorID)) block.SetColor(CenterColorID, _shockCenterColor);
            r.SetPropertyBlock(block);
        }
    }

    // 주 대상(없으면 첫 대상)이 있는 칸과 같은 줄의 칸을 순서대로 잇는다. 칸을 못 찾으면 대상 위치로 폴백한다.
    // crossSpacing = 줄과 수직인 이웃 칸 간격(굵기 기준).
    private bool TryResolveRow(SkillEffectContext ctx, out Vector3 start, out Vector3 end, out float crossSpacing)
    {
        start = end = Vector3.zero;
        crossSpacing = 1f;
        if (ctx == null) return false;

        GridCellRef center = ctx.PrimaryTarget != null ? ctx.PrimaryTarget.OccupiedCell : null;
        if (center == null && ctx.Targets != null)
            for (int i = 0; i < ctx.Targets.Count && center == null; i++)
                center = ctx.Targets[i] != null ? ctx.Targets[i].OccupiedCell : null;

        var points = new List<Vector3>();
        BattleGridManager grid = BattleGridManager.Instance;
        if (center != null && grid != null)
        {
            Vector2Int c0 = center.Coords;
            bool sameX = _axis == RowLightningAxis.SameX;
            List<Vector2Int> coords = grid.GetAllCoordsSnapshot();
            coords.RemoveAll(c => sameX ? c.x != c0.x : c.y != c0.y);
            if (_targetSideOnly) coords.RemoveAll(c => !IsSameSide(c.x, c0.x));
            coords.Sort((a, b) => sameX ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            for (int i = 0; i < coords.Count; i++)
                if (grid.TryGetCell(coords[i], out GridCellRef cell) && cell != null)
                    points.Add(cell.transform.position);

            Vector2Int cross = sameX ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
            if ((grid.TryGetCell(c0 + cross, out GridCellRef neighbor) && neighbor != null) ||
                (grid.TryGetCell(c0 - cross, out neighbor) && neighbor != null))
                crossSpacing = Vector3.Distance(center.transform.position, neighbor.transform.position);
        }
        else if (ctx.Targets != null)
        {
            for (int i = 0; i < ctx.Targets.Count; i++)
                if (ctx.Targets[i] != null) points.Add(ctx.Targets[i].transform.position);
            SortFarthestPairToEnds(points);
        }

        if (points.Count == 0) return false;

        float startSpacing = 1f;
        float endSpacing = 1f;
        if (points.Count == 1)
        {
            // 한 칸뿐이면 시전자→대상 방향과 수직으로 1칸 길이를 잡는다.
            Vector3 forward = ctx.Caster != null ? points[0] - ctx.Caster.transform.position : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward.normalized);
            start = points[0] - side * 0.5f;
            end = points[0] + side * 0.5f;
        }
        else
        {
            start = points[0];
            end = points[points.Count - 1];
            // 진영 사이 간격이 일반 칸 간격과 달라도 바깥쪽 연장은 각 진영의 칸 간격을 따른다.
            startSpacing = Vector3.Distance(points[0], points[1]);
            endSpacing = Vector3.Distance(points[points.Count - 2], points[points.Count - 1]);
        }

        // 시전자 쪽 끝에서 긋기 시작한다(공멸의 궤적은 적진→아군 끝으로 뻗는다).
        if (ctx.Caster != null &&
            (end - ctx.Caster.transform.position).sqrMagnitude < (start - ctx.Caster.transform.position).sqrMagnitude)
        {
            Vector3 swap = start;
            start = end;
            end = swap;
            float spacingSwap = startSpacing;
            startSpacing = endSpacing;
            endSpacing = spacingSwap;
        }

        Vector3 dir = (end - start).normalized;
        float startExtension = _startExtension >= 0f ? _startExtension : _endExtension;
        start += -dir * (startSpacing * startExtension) + Vector3.up * _height;
        end += dir * (endSpacing * _endExtension) + Vector3.up * _height;
        return true;
    }

    /// <summary>두 좌표 x가 같은 진영인지(아군 0~1 / 적군 2~3).</summary>
    public static bool IsSameSide(int x, int otherX)
    {
        return (x <= 1) == (otherX <= 1);
    }

    private static void SortFarthestPairToEnds(List<Vector3> points)
    {
        if (points.Count < 2) return;
        int a = 0, b = 1;
        float best = -1f;
        for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
            {
                float d = (points[i] - points[j]).sqrMagnitude;
                if (d > best) { best = d; a = i; b = j; }
            }
        Vector3 first = points[a], last = points[b];
        points.Clear();
        points.Add(first);
        points.Add(last);
    }

    // ChainLightningVfx.AimBolt와 같은 규칙: 쿼드의 +X를 선분 방향으로 돌리고 X 스케일을 길이로 둔다.
    private static void AimBolt(Transform t, Vector3 start, Vector3 end)
    {
        Vector3 dir = end - start;
        float len = dir.magnitude;
        t.position = start;
        t.rotation = len > 1e-4f ? Quaternion.FromToRotation(Vector3.right, dir / len) : Quaternion.identity;
        Vector3 scale = t.localScale;
        t.localScale = new Vector3(len, scale.y, scale.z);
    }

    private void Push(Renderer r, float progress, float opacity, int index)
    {
        if (r == null) return;
        r.GetPropertyBlock(_mpb);
        _mpb.SetFloat(ProgressID, progress);
        _mpb.SetFloat(SeedID, Mathf.Floor(Time.time * _flickerRate) + index * 17f);
        _mpb.SetFloat(OpacityID, opacity);
        if (_halfWidth > 0f) _mpb.SetFloat(WidthID, _halfWidth);
        if (_boltCoreWidth > 0f) _mpb.SetFloat(CoreWidthID, _boltCoreWidth);
        if (_boltGlowWidth > 0f) _mpb.SetFloat(GlowWidthID, _boltGlowWidth);
        if (_boltEmission > 0f) _mpb.SetFloat(EmissionID, _boltEmission);
        if (_overrideColors)
        {
            bool layeredArc = _layerBoltWithArcs && _arcPrefab != null;
            _mpb.SetColor(CoreColorID, layeredArc ? _boltCoreColor : _coreColor);
            _mpb.SetColor(GlowColorID, layeredArc ? _boltGlowColor : _glowColor);
        }
        r.SetPropertyBlock(_mpb);
    }
}
