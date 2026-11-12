using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyIdentity), typeof(EnemyGridMover))]
public sealed class EnemyZoneClearIndicator : MonoBehaviour
{
    [SerializeField] private GameObject indicator;

    private EnemyIdentity identity;
    private EnemyGridMover mover;
    private DHEventStateRepository eventRepository;

    private void OnEnable()
    {
        SetVisible(false);
        RefreshVisibility();
    }

    private void Start()
    {
        // 생성 직후에는 아직 배치 키와 저장 상태가 초기화되지 않았을 수 있다.
        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (eventRepository != null)
            eventRepository.FlagChanged -= HandleFlagChanged;
        eventRepository = null;
        SetVisible(false);
    }

    public void RefreshVisibility()
    {
        if (identity == null) identity = GetComponent<EnemyIdentity>();
        if (mover == null) mover = GetComponent<EnemyGridMover>();

        DHEventStateRepository repository = DHEventStateRepository.Instance;
        if (eventRepository != repository)
        {
            if (eventRepository != null)
                eventRepository.FlagChanged -= HandleFlagChanged;
            eventRepository = repository;
            if (isActiveAndEnabled && eventRepository != null)
                eventRepository.FlagChanged += HandleFlagChanged;
        }

        MapProgressRepository progress = MapProgressRepository.Instance;
        bool visible = isActiveAndEnabled && mover != null && mover.BehaviorType == EnemyBehaviorType.Mobile &&
            identity != null && progress != null &&
            progress.TryGetEnemyState(identity.PlacementKey, out EnemyWorldState state) &&
            state != null && !state.Defeated && GateThreatController.Instance != null &&
            GateThreatController.Instance.IsZoneClearedForGateOpen(state.ZoneId);
        SetVisible(visible);
    }

    private void HandleFlagChanged(string flagName, bool value)
    {
        RefreshVisibility();
    }

    private void SetVisible(bool visible)
    {
        if (indicator != null && indicator.activeSelf != visible)
            indicator.SetActive(visible);
    }
}
