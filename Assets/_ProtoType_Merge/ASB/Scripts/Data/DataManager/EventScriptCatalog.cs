using System.Collections.Generic;
using UnityEngine;

/// <summary>이벤트 스크립트(분기형 대사) 채널 구분. 지금은 메인/서브를 분리 캐싱한다.</summary>
public enum EventScriptChannel
{
    Main,
    Sub
}

/// <summary>
/// 이벤트 스크립트(분기형 대사) 데이터를 SO에서 로드해 Dictionary로 캐싱하는 카탈로그.
/// 순수 데이터 조회까지만 담당하며, 대사 진행 상태(현재 노드/선택 처리)는 이 카탈로그를 쓰는 쪽 책임이다.
/// 골격은 DHCsvTemplateCatalog 패턴을 따른다.
/// </summary>
[DisallowMultipleComponent]
public class EventScriptCatalog : MonoBehaviour
{
    public static EventScriptCatalog Instance { get; private set; }

    [Header("SO DataTables (Excel Importer)")]
    [SerializeField] private ChatDBEventDataTable   chatMainTable;
    [SerializeField] private ChatDBEventDataTable   chatSubTable;
    [SerializeField] private BranchDBEventDataTable branchMainTable;
    [SerializeField] private BranchDBEventDataTable branchSubTable;

    [Header("Settings")]
    [SerializeField] private bool loadOnAwake       = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    // 메인/서브는 지금 분리 유지. (이후 병합 요청 시 채널 무시 오버로드만 추가하면 됨)
    private readonly Dictionary<int, ChatDBEventData> chatMainLookup = new Dictionary<int, ChatDBEventData>();
    private readonly Dictionary<int, ChatDBEventData> chatSubLookup  = new Dictionary<int, ChatDBEventData>();

    // Branch_ID → 선택지 리스트 (Selection_Index 오름차순 정렬)
    private readonly Dictionary<int, List<BranchDBEventData>> branchMainLookup = new Dictionary<int, List<BranchDBEventData>>();
    private readonly Dictionary<int, List<BranchDBEventData>> branchSubLookup  = new Dictionary<int, List<BranchDBEventData>>();

    private bool isLoaded;

    public bool IsLoaded => isLoaded;

