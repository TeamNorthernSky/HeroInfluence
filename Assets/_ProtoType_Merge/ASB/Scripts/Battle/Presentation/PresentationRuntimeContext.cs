using System.Collections.Generic;
using UnityEngine;

/// <summary>시퀀서가 해석해 컨텍스트에 등록하는 런타임 Cue (프리팹/사운드/배치).</summary>
public class RuntimeCue
{
    public List<GameObject> EffectPrefabs = new List<GameObject>();
    public List<int> SoundIds = new List<int>();
    public SpawnAnchor Anchor = SpawnAnchor.CasterSocket;
    public string SocketName;
}

/// <summary>
/// 유닛 로컬 연출 실행 컨텍스트. 시퀀서가 페이즈 실행 직전 등록, 종료 시 Clear.
/// 애니 이벤트 수신자(프리젠터)가 cueName으로 조회해 실행. 중앙 매니저 아님(유닛마다 하나).
/// </summary>
[DisallowMultipleComponent]
public class PresentationRuntimeContext : MonoBehaviour
{
    public int CurrentActionInstanceId { get; private set; }
    public SkillEffectContext Current { get; private set; }
    /// <summary>등록 시점의 기대 Animator state hash(0이면 검사 안 함). 늦게 온 이벤트 방어용.</summary>
    public int ExpectedStateHash { get; private set; }

    private readonly Dictionary<string, RuntimeCue> _cues = new Dictionary<string, RuntimeCue>();

    public bool HasValidContext => CurrentActionInstanceId != 0 && Current != null;

    public void SetActive(int actionInstanceId, SkillEffectContext ctx, Dictionary<string, RuntimeCue> cues, int expectedStateHash)
    {
        CurrentActionInstanceId = actionInstanceId;
        Current = ctx;
        ExpectedStateHash = expectedStateHash;
        _cues.Clear();
        if (cues != null)
        {
            foreach (KeyValuePair<string, RuntimeCue> kv in cues)
            {
                _cues[kv.Key] = kv.Value;
            }
        }
    }

    public bool TryGetCue(string normalizedName, out RuntimeCue cue)
    {
        cue = null;
        return HasValidContext
            && !string.IsNullOrEmpty(normalizedName)
            && _cues.TryGetValue(normalizedName, out cue);
    }

    public void Clear()
    {
        CurrentActionInstanceId = 0;
        Current = null;
        ExpectedStateHash = 0;
        _cues.Clear();
    }
}
