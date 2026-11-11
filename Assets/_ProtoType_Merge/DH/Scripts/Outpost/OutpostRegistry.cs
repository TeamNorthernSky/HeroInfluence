using System.Collections.Generic;
using UnityEngine;

public class OutpostRegistry : MonoBehaviour
{
    public event System.Action Changed;
    // JC | 2026-11-10 | 기존 Changed 계약은 유지하고 시각 갱신에는 변경 대상/재구축 완료/등록부 수명을 별도로 전달합니다.
    public event System.Action<Outpost> OutpostRegistered;
    public event System.Action<Outpost> OutpostUnregistered;
    public event System.Action<OutpostRegistry> Rebuilt;
    public static event System.Action<OutpostRegistry> LifecycleChanged;
    private readonly List<Outpost> outposts = new List<Outpost>();

    public IReadOnlyList<Outpost> Outposts => outposts;

    private void Awake()
    {
        RegisterExistingOutposts();
    }

    // JC | 2026-11-10 | 늦은 등록부 생성·교체도 주기적 씬 탐색 없이 구독을 다시 연결할 수 있게 알립니다.
    private void OnEnable() => LifecycleChanged?.Invoke(this);
    private void OnDisable() => LifecycleChanged?.Invoke(this);
    private void OnDestroy() => LifecycleChanged?.Invoke(this);

    public void Register(Outpost outpost)
    {
        if (outpost == null || outposts.Contains(outpost))
            return;

        outposts.Add(outpost);
        Changed?.Invoke();
        // JC | 2026-11-10 | 기존 알림 순서를 보존한 뒤 이번에 등록한 건물만 전달합니다.
        OutpostRegistered?.Invoke(outpost);
    }

    public void Unregister(Outpost outpost)
    {
        if (outpost == null)
            return;

        if (outposts.Remove(outpost))
        {
            Changed?.Invoke();
            // JC | 2026-11-10 | 해제는 비활성화일 수도 있으므로 파괴 의미로 바꾸지 않습니다.
            OutpostUnregistered?.Invoke(outpost);
        }
    }

    [ContextMenu("Rebuild Registry")]
    public void RegisterExistingOutposts()
    {
        outposts.Clear();

        Outpost[] sceneOutposts = FindObjectsByType<Outpost>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneOutposts.Length; i++)
        {
            Outpost outpost = sceneOutposts[i];
            if (outpost == null)
                continue;

            Register(outpost);
        }
        Changed?.Invoke();
        // JC | 2026-11-10 | 빈 목록으로 재구축된 경우도 완료를 알립니다. 기존 Changed 호출 횟수는 유지합니다.
        Rebuilt?.Invoke(this);
    }
}
