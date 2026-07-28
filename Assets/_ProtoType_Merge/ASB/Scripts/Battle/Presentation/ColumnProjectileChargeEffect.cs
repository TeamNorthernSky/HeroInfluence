using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "시전자 앞에 최대 N개 차지 → fire 신호에 각 적으로 날아가는" 순수 연출 컨트롤러.
/// 피해/판정은 전혀 건드리지 않는다(전투는 기존 열 AoE가 OnHit에 동시 처리).
/// Play(charge): ctx.Targets(열의 적, 최대 MaxCount)만큼 오브를 앞 소켓 위치에 생성.
/// Signal(fire): 각 오브를 대응 타깃으로 코스메틱 비행 후 소멸.
/// </summary>
[DisallowMultipleComponent]
public class ColumnProjectileChargeEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    [Tooltip("앞에 띄우고 날려보낼 오브 프리팹. 비우면 이 오브젝트 자체를 1개 오브로 사용.")]
    [SerializeField] private GameObject _orbPrefab;
    [Tooltip("최대 오브/투사체 수(열의 적 수와 min).")]
    [SerializeField] private int _maxCount = 3;
    [Tooltip("각 오브가 타깃까지 날아가는 배속-시간(초).")]
    [SerializeField] private float _flySeconds = 0.25f;
    [Tooltip("앞 소켓 기준 오브 좌우 간격.")]
    [SerializeField] private float _spread = 0.4f;
    [Tooltip("포물선 높이(0이면 직선).")]
    [SerializeField] private float _arcHeight = 0.3f;

    private readonly List<GameObject> _orbs = new List<GameObject>();
    private readonly List<Transform> _targets = new List<Transform>();
    private float _battleSpeed = 1f;
    private bool _fired;

    public void Play(SkillEffectContext ctx)
    {
        Transform origin = ctx != null && ctx.SocketTransform != null ? ctx.SocketTransform : transform;
        _battleSpeed = ctx != null ? Mathf.Max(0.01f, ctx.PlaybackSpeed) : 1f;
        transform.position = origin.position;
        transform.rotation = origin.rotation;

        if (ctx?.Targets == null)
        {
            return;
        }

        int n = 0;
        foreach (BattleCharactor unit in ctx.Targets)
        {
            if (unit == null || unit.IsDead)
            {
                continue;
            }
            _targets.Add(unit.transform);

            GameObject orb;
            if (_orbPrefab != null)
            {
                orb = Instantiate(_orbPrefab, transform);
            }
            else
            {
                orb = new GameObject("orb");
                orb.transform.SetParent(transform, false);
            }
            // 앞 소켓 기준 좌우로 살짝 벌려 배치(가운데 정렬).
            orb.transform.localPosition = new Vector3((n - (_maxCount - 1) * 0.5f) * _spread, 0f, 0f);
            _orbs.Add(orb);

            n++;
            if (n >= _maxCount)
            {
                break;
            }
        }
    }

    /// <summary>fire Cue: 각 오브를 대응 타깃으로 날려보낸다(연출).</summary>
    public bool Signal(SkillEffectContext ctx)
    {
        if (!_fired && isActiveAndEnabled)
        {
            _fired = true;
            StartCoroutine(FlyRoutine());
        }
        return true; // Handle 수명 종료 — 이후 오브 수명은 이 컴포넌트가 책임.
    }

    private IEnumerator FlyRoutine()
    {
        // 월드로 분리 후 각자 타깃으로 이동.
        var starts = new List<Vector3>(_orbs.Count);
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (_orbs[i] == null)
            {
                starts.Add(Vector3.zero);
                continue;
            }
            _orbs[i].transform.SetParent(null, true);
            starts.Add(_orbs[i].transform.position);
        }

        float t = 0f;
        while (t < _flySeconds)
        {
            t += Time.deltaTime * _battleSpeed;
            float u = Mathf.Clamp01(t / _flySeconds);
            for (int i = 0; i < _orbs.Count; i++)
            {
                if (_orbs[i] == null || i >= _targets.Count || _targets[i] == null)
                {
                    continue;
                }
                Vector3 pos = Vector3.Lerp(starts[i], _targets[i].position, u);
                pos.y += _arcHeight * 4f * u * (1f - u);
                _orbs[i].transform.position = pos;
            }
            yield return null;
        }

        DestroyAll();
    }

    public void Stop()
    {
        DestroyAll();
    }

    private void DestroyAll()
    {
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (_orbs[i] != null)
            {
                Destroy(_orbs[i]);
            }
        }
        _orbs.Clear();
        if (this != null)
        {
            Destroy(gameObject);
        }
    }
}
