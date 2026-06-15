using ASB.Work.Battle.Core;
using TMPro;
using UnityEngine;

/// <summary>
/// 데미지·힐 팝업 UI 생성. _popupPrefab에 TextMeshProUGUI를 포함한 프리팹을 할당하세요.
/// </summary>
public class DamagePopupPresenter : MonoBehaviour
{
    [SerializeField] private GameObject _popupPrefab;
    [SerializeField] private float _popupHeightOffset = 1.5f;

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
            return;
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
            label.color = result.IsCritical ? Color.yellow : Color.red;
        }

        instance.GetComponent<DamagePopupEffect>()?.Play(result.IsCritical);
    }
}
