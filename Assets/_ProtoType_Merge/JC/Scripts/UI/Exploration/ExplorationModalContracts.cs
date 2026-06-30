using System;
using UnityEngine;

// [JC 260630] 탐사 모달 seam 계약. DH=데이터+트리거+실행위임 / JC 모달=표시+결정.
// DH 발행자가 Raise*로 요청을 올리고, JC 모달 컨트롤러가 이벤트를 구독해 표시·결정한다.

public sealed class MapEventModalRequest
{
    public string description;       // "{자원}을 'N' 지불하고\n모든 영웅의 '{효과}'"
    public string effectAmountText;  // "+N" 또는 "N 만큼 회복"
    public string costAmountText;    // 비용 수치
    public Sprite resourceIcon;
    public Sprite effectIcon;
    public bool   canAfford;         // Yes interactable 게이팅
    public Func<bool> onConfirm;     // Yes 실행 위임(=mapEvent.TryExecuteEvent(party)); 성공 true→닫기
}

public sealed class OutpostNoticeRequest
{
    public string title;             // "{이름} 해방"
    public string description;       // 다중행
    public int    amount;
    public Sprite buildingSprite;
    public Sprite resourceSprite;
}

public static class ExplorationModalEvents
{
    public static event Action<MapEventModalRequest> MapEventRequested;
    public static event Action<OutpostNoticeRequest> OutpostNoticeRequested;

    // [JC 260630] 맵이벤트 결정 모달 활성 여부 — JC 모달이 show/close 시 갱신. 거점/본부 더블클릭·우클릭 가드가 조회.
    public static bool MapEventModalActive;

    public static void RaiseMapEvent(MapEventModalRequest r) => MapEventRequested?.Invoke(r);
    public static void RaiseOutpostNotice(OutpostNoticeRequest r) => OutpostNoticeRequested?.Invoke(r);
}
