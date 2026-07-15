using UnityEngine;

/// <summary>
/// [KJ 260714] 이벤트 오브젝트 부착용 대화 트리거 (F008). 상호작용 시 지정 Chat_ID로 대화 시작.
/// 1차는 독립 컴포넌트 — 기존 MapEventObject.Interact(DH) 경로 연결은 DH 협의 후 한 줄 훅.
/// 에디터 테스트: 플레이 중 인스펙터 우클릭 → "Start Chat (Test)".
/// </summary>
[DisallowMultipleComponent]
public class ChatEventTrigger : MonoBehaviour
{
    [Tooltip("상호작용 시 시작할 Chat_ID. 0 이하 = 비활성. (임시 테스트 데이터: 900001)")]
    [SerializeField] private int eventZoneId;
    [SerializeField] private int eventStartChatId;

    /// <summary>상호작용 진입점 — 파티 상호작용/더블클릭 등 외부 경로에서 호출.</summary>
    public void Interact()
    {
        if (eventStartChatId <= 0)
        {
            Debug.LogWarning($"[ChatEventTrigger] {name}: eventStartChatId 미설정 — 대화 생략");
            return;
        }

        int zoneId = eventZoneId > 0 ? eventZoneId : ResolveDefaultZoneId(eventStartChatId);
        ChatModalController.Show(zoneId, eventStartChatId);
    }

    [ContextMenu("Start Chat (Test)")]
    private void StartChatTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ChatEventTrigger] Start Chat (Test) can only be used in Play Mode.", this);
            return;
        }

        Interact();
    }

    private static int ResolveDefaultZoneId(int startChatId)
    {
        return startChatId >= 100000 ? startChatId / 100000 : 1;
    }
}
