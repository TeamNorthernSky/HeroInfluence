using ASB.Work.Battle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 데미지·힐 팝업 UI 생성. _popupPrefab에 TextMeshProUGUI를 포함한 프리팹을 할당하세요.
/// </summary>
public class DamagePopupPresenter : MonoBehaviour
{
    private const float NormalDamageIconScale = .65f;
    private static Sprite healCrossSprite;
    [SerializeField] private GameObject _popupPrefab;
    [SerializeField] private float _popupHeightOffset = 1.5f;
    [Tooltip("피해 숫자 앞의 아이콘입니다. 프리팹 안의 Image를 연결합니다.")]
    [SerializeField] private Image _resultIcon;
    [Tooltip("회복 시 표시할 녹색 십자가 묶음입니다. 프리팹 안에 배치합니다.")]
    [SerializeField] private GameObject _healIcon;
    [Tooltip("일반 피해에 사용하는 기존 공격력 아이콘입니다.")]
    [SerializeField] private Sprite _damageIcon;
    [Tooltip("치명타 피해에 사용하는 기존 크리티컬 아이콘입니다.")]
    [SerializeField] private Sprite _criticalIcon;
    [Tooltip("반격자 위에 표시할 기존 반격 아이콘입니다.")]
    [SerializeField] private Sprite _counterIcon;
    [Tooltip("후열의 비치명타 피격에서 확률적 50% 피해경감이 발동했을 때 표시합니다. 전열 보정과 스킬 피해 감소는 포함하지 않습니다.")]
    [SerializeField] private Sprite _reducedIcon;
    [Tooltip("기존 프리팹 참조를 보존하는 보조 아이콘입니다. 치명타와 경감은 함께 표시하지 않으므로 비활성화합니다.")]
    [SerializeField] private Image _secondaryIcon;

    public void Show(BattleHitResult result)
    {
        if (_popupPrefab == null || result?.Target == null)
        {
            return;
        }

        var profile = result.Target.GetComponent<UnitVisualProfile>();
        Vector3 spawnPos = profile?.DamagePopupSocket != null
            ? profile.DamagePopupSocket.position
            : result.Target.transform.position + Vector3.up * _popupHeightOffset;

        GameObject instance = Instantiate(_popupPrefab, spawnPos, Quaternion.identity);
        TextMeshProUGUI label = instance.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
        {
            Destroy(instance);
            return;
        }
        bool normalDamage = !result.IsCritical && !result.DamageReduced && !result.IsHeal && !result.IsMiss;
        var display = instance.GetComponent<DamagePopupPresenter>();
        if (display != null)
        {
            if (display._healIcon != null)
            {
                display._healIcon.SetActive(result.IsHeal && !result.IsMiss);
                if (result.IsHeal && !result.IsMiss) display.PrepareHealCross();
            }
            if (display._resultIcon != null)
            {
                display._resultIcon.sprite = result.DamageReduced ? _reducedIcon : result.IsCritical ? _criticalIcon : _damageIcon;
                // 칼 실루엣만 축소합니다. 치명타·경감·회복 아이콘과 숫자 배치는 유지합니다.
                if (normalDamage)
                    display._resultIcon.rectTransform.localScale *= NormalDamageIconScale;
                display._resultIcon.enabled = !result.IsMiss && !result.IsHeal && display._resultIcon.sprite != null;
            }
            if (display._secondaryIcon != null)
            {
                display._secondaryIcon.sprite = _criticalIcon;
                display._secondaryIcon.enabled = false;
            }
        }

        if (result.IsMiss)
        {
            label.text = "MISS";
            label.color = Color.white;
        }
        else if (result.IsHeal)
        {
            label.text = $"+{result.Damage:F0}";
            label.color = Color.green;
        }
        else
        {
            label.text = result.IsCritical ? $"<b>{result.Damage:F0}!</b>" : $"{result.Damage:F0}";
            label.color = result.DamageReduced ? new Color(.25f,.55f,1f) : result.IsCritical ? new Color(1f, .48f, .08f) : new Color(1f, .16f, .12f);
        }

        if (display != null && label != null) display.CenterNumberOverTarget(label, normalDamage ? NormalDamageIconScale : 1f);
        var effect = instance.GetComponent<DamagePopupEffect>();
        if (effect != null) { effect.Follow(result.Target.transform); effect.Play(result.IsCritical); }
    }

