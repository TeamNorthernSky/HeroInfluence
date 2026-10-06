using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitHPBar : MonoBehaviour
{
    [Tooltip("HP와 IP 변경 이벤트를 받을 유닛입니다. 비어 있으면 부모에서 찾습니다.")]
    [SerializeField] private BattleCharactor battleCharactor;
    [Tooltip("HP 비율을 왼쪽부터 채우는 이미지입니다.")]
    [SerializeField] private Image hpFillImage;
    [Tooltip("IP 비율을 왼쪽부터 채우는 이미지입니다.")]
    [SerializeField] private Image ipFillImage;
    [Tooltip("카메라에 정렬할 머리 위 게이지 전체입니다.")]
    [SerializeField] private RectTransform hpBarRect;
    [Tooltip("HP와 IP를 그리는 월드 공간 Canvas입니다.")]
    [SerializeField] private Canvas hpCanvas;
    [Tooltip("IP 프레임·채움·숫자를 포함하는 행입니다. IP 미표시 시 함께 숨깁니다.")]
    [SerializeField] private RectTransform ipBarRect;
    [Tooltip("HP와 별도 Canvas를 사용하는 기존 IP 표시의 선택적 참조입니다.")]
    [SerializeField] private Canvas ipCanvas;
    [Tooltip("정렬 기준 카메라입니다. 비어 있거나 꺼져 있으면 Main Camera를 사용합니다.")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("유닛 원점 위 기준 높이(월드 단위)입니다. 모델 크기에 맞춰 프리팹에 저장합니다.")]
    [Min(0f)] [SerializeField] private float anchorHeight = 2f;
    [Tooltip("공용 설정이 없는 씬에서 사용할 화면 여백입니다. 1080px 기준입니다. JC_BattleUI_VFX/UnitBars 설정이 있으면 그 값을 사용합니다.")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 24f);
    [Tooltip("공용 설정이 없는 씬의 1080px 기준 가로 폭입니다. 0이면 월드 크기 유지. JC_BattleUI_VFX/UnitBars 설정이 있으면 그 값을 사용합니다.")]
    [Min(0f)] [SerializeField] private float screenWidth = 170f;
    [Tooltip("IP 행 표시 여부입니다. 최대 IP가 0 이하일 때는 켜져 있어도 숨깁니다. HP 전용 적에는 끕니다.")]
    [SerializeField] private bool showInfluence = true;
    [Tooltip("사망 시 진행 중인 HP 소모 연출이 끝나면 게이지를 숨깁니다. 유닛 제거를 지연하지 않으며 부활하면 다시 표시합니다.")]
    [SerializeField] private bool hideWhenDead = true;
    [Tooltip("현재 HP / 최대 HP 텍스트입니다.")]
    [SerializeField] private TMP_Text hpGaugeText;
    [Tooltip("현재 IP / 최대 IP 텍스트입니다.")]
    [SerializeField] private TMP_Text ipGaugeText;
    [Tooltip("대신 맞기 버프가 있을 때만 표시하는 텍스트 배지입니다.")]
    [SerializeField] private TMP_Text guardBadge;
    [Tooltip("기준 높이에 더하는 월드 Y 보정값입니다. 기존 설정 호환용이며 0이면 추가 보정하지 않습니다.")]
    public float YaxisValue = 0f;

    private BattleCharactor subscribedCharacter;
    private bool ipVisible;
    private Image hpLossImage;
    private Image ipLossImage;
    private RectTransform hpRow;
    private BattleUnitBarVisualSettings visualSettings;
    private readonly LossState hpLoss = new LossState();
    private readonly LossState ipLoss = new LossState();
    private Vector3 appliedLayout = new Vector3(-1f, -1f, -1f);

    // 실제 수치와 별도로 변화 구간 끝점만 보관한다. 전투 상태는 변경하지 않는다.
    private sealed class LossState
    {
        public float Target, Display, Elapsed;
        public bool Active;
        public bool IsRecovery;
        public float Base => start;
        private float start, previousCurrent, previousMax, progress;
        private bool initialized;

        public void Reset() { initialized = false; Active = false; IsRecovery = false; }

        public void Set(float current, float maximum, BattleUnitBarVisualSettings.LossMotion motion,
            BattleUnitBarVisualSettings.LossMotion recovery)
        {
            float target = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            if (!initialized || maximum <= 0f || !Mathf.Approximately(maximum, previousMax))
            {
                Display = target; Active = false; IsRecovery = false;
            }
            else if (current > previousCurrent)
            {
                start = Target;
                Display = start;
                Elapsed = progress = 0f;
                IsRecovery = true;
                Active = recovery.duration > 0f && target > start;
                if (!Active) Display = target;
            }
            else if (current < previousCurrent)
            {
                start = IsRecovery ? Target : Display;
                Display = start;
                IsRecovery = false;
                Elapsed = progress = 0f;
                Active = motion.duration > 0f && start > target;
                if (!Active) Display = target;
            }
            Target = target;
            previousCurrent = current; previousMax = maximum; initialized = true;
        }

        public void Advance(float delta, BattleUnitBarVisualSettings.LossMotion motion)
        {
            if (!Active) return;
            Elapsed += Mathf.Max(0f, delta);
            float t = motion.duration > 0f ? Mathf.Clamp01(Elapsed / motion.duration) : 1f;
            // 사용자 곡선이나 실행 중 설정 변경으로 소모 구간이 다시 늘어나지 않게 한다.
            progress = Mathf.Max(progress, motion.Evaluate(t));
            Display = Mathf.Lerp(start, Target, progress);
            if (t >= 1f) { Display = Target; Active = false; }
        }
    }

    private void Awake() => ResolveReferences();

    private void OnEnable()
    {
        ResolveReferences();
        hpLoss.Reset(); ipLoss.Reset();
        RefreshSettings();
        ApplyLayout();
        Unsubscribe();
        if (battleCharactor != null)
        {
            subscribedCharacter = battleCharactor;
            subscribedCharacter.OnHpChanged += UpdateHPBar;
            subscribedCharacter.OnInfluenceChanged += UpdateIPBar;
            subscribedCharacter.OnStatusEffectsChanged += RefreshGuardBadge;
            UpdateHPBar(battleCharactor.CurrentHp, battleCharactor.MaxHp);
            UpdateIPBar(battleCharactor.CurrentInfluence, battleCharactor.MaxInfluence);
        }
        RefreshGuardBadge();
        UpdateScreenPosition();
    }

    private void OnDisable()
    {
        Unsubscribe();
        hpLoss.Reset(); ipLoss.Reset();
        RenderLosses();
        SetVisible(false);
    }

    private void Unsubscribe()
    {
        if (subscribedCharacter == null) return;
        subscribedCharacter.OnHpChanged -= UpdateHPBar;
        subscribedCharacter.OnInfluenceChanged -= UpdateIPBar;
        subscribedCharacter.OnStatusEffectsChanged -= RefreshGuardBadge;
        subscribedCharacter = null;
    }

    private void RefreshGuardBadge()
    {
        if (guardBadge != null)
            guardBadge.gameObject.SetActive(battleCharactor != null && battleCharactor.GuardSource != null);
    }

    private void LateUpdate()
    {
        RefreshSettings();
        ApplyLayout();
        AdvanceLosses(Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f);
        UpdateScreenPosition();
    }

    private void RefreshSettings() => visualSettings = BattleUnitBarVisualSettings.ForScene(gameObject.scene);
    private BattleUnitBarVisualSettings.LossMotion HpMotion =>
        visualSettings != null && visualSettings.hpLoss != null ? visualSettings.hpLoss : BattleUnitBarVisualSettings.DefaultHpLoss;
    private BattleUnitBarVisualSettings.LossMotion IpMotion =>
        visualSettings != null && visualSettings.ipLoss != null ? visualSettings.ipLoss : BattleUnitBarVisualSettings.DefaultIpLoss;
    private BattleUnitBarVisualSettings.LossMotion HpRecovery =>
        visualSettings != null && visualSettings.hpRecovery != null ? visualSettings.hpRecovery : BattleUnitBarVisualSettings.DefaultHpRecovery;
    private BattleUnitBarVisualSettings.LossMotion IpRecovery =>
        visualSettings != null && visualSettings.ipRecovery != null ? visualSettings.ipRecovery : BattleUnitBarVisualSettings.DefaultIpRecovery;

    private void AdvanceLosses(float delta)
    {
        hpLoss.Advance(delta, hpLoss.IsRecovery ? HpRecovery : HpMotion);
        ipLoss.Advance(delta, ipLoss.IsRecovery ? IpRecovery : IpMotion);
        RenderLosses();
    }

    private void RenderLosses()
    {
        if (hpFillImage != null) hpFillImage.fillAmount = hpLoss.Active && hpLoss.IsRecovery && hpLossImage != null ? hpLoss.Base : hpLoss.Target;
        if (ipFillImage != null) ipFillImage.fillAmount = ipLoss.Active && ipLoss.IsRecovery && ipLossImage != null ? ipLoss.Base : ipLoss.Target;
        if (hpLossImage != null)
        {
            hpLossImage.enabled = hpLoss.Active;
            hpLossImage.fillAmount = hpLoss.Display;
            var colors = visualSettings != null && visualSettings.hpLossColors != null
                ? visualSettings.hpLossColors : BattleUnitBarVisualSettings.DefaultHpColors;
            if (hpLoss.IsRecovery) colors = visualSettings != null && visualSettings.hpRecoveryColors != null
                ? visualSettings.hpRecoveryColors : BattleUnitBarVisualSettings.DefaultHpRecoveryColors;
            var motion = hpLoss.IsRecovery ? HpRecovery : HpMotion;
            hpLossImage.color = colors.Evaluate(motion.duration > 0f ? Mathf.Clamp01(hpLoss.Elapsed / motion.duration) : 1f);
        }
        if (ipLossImage != null)
        {
            ipLossImage.enabled = ipLoss.Active && ipVisible;
            ipLossImage.fillAmount = ipLoss.Display;
            ipLossImage.color = visualSettings != null ? visualSettings.ipLossColor : Color.white;
            if (ipLoss.IsRecovery)
            {
                var colors = visualSettings != null && visualSettings.ipRecoveryColors != null
                    ? visualSettings.ipRecoveryColors : BattleUnitBarVisualSettings.DefaultIpRecoveryColors;
                ipLossImage.color = colors.Evaluate(IpRecovery.duration > 0f ? Mathf.Clamp01(ipLoss.Elapsed / IpRecovery.duration) : 1f);
            }
        }
    }

    private void ApplyLayout()
    {
        if (visualSettings == null || hpBarRect == null) return;
        var layout = new Vector3(Mathf.Max(1f, visualSettings.rowHeight),
            Mathf.Max(0f, visualSettings.rowGap), Mathf.Max(1f, visualSettings.fontSize));
        if (layout == appliedLayout) return;
        appliedLayout = layout;
        hpBarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layout.x * 2f + layout.y);
        LayoutRow(hpRow, (layout.x + layout.y) * 0.5f, layout.x, layout.z);
        LayoutRow(ipBarRect, -(layout.x + layout.y) * 0.5f, layout.x, layout.z);
    }

    private static void LayoutRow(RectTransform row, float y, float height, float fontSize)
    {
        if (row == null) return;
        row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        var position = row.anchoredPosition; position.y = y; row.anchoredPosition = position;
        for (int i = 0; i < row.childCount; i++)
        {
            var child = row.GetChild(i) as RectTransform;
            if (child == null) continue;
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (child.TryGetComponent<TMP_Text>(out var label)) label.fontSize = fontSize;
        }
    }

    private void UpdateHPBar(float currentHp, float maxHp)
    {
        RefreshSettings();
        hpLoss.Set(currentHp, maxHp, HpMotion, HpRecovery);
        if (hpFillImage != null)
            hpFillImage.fillAmount = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        if (hpGaugeText != null)
            hpGaugeText.text = maxHp > 0f
                ? $"{Mathf.CeilToInt(Mathf.Max(0f, currentHp))} / {Mathf.CeilToInt(maxHp)}"
                : "0 / 0";
        RenderLosses();
    }

    private void UpdateIPBar(float currentIP, float maxIP)
    {
        RefreshSettings();
        ipLoss.Set(currentIP, maxIP, IpMotion, IpRecovery);
        if (ipFillImage != null)
            ipFillImage.fillAmount = maxIP > 0f ? Mathf.Clamp01(currentIP / maxIP) : 0f;
        if (ipGaugeText != null)
            ipGaugeText.text = maxIP > 0f
                ? $"{(int)Mathf.Max(0f, currentIP)} / {(int)maxIP}"
                : "0 / 0";
        ipVisible = showInfluence && maxIP > 0f;
        if (ipBarRect != null && ipBarRect != hpBarRect)
            ipBarRect.gameObject.SetActive(ipVisible);
        else
        {
            if (ipFillImage != null) ipFillImage.enabled = ipVisible;
            if (ipGaugeText != null) ipGaugeText.enabled = ipVisible;
        }
        RenderLosses();
    }

    private void UpdateScreenPosition()
    {
        if (battleCharactor == null || hpBarRect == null || battleCharactor.MaxHp <= 0f ||
            (hideWhenDead && battleCharactor.IsDead && !(hpLoss.Active && hpLossImage != null)))
        {
            SetVisible(false);
            return;
        }
        if (targetCamera == null || !targetCamera.isActiveAndEnabled) targetCamera = Camera.main;
        if (targetCamera == null)
        {
            SetVisible(false);
            return;
        }
        Vector3 anchor = battleCharactor.transform.position + Vector3.up * (anchorHeight + YaxisValue);
        Vector3 screenPosition = targetCamera.WorldToScreenPoint(anchor);
        if (screenPosition.z <= targetCamera.nearClipPlane || screenPosition.z >= targetCamera.farClipPlane)
        {
            SetVisible(false);
            return;
        }
        float resolutionScale = targetCamera.pixelHeight / 1080f;
        Vector2 offset = visualSettings != null ? visualSettings.screenOffset : screenOffset;
        float width = visualSettings != null ? Mathf.Max(1f, visualSettings.screenWidth) : screenWidth;
        screenPosition.x += offset.x * resolutionScale;
        screenPosition.y += offset.y * resolutionScale;
        hpBarRect.SetPositionAndRotation(targetCamera.ScreenToWorldPoint(screenPosition), targetCamera.transform.rotation);
        if (width > 0f && hpBarRect.rect.width > 0f)
        {
            Vector3 right = screenPosition + Vector3.right * (width * resolutionScale);
            float scale = Vector3.Distance(targetCamera.ScreenToWorldPoint(right), hpBarRect.position) / hpBarRect.rect.width;
            Vector3 parentScale = hpBarRect.parent != null ? hpBarRect.parent.lossyScale : Vector3.one;
            hpBarRect.localScale = new Vector3(
                scale / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
                scale / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
                scale / Mathf.Max(Mathf.Abs(parentScale.z), 0.0001f));
        }
        if (hpCanvas != null) hpCanvas.worldCamera = targetCamera;
        SetVisible(true);
    }

    private void ResolveReferences()
    {
        if (battleCharactor == null) battleCharactor = GetComponentInParent<BattleCharactor>(true);
        if (hpBarRect == null)
        {
            hpBarRect = transform.Find("HPCanvas") as RectTransform;
            if (hpBarRect == null) hpBarRect = transform as RectTransform;
        }
        if (hpBarRect == null) return;
        if (hpCanvas == null) hpCanvas = hpBarRect.GetComponent<Canvas>();
        hpRow = hpBarRect.Find("HP Background") as RectTransform;
        hpLossImage = Find<Image>(hpBarRect, "HP Background/Loss");
        ipLossImage = Find<Image>(hpBarRect, "IP Background/Loss");
        if (hpFillImage == null) hpFillImage = Find<Image>(hpBarRect, "HP Background/HP Fill", "HP Fill", "Fill");
        if (ipFillImage == null) ipFillImage = Find<Image>(hpBarRect, "IP Background/IP Fill", "IP Fill", "IPFill");
        if (hpGaugeText == null) hpGaugeText = Find<TMP_Text>(hpBarRect, "HP Background/HP GaugeText", "HP GaugeText");
        if (ipGaugeText == null) ipGaugeText = Find<TMP_Text>(hpBarRect, "IP Background/IP GaugeText", "IP GaugeText");
        if (guardBadge == null) guardBadge = Find<TMP_Text>(hpBarRect, "Guard Badge");
        if (ipBarRect == null) ipBarRect = hpBarRect.Find("IP Background") as RectTransform;
        if (ipBarRect == null) ipBarRect = transform.Find("IPCanvas") as RectTransform;
        if (ipCanvas == null && ipBarRect != null) ipCanvas = ipBarRect.GetComponent<Canvas>();
    }

    private static T Find<T>(Transform root, params string[] paths) where T : Component
    {
        foreach (string path in paths)
        {
            Transform child = root.Find(path);
            if (child != null && child.TryGetComponent<T>(out var component)) return component;
        }
        return null;
    }

    private void SetVisible(bool isVisible)
    {
        // 부활/초기화 시 유닛이 모든 Canvas를 켜더라도 숨긴 바가 다시 노출되지 않게 한다.
        if (hpBarRect != null && hpBarRect != transform && hpBarRect.IsChildOf(transform))
            hpBarRect.gameObject.SetActive(isVisible);
        if (hpCanvas != null) hpCanvas.enabled = isVisible;
        else if (hpFillImage != null) hpFillImage.enabled = isVisible;
        if (ipCanvas != null && ipCanvas != hpCanvas) ipCanvas.enabled = isVisible && ipVisible;
    }

