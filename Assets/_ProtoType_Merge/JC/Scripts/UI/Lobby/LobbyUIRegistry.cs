using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260628] HQLobby UI 번들 간 교차참조 단일 출처(정적). ModalRegistry 패턴 준용.
/// 도메인 리로드 시 SubsystemRegistration 훅으로 클리어. 등록=각 번들 OnEnable, 조회=이벤트 시점(지연).
/// </summary>
public static class LobbyUIRegistry
{
    private static readonly List<FacilityModule> facilities = new List<FacilityModule>();
    public static IReadOnlyList<FacilityModule> Facilities => facilities;

    public static void RegisterFacility(FacilityModule m)
    {
        if (m != null && !facilities.Contains(m)) facilities.Add(m);
    }
    public static void UnregisterFacility(FacilityModule m)
    {
        facilities.Remove(m);
    }

    // 단일 서비스 슬롯 (지연 해석용)
    public static SortieController Sortie { get; set; }
    public static HeroListController Roster { get; set; }
    public static RectTransform RosterPanelRoot { get; set; }
    // [JC 260703] GoButton 슬롯 제거 — 출전 열기버튼이 UI_Sortie 번들 내부로 이동해 SortieController가 직접 결선(sortieButtonRoot). GoButtonRegistrar도 삭제.
    public static Button HqButton { get; set; }
    public static HQBuildModeController BuildMode { get; set; }
    public static GameObject LobbyRosterRoot { get; set; } // [JC 260703] 로비 로스터 뷰 루트(출전 중 SortieController가 비활성 토글)
    // [KJ 260728] 시설 모달이 영웅 선택을 위임하는 로비 로스터 뷰. 등록=LobbyRosterView.OnEnable.
    public static LobbyRosterView RosterView { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Clear()
    {
        facilities.Clear();
        Sortie = null; Roster = null; RosterPanelRoot = null;
        HqButton = null; BuildMode = null; LobbyRosterRoot = null;
        RosterView = null;
    }
}