    public void ShowCounter(BattleCharactor actor)
    {
        if (_popupPrefab == null || actor == null || _counterIcon == null) return;
        var profile = actor.GetComponent<UnitVisualProfile>();
        var pos = profile != null && profile.DamagePopupSocket != null ? profile.DamagePopupSocket.position : actor.transform.position + Vector3.up * _popupHeightOffset;
        var instance = Instantiate(_popupPrefab, pos, Quaternion.identity);
        var display = instance.GetComponent<DamagePopupPresenter>();
        var label = instance.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = string.Empty;
        if (display != null)
        {
            if (display._healIcon != null) display._healIcon.SetActive(false);
            if (display._secondaryIcon != null) display._secondaryIcon.enabled = false;
            if (display._resultIcon != null)
            {
                display._resultIcon.sprite = _counterIcon;
                display._resultIcon.enabled = true;
                display._resultIcon.rectTransform.anchoredPosition = Vector2.zero;
            }
        }
        if (display != null && label != null) display.CenterNumberOverTarget(label);
        var effect = instance.GetComponent<DamagePopupEffect>();
        if (effect != null) { effect.Follow(actor.transform); effect.Play(); }
    }
    private void PrepareHealCross()
    {
        // 기존 막대 Image 하나를 재사용하고, 겹침 없는 단일 알파 실루엣에만 외곽선을 적용합니다.
        var images = _healIcon.GetComponentsInChildren<Image>(true);
        if (images.Length == 0) return;
        if (healCrossSprite == null)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Heal cross silhouette", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs((x + .5f) / size - .5f);
                float dy = Mathf.Abs((y + .5f) / size - .5f);
                // 기존 녹색 영역 7×2.3을 8×8 아이콘 안에서 합집합으로 만듭니다.
                bool inside = (dx < 3.5f / 8 && dy < 1.15f / 8) || (dx < 1.15f / 8 && dy < 3.5f / 8);
                pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            healCrossSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect);
            healCrossSprite.name = "Heal cross silhouette";
            healCrossSprite.hideFlags = HideFlags.HideAndDontSave;
        }
        var image = images[0];
        foreach (var part in images) part.gameObject.SetActive(part == image);
        image.sprite = healCrossSprite;
        image.color = new Color(.12f, .85f, .22f, 1);
        image.type = Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(8, 8);
        rect.localScale = Vector3.one;
        foreach (var shadow in image.GetComponents<Shadow>()) shadow.enabled = false;
    }
    // 실제 숫자의 중심을 월드 앵커에 맞춥니다. 아이콘은 숫자 너비에 맞춰 왼쪽에 붙입니다.
    private void CenterNumberOverTarget(TextMeshProUGUI label, float iconSpacingScale = 1f)
    {
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchoredPosition = Vector2.zero;
        float width = label.GetPreferredValues(label.text).x;
        // 숫자 폭은 유지하고 숫자 왼쪽 끝에서 아이콘 중심까지의 거리만 크기에 맞춰 줄입니다.
        float iconX = string.IsNullOrEmpty(label.text) ? 0 : -(width * .5f + 5f * iconSpacingScale);
        if (_resultIcon != null) _resultIcon.rectTransform.anchoredPosition = new Vector2(iconX, 0);
        if (_healIcon != null) ((RectTransform)_healIcon.transform).anchoredPosition = new Vector2(iconX, 0);
        if (_secondaryIcon != null) _secondaryIcon.rectTransform.anchoredPosition = new Vector2(iconX - 9f, 0);
    }

}
