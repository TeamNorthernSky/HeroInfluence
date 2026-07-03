using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// [JC 260702] 모달 통합 매니저 (영속 DDOL). 구 ModalRegistry 흡수(스택 + OnStackChanged) + 공유 dim 구동.
/// dim = 코드 생성 단일 Image 1장을 top 모달의 부모로 co-locate(형제 바로 아래)해 z-order 자동 정합 → 캔버스 종속 없음.
/// 스택 API는 정적(모달 등록이 Instance 수명에 의존하지 않도록). dim 구동은 Instance가 OnStackChanged 구독으로 담당.
/// </summary>
[DisallowMultipleComponent]
public class ModalManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // 정적 스택 (구 ModalRegistry 흡수)
    // ─────────────────────────────────────────────
    private static readonly List<GameObject> stack = new List<GameObject>();

    /// <summary>스택(열림/닫힘)이 바뀔 때마다 발행. dim 구동자·기타 구독자용.</summary>
    public static event Action OnStackChanged;

    public static int Count => stack.Count;
    public static bool HasAny => stack.Count > 0;
    public static GameObject Top => stack.Count > 0 ? stack[stack.Count - 1] : null;

    public static void Register(GameObject modal)
    {
        if (modal == null) return;
        stack.Remove(modal);
        stack.Add(modal);
        OnStackChanged?.Invoke();
    }

    public static void Unregister(GameObject modal)
    {
        if (modal == null) return;
        if (stack.Remove(modal))
            OnStackChanged?.Invoke();
    }

    public static void CloseTop()
    {
        var top = Top;
        if (top != null) top.SetActive(false); // Modal.OnDisable → Unregister
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        stack.Clear();
        OnStackChanged = null;
    }

    // ─────────────────────────────────────────────
    // 인스턴스: DDOL + dim 구동
    // ─────────────────────────────────────────────
    public static ModalManager Instance { get; private set; }

    private const float DefaultDimAlpha = 0.75f;

    private GameObject dimGo;
    private RectTransform dimRt;
    private Image dimImage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("ModalManager");
        DontDestroyOnLoad(go);
        go.AddComponent<ModalManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);

        EnsureDim();
        OnStackChanged += HandleStackChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        HandleStackChanged();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        OnStackChanged -= HandleStackChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    // 씬 언로드로 dim이 파괴됐거나(씬 계층에 co-locate된 상태), 영속 모달이 Top으로 남은 경우 재슬롯.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureDim();
        HandleStackChanged();
    }

    private void EnsureDim()
    {
        if (dimGo != null) return;
        dimGo = new GameObject("ModalDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dimRt = dimGo.GetComponent<RectTransform>();
        dimImage = dimGo.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, DefaultDimAlpha);
        dimImage.raycastTarget = true;
        dimGo.AddComponent<ModalDimClickCloser>();
        dimGo.transform.SetParent(transform, false);
        dimGo.SetActive(false);
    }

    // 스택 top 모달 뒤(형제 바로 아래)로 dim을 co-locate. wantsDim=false거나 모달 없으면 주차.
    private void HandleStackChanged()
    {
        EnsureDim();

        GameObject top = Top;
        Modal modal = top != null ? top.GetComponent<Modal>() : null;
        Transform parent = top != null ? top.transform.parent : null;

        if (top == null || modal == null || !modal.WantsDim || parent == null)
        {
            Park();
            return;
        }

        dimGo.transform.SetParent(parent, false);
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        dimRt.localScale = Vector3.one;
        // [JC 260702] dim을 top 바로 아래(뒤)에 확정 배치.
        // 주의: SetSiblingIndex(top.index) 단독은 버그 — dim이 이미 같은 부모에 있고 top보다 아래
        // 인덱스일 때(2번째 모달이 같은 레이어에 열릴 때) dim이 top '위'로 올라가 히어로선택 패널을 덮음.
        // → dim을 맨 앞으로 올린 뒤 top을 다시 맨 앞으로 = dim이 top 직전 형제로 확정(인덱스 산술·SetParent 모호성 무관).
        dimGo.transform.SetAsLastSibling();
        top.transform.SetAsLastSibling();

        Color c = dimImage.color;
        c.a = modal.DimAlpha;
        dimImage.color = c;

        dimGo.SetActive(true);
    }

    private void Park()
    {
        if (dimGo == null) return;
        dimGo.SetActive(false);
        // [JC 260703] 재부모화(→ModalManager) 생략. Park는 Modal.OnDisable에서도 호출되는데, 이때 부모 레이어(Layer_Modals)가
        // 활성/비활성 전환 중이면 SetParent가 "Cannot set the parent ... while activating/deactivating" 에러를 낸다.
        // dim은 비활성만 하고 현 위치에 둔다 — 다음 dim-모달 오픈 시 HandleStackChanged가 올바른 레이어로 재슬롯하고,
        // 씬 언로드로 파괴되면 OnSceneLoaded/EnsureDim가 재생성한다. (재부모화는 불필요한 최적화였고 이 에러의 원인.)
    }
}
