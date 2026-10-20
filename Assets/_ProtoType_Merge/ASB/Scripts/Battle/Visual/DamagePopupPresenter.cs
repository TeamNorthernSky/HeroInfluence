using ASB.Work.Battle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 데미지·힐 팝업 UI 생성. _popupPrefab에 TextMeshProUGUI를 포함한 프리팹을 할당하세요.
/// </summary>
public class DamagePopupPresenter : MonoBehaviour
{
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
    [Tooltip("실제 감소가 적용된 피해에 사용하는 기존 피해 경감 아이콘입니다.")]
    [SerializeField] private Sprite _reducedIcon;
    [Tooltip("치명타와 경감이 동시에 적용될 때 치명타 아이콘을 추가 표시합니다.")]
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
        var display = instance.GetComponent<DamagePopupPresenter>();
        if (display != null)
        {
            if (display._healIcon != null) display._healIcon.SetActive(result.IsHeal && !result.IsMiss);
            if (display._resultIcon != null)
            {
                display._resultIcon.sprite = result.DamageReduced ? _reducedIcon : result.IsCritical ? _criticalIcon : _damageIcon;
                display._resultIcon.enabled = !result.IsMiss && !result.IsHeal && display._resultIcon.sprite != null;
            }
            if (display._secondaryIcon != null)
            {
                display._secondaryIcon.sprite = _criticalIcon;
                display._secondaryIcon.enabled = result.DamageReduced && result.IsCritical && !result.IsHeal && !result.IsMiss;
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

        if (display != null && label != null) display.CenterNumberOverTarget(label);
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
    // 실제 숫자의 중심을 월드 앵커에 맞춥니다. 아이콘은 숫자 너비에 맞춰 왼쪽에 붙입니다.
    private void CenterNumberOverTarget(TextMeshProUGUI label)
    {
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchoredPosition = Vector2.zero;
        float width = label.GetPreferredValues(label.text).x;
        float iconX = string.IsNullOrEmpty(label.text) ? 0 : -(width * .5f + 5f);
        if (_resultIcon != null) _resultIcon.rectTransform.anchoredPosition = new Vector2(iconX, 0);
        if (_healIcon != null) ((RectTransform)_healIcon.transform).anchoredPosition = new Vector2(iconX, 0);
        if (_secondaryIcon != null) _secondaryIcon.rectTransform.anchoredPosition = new Vector2(iconX - 9f, 0);
    }

}
