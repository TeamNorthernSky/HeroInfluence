using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyComposition))]
[RequireComponent(typeof(PartyIdentity))]
public class PartyUnitBootstrap : MonoBehaviour
{
    private const string JusticeTemplateKey = "10001";
    private const string RuminaTemplateKey = "10002";
    private const string BlackBulletTemplateKey = "10003";
    private const string NekomingTemplateKey = "10004";

    [Header("Bootstrap")]
    [SerializeField] private bool populateOnStart = true;
    [SerializeField] private List<PartyUnitState> partyUnitStates = new List<PartyUnitState>();
    [SerializeField] private bool onlyWhenAllSlotsEmpty = true;

    private PartyComposition partyComposition;
    private PartyIdentity partyIdentity;

    private void Awake()
    {
        partyComposition = GetComponent<PartyComposition>();
        partyIdentity = GetComponent<PartyIdentity>();
    }

    private void Start()
    {
        if (!Application.isPlaying || !populateOnStart)
            return;

        InitializeStartingUnits();
    }

    [ContextMenu("Initialize Starting Units")]
    public void InitializeStartingUnits()
    {
        if (partyComposition == null)
            partyComposition = GetComponent<PartyComposition>();

        if (partyIdentity == null)
            partyIdentity = GetComponent<PartyIdentity>();

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        if (repository == null || partyRepository == null || partyComposition == null)
            return;

        if (!HasConfiguredUnitStates())
            CollectPartyUnitStatesFromChildren();

        if (!HasConfiguredUnitStates())
            return;

        InitializeFromUnitStates(repository, partyRepository);
    }

    [ContextMenu("Collect Party Unit States From Children")]
    public void CollectPartyUnitStatesFromChildren()
    {
        partyUnitStates.Clear();
        partyUnitStates.AddRange(GetComponentsInChildren<PartyUnitState>(true));
    }

    private bool HasConfiguredUnitStates()
    {
        for (int i = 0; i < partyUnitStates.Count; i++)
        {
            if (partyUnitStates[i] != null)
                return true;
        }

        return false;
    }

