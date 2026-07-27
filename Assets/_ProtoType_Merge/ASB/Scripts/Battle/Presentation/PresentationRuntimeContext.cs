using System.Collections.Generic;
using UnityEngine;

/// <summary>시퀀서가 해석해 컨텍스트에 등록하는 런타임 Cue (프리팹/사운드/배치).</summary>
public class RuntimeCue
{
    public List<GameObject> EffectPrefabs = new List<GameObject>();
    public List<int> SoundIds = new List<int>();
    public CueOperation Operation = CueOperation.Spawn;
    public string InstanceKey;
    public SpawnAnchor Anchor = SpawnAnchor.CasterSocket;
    public UnitSocket Socket;
}

/// <summary>유닛 로컬 Cue 컨텍스트와 유지형 이펙트 Handle의 액션 단위 수명 소유자.</summary>
[DisallowMultipleComponent]
public class PresentationRuntimeContext : MonoBehaviour
{
    public int CurrentActionInstanceId { get; private set; }
    public SkillEffectContext Current { get; private set; }
    public int ExpectedStateHash { get; private set; }

    private readonly Dictionary<string, RuntimeCue> _cues = new Dictionary<string, RuntimeCue>();
    private readonly Dictionary<string, ISkillEffectHandle> _handles = new Dictionary<string, ISkillEffectHandle>();

    public bool HasValidContext => CurrentActionInstanceId != 0 && Current != null;

    /// <summary>같은 actionId면 Beat의 Cue map만 갱신하고 Held Handle은 유지한다.</summary>
    public void SetActive(int actionInstanceId, SkillEffectContext ctx, Dictionary<string, RuntimeCue> cues, int expectedStateHash)
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
        _cues.Clear();
        if (cues == null) return;

        foreach (KeyValuePair<string, RuntimeCue> kv in cues)
        {
            _cues[kv.Key] = kv.Value;
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
        _cues.Clear();
    }
}