using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [KJ 260714] 채팅 대화 노드 (Chat DB 1행). 필드 ↔ CSV 컬럼 매핑은 specs/2026-07-11-chat-system-design.md 참조.
/// </summary>
public class ChatNode
{
    public int Id;              // Chat_ID
    public int Type;            // Chat_Type (0=상대/좌, 1=플레이어/우)
    public string CharName;     // Char_Name
    public string CharProfile;  // Char_Profile (기반 단계 미사용)
    public string Message;      // Message_Text
    public string ImageRes;     // IMG_Res (기반 단계 미사용)
    public int NextChatId;      // Next_Chat_ID (0 = 선형 진행 없음)
    public int BranchGroupId;   // Branch_Group_ID (0 = 분기 없음)
}

/// <summary>
/// [KJ 260714] 분기 선택지 (Branch DB 1행). SelectionIndex의 "A" 접미 = 조건부 변형
/// (조건 통과 시 같은 번호의 무접미 기본 항목을 대체).
/// </summary>
public class BranchOption
{
    public int BranchId;            // Branch_ID (Chat의 Branch_Group_ID와 매칭)
    public string SelectionIndex;   // "1", "2", "1A"...
    public string ConditionType;    // Trigger_Type (현재 "Flag"만)
    public string Condition;        // Trigger_Value ("Flag_X==1" / "Flag_X!=1")
    public string Text;             // Selection_Text (빈 값 = 버튼 없이 자동 진행 분기)
    public int TargetTalkId;        // Target_Talk_ID (0 = 점프 없음 → 대화 종료)
    public string Effect;           // Trigger_Effect ("Set_Flag_*" / "Start_Battle_*")
}

/// <summary>
/// [KJ 260714] 대화 데이터 카탈로그 (F008). 개념 = 방향 그래프(순환 허용),
/// 구현 = ID 딕셔너리 2개 — 엣지(Next/Target)는 ID로 지연 해석.
/// CSV 파싱은 타 인원 분담: 이 파일의 모델/공개 API가 계약이며, 로더 완성 전까지 임시 테스트 데이터로 동작.
/// </summary>
public class ChatDatabase
{
    private static ChatDatabase instance;

    /// <summary>지연 초기화. CSV 로더 도입 시 이 초기화만 LoadFromCsv로 교체.</summary>
    public static ChatDatabase Instance => instance ?? (instance = CreateWithTestData());

    private readonly Dictionary<int, ChatNode> nodes = new Dictionary<int, ChatNode>();
    private readonly Dictionary<int, List<BranchOption>> branches = new Dictionary<int, List<BranchOption>>();

    public bool TryGetNode(int chatId, out ChatNode node)
    {
        return nodes.TryGetValue(chatId, out node);
    }

    /// <summary>분기 그룹의 선택지 원본 목록(조건 미평가). 없으면 빈 목록.</summary>
    public IReadOnlyList<BranchOption> GetBranch(int branchGroupId)
    {
        return branches.TryGetValue(branchGroupId, out List<BranchOption> list)
            ? (IReadOnlyList<BranchOption>)list
            : System.Array.Empty<BranchOption>();
    }

    // ── 로더 담당 구현 지점 ─────────────────────────────────────────────
    // TODO(파싱 담당): Chat DB / Branch DB CSV(TextAsset) 2개를
    // CSVLoader.ParseCsvTotal + BuildHeaderIndex/GetFieldByHeader로 파싱해 AddNode/AddBranch로 채우는
    // 정적 팩토리를 여기에 구현. 시그니처 제안:
    //   public static ChatDatabase LoadFromCsv(TextAsset chatCsv, TextAsset branchCsv)
    // 완성 후 Instance 초기화를 CreateWithTestData() → LoadFromCsv(...)로 교체.

    /// <summary>[임시] 로더 완성 전 개발/플레이 검증용 미니 그래프 (ID 9000xx 대역).</summary>
    private static ChatDatabase CreateWithTestData()
    {
        Debug.LogWarning("[ChatDatabase] CSV 로더 미구현 — 임시 테스트 데이터(9000xx)로 동작합니다.");
        var db = new ChatDatabase();
        db.AddNode(new ChatNode { Id = 900001, Type = 0, CharName = "루미나", Message = "테스트 대화 시작. 화면을 클릭해 진행해봐.", NextChatId = 900002 });
        db.AddNode(new ChatNode { Id = 900002, Type = 1, Message = "클릭하면 다음 대사가 나온다.", NextChatId = 900003 });
        db.AddNode(new ChatNode { Id = 900003, Type = 0, CharName = "루미나", Message = "선택지를 골라봐.", NextChatId = 0, BranchGroupId = 90001 });
        db.AddNode(new ChatNode { Id = 900004, Type = 0, CharName = "루미나", Message = "플래그를 세웠어. 같은 선택지가 어떻게 바뀌었는지 다시 봐.", NextChatId = 900003 });
        db.AddNode(new ChatNode { Id = 900005, Type = 0, CharName = "루미나", Message = "조건부 선택지(1A)가 보였다면 성공. 대화 종료.", NextChatId = 0 });
        db.AddBranch(new BranchOption { BranchId = 90001, SelectionIndex = "1", Text = "플래그 세우기", TargetTalkId = 900004, Effect = "Set_Flag_ChatTest" });
        db.AddBranch(new BranchOption { BranchId = 90001, SelectionIndex = "1A", ConditionType = "Flag", Condition = "Flag_ChatTest==1", Text = "(선택지 변화 확인) 종료로 이동", TargetTalkId = 900005 });
        db.AddBranch(new BranchOption { BranchId = 90001, SelectionIndex = "2", Text = "그냥 종료", TargetTalkId = 0 });
        return db;
    }

    private void AddNode(ChatNode node)
    {
        if (node == null || node.Id <= 0) return;
        if (nodes.ContainsKey(node.Id))
            Debug.LogWarning($"[ChatDatabase] Chat_ID 중복: {node.Id} — 뒤 행으로 덮어씀");
        nodes[node.Id] = node;
    }

    private void AddBranch(BranchOption option)
    {
        if (option == null || option.BranchId <= 0) return;
        if (!branches.TryGetValue(option.BranchId, out List<BranchOption> list))
            branches[option.BranchId] = list = new List<BranchOption>();
        list.Add(option);
    }
}