    private void InitializeFromUnitStates(PersistentUnitRepository repository, PartyPersistentRepository partyRepository)
    {
        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
        {
            Debug.LogWarning("PartyUnitBootstrap could not find a DHCsvTemplateCatalog in the scene.", this);
            return;
        }

        // [JC 수정 260512] 누적 방지 — 영속 저장소에 이미 파티가 등록되어 있으면 skip
        // Orora의 PartyComposition 슬롯 가드는 씬 컴포넌트 리셋으로 매번 빈 상태가 되어 작동하지 않음
        string partyId = partyIdentity != null ? partyIdentity.PartyId : gameObject.name;
        if (partyRepository.ContainsParty(partyId))
        {
            // [JC 추가 260513] 씬 재진입 시 PartyComposition.unitIndices가 인스펙터 직렬화값(빈)으로 리셋됨.
            // 영속 PartyPersistentData에서 인덱스를 복원해 UI/검색 로직이 정상 동작하도록 보장.
            if (partyRepository.TryGetParty(partyId, out PartyPersistentData persistentData))
            {
                System.Collections.Generic.IReadOnlyList<int> persistentIndices = persistentData.UnitIndices;
                partyComposition.EnsureSlotCount(persistentIndices.Count);
                for (int i = 0; i < persistentIndices.Count; i++)
                {
                    partyComposition.SetUnitIndexAt(i, persistentIndices[i]);
                    ApplyPersistentUnitStateAt(i, persistentIndices[i]);
                }

                ApplyExplorationUnitPositions();
            }
            return;
        }

        int slotCount = partyUnitStates.Count;
        partyComposition.EnsureSlotCount(slotCount);

        if (onlyWhenAllSlotsEmpty && !AreAllUnitSlotsEmpty(slotCount))
        {
            ApplyExplorationUnitPositions();
            return;
        }

        int registeredCount = 0;
        for (int i = 0; i < partyUnitStates.Count; i++)
        {
            PartyUnitState unitState = partyUnitStates[i];
            if (unitState == null)
                continue;

            if (partyComposition.GetUnitIndexAt(i) > 0)
                continue;

            if (string.IsNullOrWhiteSpace(unitState.UnitTemplateKey))
            {
                Debug.LogWarning($"Party unit state on '{unitState.name}' is missing a unitTemplateKey.", unitState);
                continue;
            }

            if (!templateCatalog.TryGetPlayerUnitTemplate(unitState.UnitTemplateKey, out DHPlayerUnitTemplate template))
            {
                Debug.LogWarning($"Party unit state on '{unitState.name}' could not resolve CSV template '{unitState.UnitTemplateKey}'.", unitState);
                continue;
            }

            string currentWeaponKey = unitState.CurrentWeaponKey;
            EquipmentStatBlock currentWeaponStats = default;
            if (!string.IsNullOrWhiteSpace(currentWeaponKey) &&
                templateCatalog.TryGetWeaponTemplate(currentWeaponKey, out DHWeaponTemplate weaponTemplate) &&
                weaponTemplate != null)
            {
                currentWeaponKey = weaponTemplate.WeaponKey;
                currentWeaponStats = EquipmentStatBlock.FromStatBlock(weaponTemplate.GetBonusStatsAtLevel(WeaponPersistentRepository.BaseWeaponLevel));
            }
            else if (templateCatalog.TryGetWeaponTemplate("HC001", out weaponTemplate) && weaponTemplate != null)
            {
                currentWeaponKey = weaponTemplate.WeaponKey;
                currentWeaponStats = EquipmentStatBlock.FromStatBlock(weaponTemplate.GetBonusStatsAtLevel(WeaponPersistentRepository.BaseWeaponLevel));
            }

            unitState.SetCurrentWeapon(currentWeaponKey, currentWeaponStats);

            unitState.InitializeFromTemplate(template, currentWeaponStats);

            int unitIndex = repository.CreateUnit(
                unitState.UnitTemplateKey,
                unitState.Level,
                unitState.BaseStats,
                unitState.LevelupStats,
                unitState.CurrentSkillIndex,
                unitState.CurrentWeaponKey,
                unitState.CurrentWeaponStats,
                unitState.IngameStats,
                unitState.CurrentHp);
            unitState.AssignUnitIndex(unitIndex);
            partyComposition.SetUnitIndexAt(i, unitIndex);
            registeredCount++;
        }

        partyRepository.RegisterOrUpdateParty(partyId, partyComposition.UnitIndices);
        ApplyExplorationUnitPositions();
    }

    private bool AreAllUnitSlotsEmpty(int slotCount)
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (partyComposition.GetUnitIndexAt(i) > 0)
                return false;
        }

        return true;
    }

    private void ApplyPersistentUnitStateAt(int slotIndex, int unitIndex)
    {
        if (unitIndex <= 0 || slotIndex < 0 || slotIndex >= partyUnitStates.Count)
            return;

        PartyUnitState unitState = partyUnitStates[slotIndex];
        if (unitState == null)
            return;

        unitState.AssignUnitIndex(unitIndex);
        unitState.RefreshFromRepository();
    }

    private void ApplyExplorationUnitPositions()
    {
        for (int i = 0; i < partyUnitStates.Count; i++)
        {
            PartyUnitState unitState = partyUnitStates[i];
            if (unitState == null)
                continue;

            if (TryGetExplorationLocalPosition(unitState.UnitTemplateKey, out Vector3 localPosition))
                unitState.transform.localPosition = localPosition;
        }
    }

    private static bool TryGetExplorationLocalPosition(string unitTemplateKey, out Vector3 localPosition)
    {
        switch (unitTemplateKey)
        {
            case JusticeTemplateKey:
                localPosition = new Vector3(0f, 0f, 0.3f);
                return true;
            case RuminaTemplateKey:
                localPosition = new Vector3(0f, 0f, -0.3f);
                return true;
            case BlackBulletTemplateKey:
                localPosition = new Vector3(-0.3f, 0f, 0f);
                return true;
            case NekomingTemplateKey:
                localPosition = new Vector3(0.3f, 0f, 0f);
                return true;
            default:
                localPosition = default;
                return false;
        }
    }
}