#if UNITY_EDITOR
    [ContextMenu("머리 위 HP/IP 바 생성 또는 교체 (공용 전투 디자인)")]
    private void GenerateHPBarInEditor()
    {
        if (Application.isPlaying) return;
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_ProtoType_Merge/JC/BattleUI/UnitBars/HPCanvas_Battle.prefab");
        if (prefab == null) return;
        UnityEditor.Undo.RecordObject(this, "머리 위 HP/IP 바 교체");
        Transform previous = transform.Find("HPCanvas");
        if (previous != null) UnityEditor.Undo.DestroyObjectImmediate(previous.gameObject);
        var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
        UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "머리 위 HP/IP 바 생성");
        instance.name = "HPCanvas";
        hpBarRect = (RectTransform)instance.transform;
        hpCanvas = instance.GetComponent<Canvas>();
        hpFillImage = null; ipFillImage = null; hpGaugeText = null; ipGaugeText = null;
        ipBarRect = null; ipCanvas = null;
        appliedLayout = new Vector3(-1f, -1f, -1f);
        hpLoss.Reset(); ipLoss.Reset();
        ResolveReferences();
        if (battleCharactor != null)
        {
            showInfluence = battleCharactor.TeamType == TeamType.Player;
            UpdateHPBar(battleCharactor.CurrentHp, battleCharactor.MaxHp);
            UpdateIPBar(battleCharactor.CurrentInfluence, battleCharactor.MaxInfluence);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