    // ─────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (loadOnAwake)
            Reload();
    }

    // ─────────────────────────────────────────────────────────
    // Public API (전부 채널 파라미터를 받는 순수 조회)
    // ─────────────────────────────────────────────────────────

    /// <summary>Chat_ID로 대사 노드를 조회한다.</summary>
    public bool TryGetChat(EventScriptChannel channel, int chatId, out ChatDBEventData chat)
    {
        EnsureLoaded();
        chat = null;
        if (chatId <= 0) return false;
        return GetChatLookup(channel).TryGetValue(chatId, out chat);
    }

    /// <summary>현재 노드의 Next_Chat_ID를 따라 다음 노드를 조회한다. (Next_Chat_ID가 0이면 종료 → false)</summary>
    public bool TryGetNextChat(EventScriptChannel channel, int chatId, out ChatDBEventData next)
    {
        next = null;
        if (!TryGetChat(channel, chatId, out ChatDBEventData current) || current == null)
            return false;

        if (current.Next_Chat_ID <= 0)
            return false;

        return TryGetChat(channel, current.Next_Chat_ID, out next);
    }

    /// <summary>이 노드가 선택지 분기를 가지는지 여부.</summary>
    public bool HasBranch(ChatDBEventData chat)
    {
        return chat != null && chat.Branch_Group_ID != 0;
    }

    /// <summary>Branch_Group_ID(= Branch_ID)에 해당하는 선택지 목록을 조회한다.</summary>
    public bool TryGetBranchOptions(EventScriptChannel channel, int branchGroupId,
                                    out IReadOnlyList<BranchDBEventData> options)
    {
        EnsureLoaded();
        options = null;
        if (branchGroupId == 0) return false;

        if (GetBranchLookup(channel).TryGetValue(branchGroupId, out List<BranchDBEventData> list))
        {
            options = list;
            return true;
        }
        return false;
    }

    /// <summary>선택지(Selection_Index)를 고른 결과의 Target_Talk_ID 노드를 조회한다.</summary>
    public bool TryResolveBranchTarget(EventScriptChannel channel, int branchGroupId,
                                       string selectionIndex, out ChatDBEventData target)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(selectionIndex)) return false;
        if (!TryGetBranchOptions(channel, branchGroupId, out IReadOnlyList<BranchDBEventData> options))
            return false;

        string normalized = selectionIndex.Trim();
        for (int i = 0; i < options.Count; i++)
        {
            BranchDBEventData option = options[i];
            if (option == null) continue;
            if (!string.Equals(option.Selection_Index?.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
                continue;

            // TODO(trigger): Trigger_Type/Trigger_Value 조건 판정은 미정. 지금은 조건 무관 그대로 해석한다.

            // Target_Talk_ID == 0 은 "이 선택지는 대화 종료" → 대상 없음(false).
            // 6자리 등 잘못된 ID는 데이터 오류이며, 조회 실패 시 안전하게 false 반환한다.
            if (option.Target_Talk_ID <= 0)
                return false;

            return TryGetChat(channel, option.Target_Talk_ID, out target);
        }
        return false;
    }

    /// <summary>디버그/에디터용: 해당 채널의 전체 대사 노드.</summary>
    public IReadOnlyList<ChatDBEventData> GetAllChats(EventScriptChannel channel)
    {
        EnsureLoaded();
        return new List<ChatDBEventData>(GetChatLookup(channel).Values);
    }

    // ─────────────────────────────────────────────────────────
    // 로드 / 캐싱
    // ─────────────────────────────────────────────────────────

    [ContextMenu("Reload")]
    public void Reload()
    {
        ClearCache();

        LoadChatTable(chatMainTable,   chatMainLookup, EventScriptChannel.Main);
        LoadChatTable(chatSubTable,    chatSubLookup,  EventScriptChannel.Sub);
        LoadBranchTable(branchMainTable, branchMainLookup, EventScriptChannel.Main);
        LoadBranchTable(branchSubTable,  branchSubLookup,  EventScriptChannel.Sub);

        isLoaded = true;
        Debug.Log($"[EventScriptCatalog] 로드 완료 — " +
                  $"Chat(Main {chatMainLookup.Count} / Sub {chatSubLookup.Count}), " +
                  $"Branch(Main {branchMainLookup.Count} / Sub {branchSubLookup.Count}) 그룹.", this);
    }

    private void LoadChatTable(ChatDBEventDataTable table, Dictionary<int, ChatDBEventData> lookup,
                               EventScriptChannel channel)
    {
        if (table == null)
        {
            Debug.LogWarning($"[EventScriptCatalog] Chat 테이블({channel})이 할당되지 않았습니다.", this);
            return;
        }

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChatDBEventData row = table.DataList[i];
            if (row == null) continue;

            if (lookup.ContainsKey(row.Chat_ID))
            {
                Debug.LogWarning($"[EventScriptCatalog] 중복 Chat_ID {row.Chat_ID}({channel}) 건너뜀.", this);
                continue;
            }
            lookup.Add(row.Chat_ID, row);
        }
    }

    private void LoadBranchTable(BranchDBEventDataTable table, Dictionary<int, List<BranchDBEventData>> lookup,
                                 EventScriptChannel channel)
    {
        if (table == null)
        {
            Debug.LogWarning($"[EventScriptCatalog] Branch 테이블({channel})이 할당되지 않았습니다.", this);
            return;
        }

        // Branch_ID로 선택지들을 묶는다.
        for (int i = 0; i < table.DataList.Count; i++)
        {
            BranchDBEventData row = table.DataList[i];
            if (row == null) continue;

            if (!lookup.TryGetValue(row.Branch_ID, out List<BranchDBEventData> options))
                lookup[row.Branch_ID] = options = new List<BranchDBEventData>();

            options.Add(row);
        }

        // 그룹별로 Selection_Index 오름차순 정렬. ("1", "1A", "2" …)
        foreach (var options in lookup.Values)
        {
            options.Sort((a, b) => string.Compare(
                a?.Selection_Index, b?.Selection_Index, System.StringComparison.Ordinal));
        }
    }

    // ─────────────────────────────────────────────────────────
    // 공통 유틸
    // ─────────────────────────────────────────────────────────

    private Dictionary<int, ChatDBEventData> GetChatLookup(EventScriptChannel channel)
    {
        return channel == EventScriptChannel.Main ? chatMainLookup : chatSubLookup;
    }

    private Dictionary<int, List<BranchDBEventData>> GetBranchLookup(EventScriptChannel channel)
    {
        return channel == EventScriptChannel.Main ? branchMainLookup : branchSubLookup;
    }

    private void ClearCache()
    {
        chatMainLookup.Clear();
        chatSubLookup.Clear();
        branchMainLookup.Clear();
        branchSubLookup.Clear();
        isLoaded = false;
    }

    private void EnsureLoaded()
    {
        if (!isLoaded) Reload();
    }
}
