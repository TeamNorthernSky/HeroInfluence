using System.Collections.Generic;
using JC.VFX;
using UnityEngine;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;

[DisallowMultipleComponent]
public sealed class PrismExplosionCueEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    [Tooltip("독립 차징·비행·착탄을 연결할 프리즘 익스플로전입니다.")]
    [SerializeField] private PrismExplosionVfx _prismExplosion;

    private SkillEffectContext _context;
    private readonly List<Transform> _targets = new List<Transform>();
    private Transform _launchOrigin;
    private Transform _aimPoint;
    private bool _launched;
    private bool _impactNotified;

    private void Awake()
    {
        if (_prismExplosion == null)
        {
            _prismExplosion = GetComponent<PrismExplosionVfx>();
        }
    }

    public void Play(SkillEffectContext context)
    {
        _context = context;
        _launched = false;
        _impactNotified = false;
        _targets.Clear();

        if (_prismExplosion == null || context == null)
        {
            return;
        }

        _launchOrigin = context.SocketTransform != null
            ? context.SocketTransform
            : context.Caster != null
                ? context.Caster.transform
                : transform;

        Transform target = context.PrimaryTarget != null ? context.PrimaryTarget.transform : null;
        _prismExplosion.Impacted -= OnImpact;
        _prismExplosion.Impacted += OnImpact;
        _prismExplosion.PlaybackSpeed = context.PlaybackSpeed;
        _prismExplosion.Play(_launchOrigin, target);
        // Launch is deliberately deferred. BattleManager calls Signal() on AniEvent_OnHit.
    }

    public bool Signal(SkillEffectContext context)
    {
        if (_prismExplosion == null || _launched)
        {
            return false;
        }

        Launch(context ?? _context);
        return _launched;
    }

    public void Stop()
    {
        if (!_impactNotified && _context != null)
        {
            SkillImpactSignalBus.PublishCancelled(
                _context.ActionInstanceId,
                _targets,
                transform.position);
        }

        if (_prismExplosion != null) { _prismExplosion.Impacted -= OnImpact; _prismExplosion.Stop(); }
        Destroy(gameObject);
    }

    private void Launch(SkillEffectContext context)
    {
        if (_launched || _prismExplosion == null)
        {
            return;
        }

        _launched = true;
        BuildTargets(context);
        // 착탄점 = 대상 진영 칸 전체의 중앙. 대상 평균이면 적이 한쪽(예: 후열)에만 있을 때 그쪽으로 쏠린다.
        // 피해·착탄 신호 대상(_targets)은 그대로 두고 조준점만 바꾼다. 그리드를 못 찾으면 기존(대상 평균).
        _prismExplosion.Launch(TryGetBoardCenter(out Vector3 boardCenter) ? AimAt(boardCenter) : _targets);
    }

    private bool TryGetBoardCenter(out Vector3 center)
    {
        center = Vector3.zero;
        ASB.Work.BattleGrid.BattleGridManager grid = ASB.Work.BattleGrid.BattleGridManager.Instance;
        if (grid == null) return false;

        ASBGridCell sideCell = null;
        foreach (Transform target in _targets)
        {
            BattleCharactor unit = target != null ? target.GetComponent<BattleCharactor>() : null;
            if (unit != null && unit.OccupiedCell != null)
            {
                sideCell = unit.OccupiedCell;
                break;
            }
        }
        if (sideCell == null) return false;

        int count = 0;
        foreach (Vector2Int coords in ASB.Work.BattleGrid.SkillTargetingMapper.GetFullSideBoardCoordinates(sideCell.Coords))
        {
            if (!grid.TryGetCell(coords, out ASBGridCell cell) || cell == null) continue;
            center += cell.transform.position;
            count++;
        }
        if (count == 0) return false;
        center /= count;
        return true;
    }

    // 발사 순간 한 번만 읽히므로 이펙트 하위에 두고 함께 파괴한다.
    private IReadOnlyList<Transform> AimAt(Vector3 position)
    {
        if (_aimPoint == null)
        {
            _aimPoint = new GameObject("PrismExplosionAimPoint").transform;
            _aimPoint.SetParent(transform, false);
        }
        _aimPoint.position = position;
        return new[] { _aimPoint };
    }

    private void BuildTargets(SkillEffectContext context)
    {
        _targets.Clear();
        if (context != null && context.Targets != null)
        {
            foreach (BattleCharactor target in context.Targets)
            {
                if (target != null)
                {
                    _targets.Add(target.transform);
                }
            }
        }

        if (_targets.Count == 0 && context != null && context.PrimaryTarget != null)
        {
            _targets.Add(context.PrimaryTarget.transform);
        }
    }

    private void OnImpact(Vector3 position)
    {
        if (_impactNotified || _context == null) return;
        _impactNotified = true;
        PresentationSfxEvents.PlayEffectImpact(_context.Presentation, 0);
        SkillImpactSignalBus.PublishImpact(new ImpactKey(_context.ActionInstanceId), position, _targets);
    }

}
