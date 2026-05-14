using System.Collections.Generic;
using UnityEngine;

// [JC 신설 260515] DHScene_3 전용 Castle wiring 후처리.
// LevelLoader.SpawnCastle 후 Castle GO에 CastleHQVisitDetector + CastleDoubleClickEntry 동적 부착 +
// VisitorIndicator 자식 GameObject 생성·연결. 인스펙터 슬롯은 본 컴포넌트에 미리 채워서 Reflection으로 주입.
// 다른 씬(DHScene/DHScene_2)에는 본 컴포넌트를 두지 않으므로 격리됨.
[DisallowMultipleComponent]
public class CastleVisitWiringForDHScene3 : MonoBehaviour
{
    [Header("Visitor Indicator (Castle 자식으로 동적 생성)")]
    [SerializeField] private Sprite visitorIndicatorSprite;
    [SerializeField] private int visitorIndicatorSortingOrder = 32000;
    // [JC 260515 보정] 사용자가 플레이모드에서 직접 맞춘 값. 카메라 방향 X 로테이션 포함.
    [SerializeField] private Vector3 visitorIndicatorLocalPosition = new Vector3(0f, 0.92f, -0.198f);
    [SerializeField] private Vector3 visitorIndicatorLocalEulerAngles = new Vector3(36.23f, 0f, 0f);
    [SerializeField] private Vector3 visitorIndicatorLocalScale = Vector3.one;

    // [JC 260515 일원화] gridManager / targetGridOffsets 슬롯 폐기. 검사 위치는 CastleUnit.IsInteractionCell 위임.
    [Header("Debug")]
    [SerializeField] private bool logWireOnce = true;

    private bool wired;

    private void LateUpdate()
    {
        if (wired) return;

        CastleUnit castle = FindFirstObjectByType<CastleUnit>();
        if (castle == null) return;

        Wire(castle.gameObject);
        wired = true;
    }

    private void Wire(GameObject castleGO)
    {
        // 1) VisitorIndicator 자식 GO 생성
        GameObject indicator = new GameObject("VisitorIndicator");
        indicator.transform.SetParent(castleGO.transform, false);
        indicator.transform.localPosition = visitorIndicatorLocalPosition;
        indicator.transform.localEulerAngles = visitorIndicatorLocalEulerAngles;
        indicator.transform.localScale = visitorIndicatorLocalScale;

        SpriteRenderer sr = indicator.AddComponent<SpriteRenderer>();
        sr.sprite = visitorIndicatorSprite;
        sr.sortingOrder = visitorIndicatorSortingOrder;

        indicator.SetActive(false);

        // 2) CastleHQVisitDetector 부착 + visitorIndicator 슬롯만 채움
        //    [JC 260515 일원화] gridManager / targetGridOffsets 슬롯 폐기. CastleUnit.IsInteractionCell 위임.
        CastleHQVisitDetector detector = castleGO.GetComponent<CastleHQVisitDetector>();
        if (detector == null)
            detector = castleGO.AddComponent<CastleHQVisitDetector>();

        SetPrivateField(detector, "visitorIndicator", indicator);

        // 즉시 재평가 (현재 mover 위치 기반 인디케이터 갱신)
        detector.ReevaluateNow();

        // 3) CastleDoubleClickEntry 부착 (인스펙터 슬롯은 default — Camera.main + threshold 0.3)
        CastleDoubleClickEntry entry = castleGO.GetComponent<CastleDoubleClickEntry>();
        if (entry == null)
            entry = castleGO.AddComponent<CastleDoubleClickEntry>();

        if (logWireOnce)
            Debug.Log($"[CastleVisitWiringForDHScene3] Wired to Castle GO '{castleGO.name}' — detector + entry + indicator", this);
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
