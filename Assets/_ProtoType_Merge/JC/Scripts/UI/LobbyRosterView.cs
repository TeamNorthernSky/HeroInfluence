using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 260703] 로비 상시 로스터 "뷰 전용" 컴포넌트(경량). Layer_LobbyOverlay에 배치.
/// 책임: 파티 표시 + 클릭→히어로 인포 모달 + 진형순서 반영(읽기). 편성/드래그/선택 인프라 없음.
///
/// [JC 260703 · 결정] 이 컴포넌트는 **두 곳**에 표시용으로 쓰인다:
///   (1) 로비 상시 로스터 — Layer_LobbyOverlay.
///   (2) 출전 편성 모달 — Modal_Sortie 자식(공유 dim 위). 로비 원본을 그대로 복제해 배치.
/// 원래 계획은 (2)에 무거운 HeroListController(selection) + SortieDragItem(로스터→그리드 드래그-추가)을 두는 것이었으나,
/// **현재 기획상 파티 4명 고정 + 진형은 그리드 내 위치 재배열만 허용(로스터→그리드 드래그-추가는 dormant)** 이라
/// 표시 전용 뷰로 충분하다. 무거운 드래그-추가 인프라는 **드래그-추가 기획 재활성 시** (2)의 이 표시 뷰를
/// HeroListController(selection)로 교체하며 부착한다. (1)과 (2)는 동시 활성되지 않는다(출전 시 (1) 원본 비활성).
/// 정렬은 RosterOrdering 공용 헬퍼 사용(HeroListController와 동일 순서). 진형순서는 party.UnitIndices 순으로
/// 자동 반영(SortieController.SaveFormation이 진형 순서대로 저장하므로).
/// </summary>
[DisallowMultipleComponent]
public class LobbyRosterView : MonoBehaviour
{
    [SerializeField] private RectTransform content;         // 카드 부모(VerticalLayoutGroup 권장, ContentSizeFitter 금지)
    [SerializeField] private HeroProfileButton itemPrefab;
    [SerializeField] private HeroInfoModal infoModal;

    private readonly List<HeroProfileButton> spawned = new List<HeroProfileButton>();

    private void OnEnable()
    {
        // [JC 260703] 크로스번들 폴백 등록(SortieController가 출전 중 이 루트를 비활성).
        LobbyUIRegistry.LobbyRosterRoot = gameObject;
        Rebuild();
    }

    private void OnDisable()
    {
        Clear();
        if (LobbyUIRegistry.LobbyRosterRoot == gameObject) LobbyUIRegistry.LobbyRosterRoot = null;
    }

    public void Rebuild()
    {
        if (itemPrefab == null || content == null) return;
        Clear();
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return;
        var catalog = DHCsvTemplateCatalog.Instance;
        var visit = HQVisitState.Instance;

        // 로비 표시 = 본부 상주(방문 파티 + 무소속). visitingOnly=true로 탐사 나간 파티 제외(HeroListController 로비 기본과 동일).
        List<int> ordered = RosterOrdering.ResolveOrderedUnits(repo, visit, true);
        for (int i = 0; i < ordered.Count; i++)
        {
            int unitIndex = ordered[i];
            if (!repo.TryGetUnit(unitIndex, out UnitPersistentData unit) || unit == null) continue;
            UnitData template = null;
            if (catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
                catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out template);
            bool inParty = RosterOrdering.IsUnitInAnyParty(unitIndex);

            HeroProfileButton item = Instantiate(itemPrefab, content);
            item.gameObject.SetActive(true);
            item.Bind(unitIndex, infoModal, unit, template, inParty);  // 항상 info 모드(클릭=인포모달)
            spawned.Add(item);
        }
    }

    private void Clear()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null)
            {
                spawned[i].transform.SetParent(null, false);
                Destroy(spawned[i].gameObject);
            }
        spawned.Clear();
    }
}
