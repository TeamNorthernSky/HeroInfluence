using System.Collections.Generic;
using UnityEngine;

/// <summary>시퀀서가 해석해 컨텍스트에 등록하는 런타임 Cue (프리팹/사운드/배치/시각).</summary>
public class RuntimeCue
{
    public List<GameObject> EffectPrefabs = new List<GameObject>();
    public List<int> SoundIds = new List<int>();
    public CueOperation Operation = CueOperation.Spawn;
    public string InstanceKey;
    public SpawnAnchor Anchor = SpawnAnchor.CasterSocket;
    public UnitSocket Socket;

    /// <summary>정규화된 CueName. 드라이버가 발화할 때 이 이름을 외친다.</summary>
    public string NormalizedCueName;

    /// <summary>ClipEvent면 클립 이벤트가 발화. 그 외는 드라이버가 Time에 발화.</summary>
    public CueTimingSource Timing = CueTimingSource.ClipEvent;
    public float Time;

    public bool IsDataTimed => Timing != CueTimingSource.ClipEvent;

    /// <summary>발화 시각(초). NormalizedTime은 클립 길이를 곱한다. 해석 불가면 음수.</summary>
    public float ResolveFireSeconds(float stateLength)
    {
        if (Timing == CueTimingSource.Seconds)
        {
            return Mathf.Max(0f, Time);
        }

        return stateLength > 0f ? Mathf.Max(0f, Time) * stateLength : -1f;
    }
}

/// <summary>유닛 로컬 Cue 컨텍스트와 유지형 이펙트 Handle의 액션 단위 수명 소유자.</summary>
[DisallowMultipleComponent]
public class PresentationRuntimeContext : MonoBehaviour
{
    public int CurrentActionInstanceId { get; private set; }
    public SkillEffectContext Current { get; private set; }

    /// <summary>
    /// 이 Cue 표가 속한 Animator state의 fullPathHash. 0이면 state 미지정(게이트 통과).
    /// <see cref="PresentationCueDriver"/>가 발화 게이트로 소비한다 — 값을 넣기만 하고 쓰지 않으면
    /// 이전 페이즈의 늦은 이벤트가 새 Cue 표를 맞춰 오발화한다.
    /// </summary>
    public int ExpectedStateHash { get; private set; }

    /// <summary>진단 로그용 식별 문자열(연출 자산명 + 기대 state). 런타임 동작에는 쓰지 않는다.</summary>
    public string DebugLabel { get; private set; } = string.Empty;

    private readonly Dictionary<string, RuntimeCue> _cues = new Dictionary<string, RuntimeCue>();

    /// <summary>등록 순서를 보존한 Cue 목록. 같은 프레임에 여러 Cue가 걸릴 때 선언 순서로 발화하기 위해 유지한다.</summary>
    private readonly List<RuntimeCue> _orderedCues = new List<RuntimeCue>();

    // ── 다-state Cue 인덱스 (§3-A / §3-A2) — 재배선 전 검증 가능한 토대 ──
    // 모든 state의 Cue를 stateHash로 인덱싱한다. 드라이버가 현재 state로 조회하면 Beat별 SetActive 교체가
    // 불필요해지고(동기화 문제 소멸), 이름 단일 키가 아니라 (state, 이름)으로 특정하므로 전이 중 동명 Cue도 구분된다.
    // ★아직 라이브 경로(SetupPresentationContext→SetActive)는 이 인덱스를 쓰지 않는다 — 이 API는
    //   아무 데서도 호출되지 않는 순수 추가분이며 기존 동작에 영향이 없다. 라이브 배선은 1010 실측·PlayMode 회귀
    //   뒤에 한다(Docs/SkillPresentation_StateDrivenCue_구현검토서.md §3-A/§3-A2/§8).
    private readonly Dictionary<int, List<RuntimeCue>> _cuesByState = new Dictionary<int, List<RuntimeCue>>();
    private readonly Dictionary<int, Dictionary<string, RuntimeCue>> _lookupByState =
        new Dictionary<int, Dictionary<string, RuntimeCue>>();

