using System;
using UnityEngine;

/// <summary>협회 시설을 건설 완료 상태로 만들고 등록된 UI를 활성화한다.</summary>
[DisallowMultipleComponent]
public class TutorialLobbyUIActivator : MonoBehaviour
{
    [Tooltip("활성화할 기능 UI를 등록합니다. Project의 프리팹 에셋이 아니라 Hierarchy의 인스턴스를 연결하세요.")]
    [SerializeField] private GameObject[] featureUIs = Array.Empty<GameObject>();

    /// <summary>모든 시설을 최소 1레벨로 건설하고 등록한 UI를 활성화한다.</summary>
    public void ActivateAllFeatures()
    {
        ActivateAllFeatures(featureUIs);
    }

    /// <summary>코드에서 UI 인스턴스 배열을 전달하여 전체 시설을 건설/활성화한다.</summary>
    public void ActivateAllFeatures(GameObject[] uiInstances)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null)
        {
            Debug.LogWarning("GameManager/HQ 초기화 후 협회 기능 활성화를 호출해주세요.", this);
            return;
        }

        // 비용/선행조건 없이 미건설 시설만 해금한다. 기존 강화 레벨은 유지한다.
        gm.HQ.DebugUnlockAllFacilities();
        if (gm.HQ.GetLevel(HQDepartment.Headquarters) < 1)
            gm.HQ.TryUpgrade(HQDepartment.Headquarters, out _, out _);

        if (uiInstances != null)
            foreach (GameObject featureUI in uiInstances)
                ActivateFeature(featureUI);

        // 재호출 시 상태 이벤트가 없어도 UI를 갱신한다. 활성화 중 등록 변경에 대비한다.
        var registered = LobbyUIRegistry.Facilities;
        var facilities = new FacilityModule[registered.Count];
        for (int i = 0; i < facilities.Length; i++) facilities[i] = registered[i];
        foreach (var facility in facilities) RefreshFacility(facility);

        // 건설 모드에서는 클릭이 업그레이드로 연결되므로 일반 사용 모드로 돌아간다.
        if (LobbyUIRegistry.BuildMode != null)
            LobbyUIRegistry.BuildMode.HandleEscape();
    }

    private static void RefreshFacility(FacilityModule facility)
    {
        if (facility == null) return;
        facility.Refresh();
        facility.SetLockedInteractive(false);
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null || gm.HQ.GetLevel(facility.Department) < 1) return;
        if (facility.UnlockedButton != null)
        {
            ActivateHierarchy(facility.UnlockedButton.gameObject);
            facility.UnlockedButton.enabled = true;
            facility.UnlockedButton.interactable = true;
        }
    }

    private static void ActivateHierarchy(GameObject target)
    {
        if (target.transform.parent != null && !target.transform.parent.gameObject.activeInHierarchy)
            ActivateHierarchy(target.transform.parent.gameObject);
        target.SetActive(true);
    }

    /// <summary>인자로 받은 기능 UI 인스턴스를 활성화한다.</summary>
    public void ActivateFeature(GameObject featureUI)
    {
        if (featureUI == null)
            return;

        if (!featureUI.scene.IsValid())
        {
            Debug.LogWarning("기능 UI에는 프리팹 에셋 대신 씬에 생성된 인스턴스를 전달해주세요.", this);
            return;
        }

        // 자식 팝업/잠금 표시의 상태는 각 UI 컨트롤러가 관리한다.
        ActivateHierarchy(featureUI);
        foreach (var facility in featureUI.GetComponentsInChildren<FacilityModule>(true))
        {
            ActivateHierarchy(facility.gameObject);
            facility.enabled = true;
            RefreshFacility(facility);
        }
    }

    /// <summary>Inspector 목록의 인덱스(0부터 시작)로 기능 하나를 활성화한다.</summary>
    public void ActivateFeatureAt(int index)
    {
        if (featureUIs == null || index < 0 || index >= featureUIs.Length)
        {
            Debug.LogWarning($"기능 UI 인덱스가 범위를 벗어났습니다: {index}", this);
            return;
        }

        ActivateFeature(featureUIs[index]);
    }
}
