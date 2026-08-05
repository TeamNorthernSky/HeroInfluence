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

    private readonly Dictionary<string, ISkillEffectHandle> _handles = new Dictionary<string, ISkillEffectHandle>();

    public bool HasValidContext => CurrentActionInstanceId != 0 && Current != null;

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

    private void OnDisable()
    {
        StopAllHandles();
    }

    private void OnDestroy()
    {
        StopAllHandles();
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
    }
}
