using System;
using UnityEngine;

/// <summary>
/// 튜토리얼 UI 한 항목(콘텐츠 데이터). flow는 key로 참조해 표시한다.
/// 필드 성격: 기능(key/viewId/message/highlightActionId) vs 오버뷰 메타(purpose/whenNote/requiredActionNote/displayOrder).
/// 오버뷰 메타는 사람이 읽는 라벨이며 런타임 판정에 쓰지 않는다(판정은 flow 코드).
/// </summary>
[Serializable]
public sealed class TutorialUiEntry
{
    public string key;                 // (기능) 시트 내 유일. 예: "intro"
    public int displayOrder;           // (오버뷰) 정렬용. 런타임 순서 아님
    public string purpose;             // (오버뷰) 용도. 예: "입장 안내"
    public string whenNote;            // (오버뷰) 언제(라벨). 예: "전투 입장 시"
    public string viewId = "guide";    // (기능) 표시 뷰. 현재 "guide"만 지원
    [TextArea] public string message;  // (기능) UI 문구(string.Format 대상 가능)
    public string highlightActionId;   // (기능) 강조/누를 버튼 actionId(비면 강조 없음)
    public string requiredActionNote;  // (오버뷰) 진행에 필요한 입력 '설명'. 판정은 코드
}
