using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Short, noninteractive feedback above one randomly chosen exploration character.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class ExplorationActionPointBubble : MonoBehaviour
{
    private const string ResourcePath = "ExplorationActionPointBubble";
    private const string Message = "행동력이 부족해!";
    private static ExplorationActionPointBubble instance;

    [SerializeField] private RectTransform followAnchor;
    [SerializeField] private BubbleRiseEffect entrance;
    [SerializeField] private CanvasGroup bubbleGroup;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private TMP_Text messageText;
    [SerializeField, Min(0f)] private float headPadding = 0.15f;
    [SerializeField, Min(0f)] private float holdSeconds = 1.6f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;

    private Transform character;
    private Camera worldCamera;
    private Animator characterAnimator;
    private Transform head;
    private Renderer[] characterRenderers;
    private RectTransform canvasRect;
    private float elapsed;

    public static bool TryShow(PartyGridMover party)
    {
        if (!Application.isPlaying || party == null || !party.gameObject.activeInHierarchy ||
            DefeatedPartyReturnController.IsPartyWaiting(party)) return false;

        Transform chosen = ChooseCharacter(party.transform);
        Camera camera = Camera.main;
        if (chosen == null || camera == null) return false;

        if (instance == null)
        {
            var prefab = Resources.Load<ExplorationActionPointBubble>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[ActionPointBubble] Missing Resources/ExplorationActionPointBubble prefab.");
                return false;
            }
            instance = Instantiate(prefab);
        }
        instance.Present(chosen, camera);
        return true;
    }

    private static Transform ChooseCharacter(Transform partyRoot)
    {
        var candidates = new List<Transform>(4);
        foreach (var unit in partyRoot.GetComponentsInChildren<PartyUnitState>())
            if (unit.gameObject.activeInHierarchy) candidates.Add(unit.transform);
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    public void Present(Transform target, Camera camera)
    {
        if (target == null || camera == null || followAnchor == null || entrance == null ||
            bubbleGroup == null || visibility == null || messageText == null) return;

        character = target;
        worldCamera = camera;
        characterAnimator = target.GetComponentInChildren<Animator>();
        head = characterAnimator != null && characterAnimator.isHuman
            ? characterAnimator.GetBoneTransform(HumanBodyBones.Head) : null;
        characterRenderers = target.GetComponentsInChildren<Renderer>();
        canvasRect = (RectTransform)GetComponent<Canvas>().transform;
        elapsed = 0f;
        messageText.text = Message;
        visibility.interactable = visibility.blocksRaycasts = false;
        gameObject.SetActive(true);
        entrance.Play(Vector2.zero);
        FollowCharacter();
    }

    private void LateUpdate()
    {
        if (character == null || worldCamera == null || !character.gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }
        elapsed += Time.unscaledDeltaTime;
        float fadeStart = entrance.Duration + holdSeconds;
        if (elapsed >= fadeStart + fadeSeconds)
        {
            entrance.Hide();
            gameObject.SetActive(false);
            return;
        }
        if (elapsed > fadeStart)
            bubbleGroup.alpha = 1f - Mathf.Clamp01((elapsed - fadeStart) / fadeSeconds);
        FollowCharacter();
    }

    private void FollowCharacter()
    {
        Vector3 world = character.position + Vector3.up * 1.6f;
        if (head != null) world = head.position;
        else
        {
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (var renderer in characterRenderers)
            {
                if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer || renderer is LineRenderer) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (hasBounds) world = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
        Vector3 screen = worldCamera.WorldToScreenPoint(world + Vector3.up * headPadding);
        visibility.alpha = screen.z > 0f ? 1f : 0f;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local))
            followAnchor.anchoredPosition = local;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
