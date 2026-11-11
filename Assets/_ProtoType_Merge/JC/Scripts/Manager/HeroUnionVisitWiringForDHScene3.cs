using System.Collections.Generic;
using UnityEngine;

// [JC 신설 260514] DHScene_3 전용 HeroUnion wiring 후처리.
// LevelLoader.SpawnHeroUnion 후 HeroUnion GO에 HeroUnionHQVisitDetector + HeroUnionDoubleClickEntry 동적 부착 +
// JC | 2026-11-10 | VisitorIndicator 자식 생성 후 명시적 ConfigureIndicator로 표시·연출 참조를 연결합니다.
// 다른 씬(DHScene/DHScene_2)에는 본 컴포넌트를 두지 않으므로 격리됨.
[DisallowMultipleComponent]
public class HeroUnionVisitWiringForDHScene3 : MonoBehaviour
{
    [Header("Visitor Indicator (HeroUnion 자식으로 동적 생성)")]
    [SerializeField] private Sprite visitorIndicatorSprite;
    [SerializeField] private int visitorIndicatorSortingOrder = 32000;
    // [JC 260514 보정] 사용자가 플레이모드에서 직접 맞춘 값. 카메라 방향 X 로테이션 포함.
    [SerializeField] private Vector3 visitorIndicatorLocalPosition = new Vector3(0f, 0.92f, -0.198f);
    [SerializeField] private Vector3 visitorIndicatorLocalEulerAngles = new Vector3(36.23f, 0f, 0f);
    [SerializeField] private Vector3 visitorIndicatorLocalScale = Vector3.one;

    // [JC 260618] 거점 인디케이터(OutpostVisitIndicator)가 동일 스프라이트·배치를 재사용하도록 노출.
    //   컴포넌트는 씬에 항상 존재하므로(wire 타이밍 무관) 거점이 안전하게 읽을 수 있다.
    public Sprite VisitorIndicatorSprite => visitorIndicatorSprite;
    public int VisitorIndicatorSortingOrder => visitorIndicatorSortingOrder;
    public Vector3 VisitorIndicatorLocalPosition => visitorIndicatorLocalPosition;
    public Vector3 VisitorIndicatorLocalEulerAngles => visitorIndicatorLocalEulerAngles;
    public Vector3 VisitorIndicatorLocalScale => visitorIndicatorLocalScale;

    // [JC 260514 일원화] gridManager / targetGridOffsets 슬롯 폐기. 검사 위치는 HeroUnionUnit.IsInteractionCell 위임.
    [Header("Debug")]
    [SerializeField] private bool logWireOnce = true;

    private readonly HashSet<HeroUnionUnit> wiredHeroUnions = new HashSet<HeroUnionUnit>();

    // JC | 2026-11-10 | 메인 탐사씬은 기존 DH 등록부를 직접 연결하여 매 프레임 전체 씬 검색을 없앱니다.
    // 제작 씬에는 이 연결을 추가하지 않아 기존 정적 표시·연결 경로를 유지합니다.
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private JcAssociationIndicatorController indicatorAnimation;

    private void LateUpdate()
    {
        RemoveDestroyedReferences();

        if (heroUnionRegistry != null)
        {
            var registered = heroUnionRegistry.HeroUnions;
            for (int i = 0; i < registered.Count; i++)
            {
                var heroUnion = registered[i];
                if (heroUnion == null || wiredHeroUnions.Contains(heroUnion)) continue;
                Wire(heroUnion.gameObject);
                wiredHeroUnions.Add(heroUnion);
            }
            return;
        }

        HeroUnionUnit[] heroUnions = FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < heroUnions.Length; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (heroUnion == null || wiredHeroUnions.Contains(heroUnion))
                continue;

            Wire(heroUnion.gameObject);
            wiredHeroUnions.Add(heroUnion);
        }
    }

    private void Wire(GameObject heroUnionGO)
    {
        // 1) VisitorIndicator 자식 GO 생성
        Transform existingIndicator = heroUnionGO.transform.Find("VisitorIndicator");
        GameObject indicator = existingIndicator != null
            ? existingIndicator.gameObject
            : new GameObject("VisitorIndicator");
        indicator.transform.SetParent(heroUnionGO.transform, false);
        indicator.transform.localPosition = visitorIndicatorLocalPosition;
        indicator.transform.localEulerAngles = visitorIndicatorLocalEulerAngles;
        indicator.transform.localScale = visitorIndicatorLocalScale;

        SpriteRenderer sr = indicator.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = indicator.AddComponent<SpriteRenderer>();
        sr.sprite = visitorIndicatorSprite;
        sr.sortingOrder = visitorIndicatorSortingOrder;

        indicator.SetActive(false);

        // 2) HeroUnionHQVisitDetector 부착 + visitorIndicator 슬롯만 채움
        //    [JC 260514 일원화] gridManager / targetGridOffsets 슬롯 폐기. HeroUnionUnit.IsInteractionCell 위임.
        HeroUnionHQVisitDetector detector = heroUnionGO.GetComponent<HeroUnionHQVisitDetector>();
        if (detector == null)
            detector = heroUnionGO.AddComponent<HeroUnionHQVisitDetector>();

        // JC | 2026-11-10 | 협회에만 연출기를 연결합니다. 거점이 재사용하는 스프라이트/배치 Getter는 바꾸지 않습니다.
        JcAssociationIndicatorMotion motion = null;
        if (indicatorAnimation != null)
        {
            motion = indicator.GetComponent<JcAssociationIndicatorMotion>();
            if (motion == null) motion = indicator.AddComponent<JcAssociationIndicatorMotion>();
            motion.Initialize(sr, indicatorAnimation);
        }
        detector.ConfigureIndicator(indicator, motion, partyRegistry);

        // 즉시 재평가 (현재 mover 위치 기반 인디케이터 갱신)
        detector.ReevaluateNow();

        // 3) HeroUnionDoubleClickEntry 부착 (인스펙터 슬롯은 default — Camera.main + threshold 0.3)
        HeroUnionDoubleClickEntry entry = heroUnionGO.GetComponent<HeroUnionDoubleClickEntry>();
        if (entry == null)
            entry = heroUnionGO.AddComponent<HeroUnionDoubleClickEntry>();

        if (logWireOnce)
            Debug.Log($"[HeroUnionVisitWiringForDHScene3] Wired to HeroUnion GO '{heroUnionGO.name}' — detector + entry + indicator", this);
    }

    private void RemoveDestroyedReferences()
    {
        if (wiredHeroUnions.Count == 0)
            return;

        List<HeroUnionUnit> destroyed = null;
        foreach (HeroUnionUnit heroUnion in wiredHeroUnions)
        {
            if (heroUnion != null)
                continue;

            destroyed ??= new List<HeroUnionUnit>();
            destroyed.Add(heroUnion);
        }

        if (destroyed == null)
            return;

        for (int i = 0; i < destroyed.Count; i++)
            wiredHeroUnions.Remove(destroyed[i]);
    }

}