    private readonly Dictionary<string, ISkillEffectHandle> _handles = new Dictionary<string, ISkillEffectHandle>();
    private readonly HashSet<GameObject> _activeOneShotCueEffects = new HashSet<GameObject>();

    public bool HasValidContext => CurrentActionInstanceId != 0 && Current != null;
    public bool HasActiveOneShotCueEffects
    {
        get
        {
            RemoveDestroyedOneShotCueEffects();
            return _activeOneShotCueEffects.Count > 0;
        }
    }

    /// <summary>등록 순서가 보존된 Cue 목록(읽기 전용). 드라이버 순회용.</summary>
    public IReadOnlyList<RuntimeCue> OrderedCues => _orderedCues;

    /// <summary>같은 actionId면 Beat의 Cue map만 갱신하고 Held Handle은 유지한다.</summary>
    public void SetActive(int actionInstanceId, SkillEffectContext ctx, List<RuntimeCue> cues, int expectedStateHash,
        string debugLabel = null)
    {
        if (actionInstanceId == 0)
        {
            Clear();
            return;
        }

        if (CurrentActionInstanceId != 0 && CurrentActionInstanceId != actionInstanceId)
        {
            StopAllHandles();
        }

        CurrentActionInstanceId = actionInstanceId;
        Current = ctx;
        ExpectedStateHash = expectedStateHash;
        DebugLabel = debugLabel ?? string.Empty;
        _cues.Clear();
        _orderedCues.Clear();
        if (cues == null) return;

        for (int i = 0; i < cues.Count; i++)
        {
            RuntimeCue cue = cues[i];
            string key = cue?.NormalizedCueName;
            if (string.IsNullOrEmpty(key) || _cues.ContainsKey(key)) continue;

            _cues[key] = cue;
            _orderedCues.Add(cue);
        }
    }

    public bool TryGetCue(string normalizedName, out RuntimeCue cue)
    {
        cue = null;
        return HasValidContext
            && !string.IsNullOrEmpty(normalizedName)
            && _cues.TryGetValue(normalizedName, out cue);
    }

    // ──────────────────────────────────────────────────────────────
    // 다-state Cue 인덱스 (§3-A) — 스킬 시작 시 모든 state의 Cue를 한 번에 등록
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 한 state의 Cue 목록을 인덱스에 등록한다. stateHash 0(미지정)은 등록하지 않는다 —
    /// state를 지정하지 않은 자산은 게이트를 통과시키는 규칙과 동일하게 인덱싱 대상이 아니다.
    /// 같은 stateHash로 다시 등록하면 덮어쓴다(재시전 갱신). 한 state 안의 이름 중복은
    /// 첫 항목만 남긴다 — 이름이 조회 키이므로 <see cref="SetActive"/>와 같은 규칙이다.
    /// </summary>
    public void RegisterStateCues(int stateHash, List<RuntimeCue> cues)
    {
        if (stateHash == 0) return;

        var ordered = new List<RuntimeCue>();
        var lookup = new Dictionary<string, RuntimeCue>();
        if (cues != null)
        {
            for (int i = 0; i < cues.Count; i++)
            {
                RuntimeCue cue = cues[i];
                string key = cue?.NormalizedCueName;
                if (string.IsNullOrEmpty(key) || lookup.ContainsKey(key)) continue;
                lookup[key] = cue;
                ordered.Add(cue);
            }
        }

        _cuesByState[stateHash] = ordered;
        _lookupByState[stateHash] = lookup;
    }

    /// <summary>
    /// 현재 state 해시로 그 state의 Cue를 조회한다(§3-A2). 이름 단일 키가 아니라 (state, 이름)으로 특정하므로
    /// 전이 중 이전/다음 state가 같은 이름을 가져도 어느 state의 것인지 확정된다.
    /// </summary>
    public bool TryGetCueForState(int stateHash, string normalizedName, out RuntimeCue cue)
    {
        cue = null;
        return !string.IsNullOrEmpty(normalizedName)
            && _lookupByState.TryGetValue(stateHash, out Dictionary<string, RuntimeCue> lookup)
            && lookup.TryGetValue(normalizedName, out cue);
    }

