using UnityEngine;

// [JC 신설 260513] pausesGame Modal 스택 기반 Time.timeScale 관리.
// 어떤 모달이 OnEnable/OnDisable에서 Refresh()를 호출하면, 스택에 pausesGame 모달이 1개라도 있으면 timeScale=0, 아니면 1로 복원.
// 여러 모달이 중첩(LIFO)되어도 OnDisable 순서에 무관하게 안전.
public static class ModalPauseGate
{
    public static void Refresh()
    {
        if (HasAnyPausingModal())
        {
            if (!Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = 0f;
        }
        else
        {
            if (!Mathf.Approximately(Time.timeScale, 1f))
                Time.timeScale = 1f;
        }
    }

    private static bool HasAnyPausingModal()
    {
        // ModalRegistry 내부 스택을 직접 접근할 수 없으므로 Top부터 순회하며 확인.
        // 단순화: Top만 보거나 전수 검사. 일관성을 위해 전수 검사.
        // ModalRegistry는 Count/Top만 public — 전수 순회는 Top SetActive 토글로 모방할 수 없으므로,
        // 호출 시점 기준 ModalRegistry에 등록된 모달 중 활성+pausesGame인 것이 있는지 GameObject.FindObjectsByType로 확인.
        Modal[] all = Object.FindObjectsByType<Modal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].PausesGame && all[i].gameObject.activeInHierarchy)
                return true;
        }
        return false;
    }
}
