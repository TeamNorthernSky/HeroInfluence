using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CombatPromptPanelController : MonoBehaviour
{
    [Serializable]
    private class AdvantageImageSlot
    {
        public CombatAdvantageState state = CombatAdvantageState.Close;
        public Image image = null;
        public Sprite activeSprite = null;
        public Sprite inactiveSprite = null;

        public void Apply(CombatAdvantageState currentState)
        {
            if (image == null)
                return;

            bool active = state == currentState;
            Sprite nextSprite = active ? activeSprite : inactiveSprite;
            if (nextSprite != null)
                image.sprite = nextSprite;

            image.enabled = nextSprite != null || image.sprite != null;
        }
    }

    [Header("References")]
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button fleeButton;
    [SerializeField] private Button skipBattleButton;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Advantage Images")]
    [SerializeField] private AdvantageImageSlot[] advantageImages;

    [Header("Combat Portraits")]
    [SerializeField] private Image[] heroPortraitImages;
    [SerializeField] private Image[] enemyPortraitImages;
    [SerializeField] private Sprite emptyHeroPortraitSprite;
    [SerializeField] private Sprite emptyEnemyPortraitSprite;

    private Action startBattleHandler;
    private Action fleeHandler;
    private Action skipBattleHandler;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (startBattleButton != null)
            startBattleButton.onClick.AddListener(HandleStartBattleClicked);

        if (fleeButton != null)
            fleeButton.onClick.AddListener(HandleFleeClicked);

        if (skipBattleButton != null)
            skipBattleButton.onClick.AddListener(HandleSkipBattleClicked);
    }

    private void OnDisable()
    {
        if (startBattleButton != null)
            startBattleButton.onClick.RemoveListener(HandleStartBattleClicked);

        if (fleeButton != null)
            fleeButton.onClick.RemoveListener(HandleFleeClicked);

        if (skipBattleButton != null)
            skipBattleButton.onClick.RemoveListener(HandleSkipBattleClicked);
    }

    public void Open(
        Action onStartBattle,
        Action onFlee,
        Action onSkipBattle,
        CombatAdvantageState advantageState,
        IReadOnlyList<int> heroUnitIndices,
        IReadOnlyList<string> enemyPortraitKeys)
    {
        startBattleHandler = onStartBattle;
        fleeHandler = onFlee;
        skipBattleHandler = onSkipBattle;
        gameObject.SetActive(true);
        PrepareInteractableState();
        SetAdvantageState(advantageState);
        SetCombatPortraits(heroUnitIndices, enemyPortraitKeys);
    }

    public void Close()
    {
        startBattleHandler = null;
        fleeHandler = null;
        skipBattleHandler = null;
        gameObject.SetActive(false);
    }

    private void HandleStartBattleClicked()
    {
        Action handler = startBattleHandler;
        startBattleHandler = null;
        handler?.Invoke();
    }

    private void HandleFleeClicked()
    {
        Action handler = fleeHandler;
        fleeHandler = null;
        handler?.Invoke();
    }

    private void HandleSkipBattleClicked()
    {
        Action handler = skipBattleHandler;
        skipBattleHandler = null;
        handler?.Invoke();
    }

    private void PrepareInteractableState()
    {
        ResolveReferences();

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.ignoreParentGroups = true;
        }

        PrepareButton(startBattleButton);
        PrepareButton(fleeButton);
        PrepareButton(skipBattleButton);
    }

    private void SetAdvantageState(CombatAdvantageState state)
    {
        if (advantageImages == null)
            return;

        for (int i = 0; i < advantageImages.Length; i++)
            advantageImages[i]?.Apply(state);
    }

    private void SetCombatPortraits(
        IReadOnlyList<int> heroUnitIndices,
        IReadOnlyList<string> enemyPortraitKeys)
    {
        ApplyPortraits(
            heroPortraitImages,
            heroUnitIndices,
            emptyHeroPortraitSprite,
            ResolveHeroPortrait);

        ApplyEnemyPortraits(
            enemyPortraitImages,
            enemyPortraitKeys,
            emptyEnemyPortraitSprite);
    }

    private static void ApplyPortraits(
        Image[] images,
        IReadOnlyList<int> unitIndices,
        Sprite emptySprite,
        Func<int, Sprite> portraitResolver)
    {
        if (images == null)
            return;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            int unitIndex = unitIndices != null && i < unitIndices.Count ? unitIndices[i] : 0;
            Sprite sprite = unitIndex > 0 && portraitResolver != null
                ? portraitResolver.Invoke(unitIndex)
                : null;

            image.sprite = sprite != null ? sprite : emptySprite;
            image.enabled = image.sprite != null;
            image.gameObject.SetActive(true);
        }
    }

    private static Sprite ResolveHeroPortrait(int unitIndex)
    {
        return unitIndex > 0 ? Sprites.Portrait.HeroByUnit(unitIndex) : null;
    }

    private static Sprite ResolveEnemyPortrait(int unitIndex)
    {
        return unitIndex > 0 ? Sprites.Portrait.Enemy(unitIndex.ToString()) : null;
    }

    private static void ApplyEnemyPortraits(
        Image[] images,
        IReadOnlyList<string> portraitKeys,
        Sprite emptySprite)
    {
        if (images == null)
            return;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            string portraitKey = portraitKeys != null && i < portraitKeys.Count
                ? portraitKeys[i]
                : string.Empty;
            Sprite sprite = !string.IsNullOrWhiteSpace(portraitKey)
                ? Sprites.Portrait.Enemy(portraitKey)
                : null;

            image.sprite = sprite != null ? sprite : emptySprite;
            image.enabled = image.sprite != null;
            image.gameObject.SetActive(true);
        }
    }

    private static void PrepareButton(Button button)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(true);
        button.enabled = true;
        button.interactable = true;
    }

    private void ResolveReferences()
    {
        Button[] buttons = null;
        if (startBattleButton == null)
        {
            buttons = GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0)
                startBattleButton = buttons[0];
        }

        if (fleeButton == null)
        {
            buttons ??= GetComponentsInChildren<Button>(true);
            if (buttons.Length > 1)
                fleeButton = buttons[1];
        }

        if (skipBattleButton == null)
        {
            buttons ??= GetComponentsInChildren<Button>(true);
            if (buttons.Length > 2)
                skipBattleButton = buttons[2];
        }

        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