    /// <summary>그 state의 등록 순서 보존 Cue 목록(없으면 빈 목록). 드라이버 시간 기반 순회용.</summary>
    public IReadOnlyList<RuntimeCue> GetOrderedCuesForState(int stateHash)
    {
        return _cuesByState.TryGetValue(stateHash, out List<RuntimeCue> list)
            ? list
            : System.Array.Empty<RuntimeCue>();
    }

    /// <summary>이 state의 Cue가 인덱스에 등록되어 있는지.</summary>
    public bool HasStateCues(int stateHash) => _cuesByState.ContainsKey(stateHash);

    /// <summary>인덱스에 등록된 state 수(진단·테스트용).</summary>
    public int IndexedStateCount => _cuesByState.Count;

    /// <summary>state 인덱스를 비운다. 스킬 종료 시 호출해 다음 연출로 새지 않게 한다.</summary>
    public void ClearStateIndex()
    {
        _cuesByState.Clear();
        _lookupByState.Clear();
    }

    private static bool IsAlive(ISkillEffectHandle handle)
    {
        return handle is Object unityObject ? unityObject != null : handle != null;
    }

    public void RegisterHandle(string instanceKey, ISkillEffectHandle handle)
    {
        if (string.IsNullOrEmpty(instanceKey) || !IsAlive(handle)) return;
        if (_handles.TryGetValue(instanceKey, out ISkillEffectHandle existing))
        {
            if (IsAlive(existing) && existing != handle) existing.Stop();
            _handles.Remove(instanceKey);
        }
        _handles[instanceKey] = handle;
    }

    public bool TryGetHandle(string instanceKey, out ISkillEffectHandle handle)
    {
        handle = null;
        if (string.IsNullOrEmpty(instanceKey) || !_handles.TryGetValue(instanceKey, out ISkillEffectHandle candidate)) return false;
        if (!IsAlive(candidate))
        {
            _handles.Remove(instanceKey);
            return false;
        }
        handle = candidate;
        return true;
    }

    public void RemoveHandle(string instanceKey)
    {
        if (!string.IsNullOrEmpty(instanceKey)) _handles.Remove(instanceKey);
    }

    public void StopAndRemoveHandle(string instanceKey)
    {
        if (TryGetHandle(instanceKey, out ISkillEffectHandle handle)) handle.Stop();
        RemoveHandle(instanceKey);
    }

    public void StopAllHandles()
    {
        foreach (ISkillEffectHandle handle in _handles.Values)
        {
            if (IsAlive(handle)) handle.Stop();
        }
        _handles.Clear();
    }

    public void RegisterActiveOneShotCueEffect(GameObject instance)
    {
        if (instance != null)
        {
            _activeOneShotCueEffects.Add(instance);
        }
    }

    public void UnregisterActiveOneShotCueEffect(GameObject instance)
    {
        if (instance != null)
        {
            _activeOneShotCueEffects.Remove(instance);
        }
        RemoveDestroyedOneShotCueEffects();
    }

    public void CollectActiveOneShotCueEffectNames(List<string> destination)
    {
        if (destination == null) return;

        RemoveDestroyedOneShotCueEffects();
        foreach (GameObject instance in _activeOneShotCueEffects)
        {
            destination.Add($"{gameObject.name}/{instance.name}");
        }
    }

    private void RemoveDestroyedOneShotCueEffects()
    {
        _activeOneShotCueEffects.RemoveWhere(instance => instance == null || !instance.activeInHierarchy);
    }

    private void OnDisable()
    {
        StopAllHandles();
    }

    private void OnDestroy()
    {
        StopAllHandles();
        _activeOneShotCueEffects.Clear();
    }

    public void Clear()
    {
        StopAllHandles();
        CurrentActionInstanceId = 0;
        Current = null;
        ExpectedStateHash = 0;
        DebugLabel = string.Empty;
        _cues.Clear();
        _orderedCues.Clear();
        ClearStateIndex();
    }
}
