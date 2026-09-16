using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyGridMover))]
[RequireComponent(typeof(TutorialPartyComposition))]
public class TutorialPartyRuntime : MonoBehaviour
{
    [SerializeField] private TutorialPartyComposition composition;
    [SerializeField] private PartyGridMover gridMover;

    public PartyGridMover GridMover => gridMover;
    public TutorialPartyComposition Composition => composition;
    public bool HasAnyJoinedUnit => composition != null && composition.HasAnyJoinedUnit;

    private void Awake()
    {
        if (composition == null)
            composition = GetComponent<TutorialPartyComposition>();

        if (gridMover == null)
            gridMover = GetComponent<PartyGridMover>();

        composition?.InitializePartyVisuals();
    }

    [System.Obsolete("Tutorial parties now start with every configured unit. This method is kept only for old scene hooks.")]
    public bool JoinUnit(string unitTemplateKey)
    {
        return composition != null && composition.JoinUnit(unitTemplateKey);
    }

    public IReadOnlyList<string> GetJoinedUnitTemplateKeys()
    {
        return composition != null
            ? composition.GetJoinedUnitTemplateKeys()
            : System.Array.Empty<string>();
    }
}
