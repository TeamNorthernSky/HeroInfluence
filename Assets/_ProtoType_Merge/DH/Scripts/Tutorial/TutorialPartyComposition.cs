using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TutorialPartyComposition : MonoBehaviour
{
    [Serializable]
    public class TutorialPartyUnitSlot
    {
        [SerializeField] private string unitTemplateKey;
        [SerializeField] private GameObject visualObject;
        [SerializeField] private Vector3 localPosition;

        public string UnitTemplateKey => string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
        public GameObject VisualObject => visualObject;
        public Vector3 LocalPosition => localPosition;

        public void SetVisualObject(GameObject value)
        {
            visualObject = value;
        }
    }

    [SerializeField] private List<TutorialPartyUnitSlot> unitSlots = new List<TutorialPartyUnitSlot>();
    [SerializeField] private Transform unitRoot;
    [SerializeField] private LevelPrefabRegistry prefabRegistry;

    public bool HasAnyJoinedUnit => HasAnyConfiguredUnitSlot();

    private void Awake()
    {
        ResolveReferences();
        InitializePartyVisuals();
    }

    private void Start()
    {
        ApplyVisualState();
    }

    public void InitializePartyVisuals()
    {
        ApplyVisualState();
    }

    [Obsolete("Tutorial parties now start with every configured unit. This method is kept only for old scene hooks.")]
    public bool JoinUnit(string unitTemplateKey)
    {
        unitTemplateKey = NormalizeKey(unitTemplateKey);
        ApplyVisualState();
        return !string.IsNullOrWhiteSpace(unitTemplateKey) && ContainsSlot(unitTemplateKey);
    }

    [Obsolete("Tutorial parties now start with every configured unit. This method is kept only for old scene hooks.")]
    public void ApplyJoinedUnits(IEnumerable<string> unitTemplateKeys)
    {
        ApplyVisualState();
    }

    public bool IsJoined(string unitTemplateKey)
    {
        unitTemplateKey = NormalizeKey(unitTemplateKey);
        return !string.IsNullOrWhiteSpace(unitTemplateKey) && ContainsSlot(unitTemplateKey);
    }

    public IReadOnlyList<string> GetJoinedUnitTemplateKeys()
    {
        List<string> result = new List<string>(unitSlots.Count);
        for (int i = 0; i < unitSlots.Count; i++)
        {
            TutorialPartyUnitSlot slot = unitSlots[i];
            if (slot == null || string.IsNullOrWhiteSpace(slot.UnitTemplateKey))
                continue;

            result.Add(slot.UnitTemplateKey);
        }

        return result;
    }

    public void ApplyVisualState()
    {
        for (int i = 0; i < unitSlots.Count; i++)
        {
            TutorialPartyUnitSlot slot = unitSlots[i];
            if (slot == null)
                continue;

            GameObject visualObject = ResolveOrCreateVisual(slot);
            if (visualObject == null)
                continue;

            visualObject.SetActive(true);
            visualObject.transform.localPosition = slot.LocalPosition;
        }
    }

    private GameObject ResolveOrCreateVisual(TutorialPartyUnitSlot slot)
    {
        if (slot.VisualObject != null)
            return slot.VisualObject;

        string unitTemplateKey = slot.UnitTemplateKey;
        if (string.IsNullOrWhiteSpace(unitTemplateKey))
            return null;

        ResolveReferences();
        if (prefabRegistry == null ||
            !prefabRegistry.TryGetTutorialHeroPrefab(unitTemplateKey, out GameObject prefab) ||
            prefab == null)
        {
            Debug.LogWarning($"[TutorialPartyComposition] Tutorial hero prefab was not found. unitTemplateKey={unitTemplateKey}", this);
            return null;
        }

        Transform parent = unitRoot != null ? unitRoot : transform;
        GameObject instance = Instantiate(prefab, parent, false);
        instance.name = $"{unitTemplateKey}_Tutorial";
        instance.transform.localPosition = slot.LocalPosition;

        TutorialUnitState tutorialUnitState = instance.GetComponent<TutorialUnitState>();
        if (tutorialUnitState != null)
            tutorialUnitState.InitializeFromTutorialState();

        slot.SetVisualObject(instance);
        RefreshMovementVisualController();
        return instance;
    }

    private void RefreshMovementVisualController()
    {
        PartyMovementVisualController movementVisualController = GetComponent<PartyMovementVisualController>();
        if (movementVisualController != null)
            movementVisualController.CollectUnitTurnControllers();
    }

    private void ResolveReferences()
    {
        if (unitRoot == null)
        {
            Transform units = transform.Find("UnitRoot");
            if (units == null)
                units = transform.Find("Units");

            unitRoot = units != null ? units : transform;
        }

        if (prefabRegistry == null)
            prefabRegistry = FindFirstObjectByType<LevelPrefabRegistry>();
    }

    private bool ContainsSlot(string unitTemplateKey)
    {
        for (int i = 0; i < unitSlots.Count; i++)
        {
            TutorialPartyUnitSlot slot = unitSlots[i];
            if (slot != null && string.Equals(slot.UnitTemplateKey, unitTemplateKey, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool HasAnyConfiguredUnitSlot()
    {
        for (int i = 0; i < unitSlots.Count; i++)
        {
            TutorialPartyUnitSlot slot = unitSlots[i];
            if (slot != null && !string.IsNullOrWhiteSpace(slot.UnitTemplateKey))
                return true;
        }

        return false;
    }

    private static string NormalizeKey(string unitTemplateKey)
    {
        return string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
    }
}
