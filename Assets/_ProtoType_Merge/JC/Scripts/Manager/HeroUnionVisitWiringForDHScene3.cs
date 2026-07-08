using System.Collections.Generic;
using UnityEngine;

// [JC 신설 260514] DHScene_3 전용 HeroUnion wiring 후처리.
// LevelLoader.SpawnHeroUnion 후 HeroUnion GO에 HeroUnionHQVisitDetector + HeroUnionDoubleClickEntry 동적 부착 +
// VisitorIndicator 자식 GameObject 생성·연결. 인스펙터 슬롯은 본 컴포넌트에 미리 채워서 Reflection으로 주입.
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

    private void LateUpdate()
    {
        RemoveDestroyedReferences();

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

        SetPrivateField(detector, "visitorIndicator", indicator);

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

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return;
        var fi = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (fi != null) fi.SetValue(target, value);
    }
}
