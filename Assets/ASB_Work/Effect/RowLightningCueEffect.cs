using System.Collections;
using System.Collections.Generic;
using ASB.Work.BattleGrid;
using GridCellRef = ASB.Work.BattleGrid.GridCell;
using JC.VFX;
using UnityEngine;

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
/// </summary>
[DisallowMultipleComponent]
public sealed class RowLightningCueEffect : MonoBehaviour, ISkillEffectBehaviour
{
    private static readonly int ProgressID = Shader.PropertyToID("_Progress");
    private static readonly int SeedID = Shader.PropertyToID("_Seed");
    private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
    private static readonly int WidthID = Shader.PropertyToID("_Width");
    private static readonly int CoreColorID = Shader.PropertyToID("_ColorCore");
    private static readonly int GlowColorID = Shader.PropertyToID("_ColorGlow");
    private static readonly int BodyColorID = Shader.PropertyToID("_ColorBody");
    private static readonly int CenterColorID = Shader.PropertyToID("_ColorCenter");

    [Header("2020 ChainLightningSkill 재료")]
    [Tooltip("ChainLightningSkill 프리팹의 ChainBolt 렌더러(줄 1개 템플릿).")]
    [SerializeField] private Renderer _boltTemplate;
    [Tooltip("ChainLightningSkill 프리팹의 LightningShock(대상 감전). 비우면 감전을 생략한다.")]
    [SerializeField] private LightningShock _shockTemplate;

    [Header("배치")]
    [Tooltip("SameX = 대상과 같은 x의 좌우 한 줄, SameY = 대상과 같은 y로 아군 끝~적군 끝.")]
    [SerializeField] private RowLightningAxis _axis = RowLightningAxis.SameX;
    [SerializeField, Min(1)] private int _lineCount = 3;
    [Tooltip("줄 사이 간격(m). 줄과 수직인 방향으로 나란히 놓는다.")]
    [SerializeField, Min(0f)] private float _lineSpacing = 0.3f;
    [Tooltip("양 끝을 칸 바깥으로 늘리는 길이(각 끝 칸의 인접 칸 간격 배수). 0.5 = 끝 칸의 바깥 경계, 2.5 = 가상 칸 2개의 바깥 경계.")]
    [SerializeField, Min(0f)] private float _endExtension = 0.5f;
    [Tooltip("칸 바닥에서 줄까지의 높이(m).")]
    [SerializeField] private float _height = 0.8f;
    [Tooltip("줄 1개의 굵기 = 줄과 수직인 칸 간격 × 이 비율. 0이면 2020 머티리얼 굵기를 그대로 쓴다.")]
    [SerializeField, Min(0f)] private float _widthRatio;

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

    public void Play(SkillEffectContext ctx)
    {
        _speed = ctx != null ? Mathf.Max(0.01f, ctx.PlaybackSpeed) : 1f;
        if (_boltTemplate == null || !TryResolveRow(ctx, out Vector3 start, out Vector3 end, out float crossSpacing))
        {
            Destroy(gameObject);
            return;
        }

        // 셰이더 _Width는 반폭(m)이므로 총 굵기의 절반을 넣는다.
        _halfWidth = _widthRatio > 0f ? crossSpacing * _widthRatio * 0.5f : -1f;
        StartCoroutine(Run(ctx, start, end));
    }

    private IEnumerator Run(SkillEffectContext ctx, Vector3 start, Vector3 end)
    {
        _mpb = new MaterialPropertyBlock();
        Vector3 flat = end - start;
        flat.y = 0f;
        Vector3 side = flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;

        int count = Mathf.Max(1, _lineCount);
        var bolts = new Renderer[count];
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = side * ((i - (count - 1) * 0.5f) * _lineSpacing);
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
        start += -dir * (startSpacing * _endExtension) + Vector3.up * _height;
        end += dir * (endSpacing * _endExtension) + Vector3.up * _height;
        return true;
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
        if (_overrideColors)
        {
            _mpb.SetColor(CoreColorID, _coreColor);
            _mpb.SetColor(GlowColorID, _glowColor);
        }
        r.SetPropertyBlock(_mpb);
    }
}
