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
        [SerializeField] private bool joinedAtStart;

        public string UnitTemplateKey => string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
        public GameObject VisualObject => visualObject;
        public Vector3 LocalPosition => localPosition;
        public bool JoinedAtStart => joinedAtStart;
    }

    [SerializeField] private List<TutorialPartyUnitSlot> unitSlots = new List<TutorialPartyUnitSlot>();
    [SerializeField] private bool useTutorialProgressRepository = true;

    private readonly HashSet<string> joinedUnitKeys = new HashSet<string>(StringComparer.Ordinal);

    public bool HasAnyJoinedUnit => joinedUnitKeys.Count > 0;

    private void Awake()
    {
        InitializeFromStartSlots();
    }

    public void InitializeFromStartSlots()
    {
        joinedUnitKeys.Clear();

        TutorialProgressRepository repository = useTutorialProgressRepository
            ? TutorialProgressRepository.EnsureInstance()
            : null;

        if (repository != null && repository.HasAnyJoinedUnit)
        {
            ApplyJoinedUnits(repository.GetJoinedUnitTemplateKeys());
        }
        else
        {
            for (int i = 0; i < unitSlots.Count; i++)
            {
                TutorialPartyUnitSlot slot = unitSlots[i];
                if (slot == null)
                    continue;

                if (slot.JoinedAtStart && !string.IsNullOrWhiteSpace(slot.UnitTemplateKey))
                {
                    joinedUnitKeys.Add(slot.UnitTemplateKey);
                    repository?.SetUnitJoined(slot.UnitTemplateKey, true);
                }
            }
        }

        ApplyVisualState();
    }

    public bool JoinUnit(string unitTemplateKey)
    {
        unitTemplateKey = NormalizeKey(unitTemplateKey);
        if (string.IsNullOrWhiteSpace(unitTemplateKey))
            return false;

        if (!ContainsSlot(unitTemplateKey))
            return false;

        bool changed = joinedUnitKeys.Add(unitTemplateKey);
        if (changed && useTutorialProgressRepository)
            TutorialProgressRepository.EnsureInstance()?.SetUnitJoined(unitTemplateKey, true);

        ApplyVisualState();
        return changed;
    }

    public void ApplyJoinedUnits(IEnumerable<string> unitTemplateKeys)
    {
        joinedUnitKeys.Clear();

        if (unitTemplateKeys != null)
        {
            foreach (string unitTemplateKey in unitTemplateKeys)
            {
                string normalized = NormalizeKey(unitTemplateKey);
                if (!string.IsNullOrWhiteSpace(normalized) && ContainsSlot(normalized))
                    joinedUnitKeys.Add(normalized);
            }
        }

        ApplyVisualState();
    }

    public bool IsJoined(string unitTemplateKey)
    {
        unitTemplateKey = NormalizeKey(unitTemplateKey);
        return !string.IsNullOrWhiteSpace(unitTemplateKey) && joinedUnitKeys.Contains(unitTemplateKey);
    }

    public IReadOnlyList<string> GetJoinedUnitTemplateKeys()
    {
        List<string> result = new List<string>(joinedUnitKeys.Count);
        for (int i = 0; i < unitSlots.Count; i++)
        {
            TutorialPartyUnitSlot slot = unitSlots[i];
            if (slot == null || !joinedUnitKeys.Contains(slot.UnitTemplateKey))
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
            if (slot == null || slot.VisualObject == null)
                continue;

            bool joined = joinedUnitKeys.Contains(slot.UnitTemplateKey);
            slot.VisualObject.SetActive(joined);
            slot.VisualObject.transform.localPosition = slot.LocalPosition;
        }
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

    private static string NormalizeKey(string unitTemplateKey)
    {
        return string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
    }
}
