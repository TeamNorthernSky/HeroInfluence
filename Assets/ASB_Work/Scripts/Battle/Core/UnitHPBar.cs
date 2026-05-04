using UnityEngine;
using UnityEngine.UI;

public class UnitHPBar : MonoBehaviour
{
    [SerializeField] private BattleCharactor battleCharactor;
    [SerializeField] private Image fillImage;

    private void Awake()
    {
        if (battleCharactor == null)
        {
            battleCharactor = GetComponentInParent<BattleCharactor>();
        }
    }

    private void OnEnable()
    {
        if (battleCharactor != null)
        {
            battleCharactor.OnHpChanged += UpdateHPBar;
            // 초기값까지 즉시 반영
            UpdateHPBar(battleCharactor.CurrentHp, battleCharactor.MaxHp);
        }
    }

    private void OnDisable()
    {
        if (battleCharactor != null)
        {
            battleCharactor.OnHpChanged -= UpdateHPBar;
        }
    }

    private void UpdateHPBar(float currentHp, float maxHp)
    {
        if (fillImage == null)
        {
            Debug.LogWarning($"[HPBar] fillImage is null on {gameObject.name}");
            return;
        }

        if (maxHp <= 0f)
        {
            Debug.LogWarning($"[HPBar] maxHp <= 0 on {gameObject.name}");
            fillImage.fillAmount = 0f;
            return;
        }

        fillImage.fillAmount = Mathf.Clamp01(currentHp / maxHp);
    }

#if UNITY_EDITOR
    [ContextMenu("머리 위 HP 바 생성")]
    private void GenerateHPBarInEditor()
    {
        if (battleCharactor == null)
        {
            battleCharactor = GetComponentInParent<BattleCharactor>();
        }

        Transform existingCanvas = transform.Find("HPCanvas");
        GameObject canvasObj = existingCanvas != null ? existingCanvas.gameObject : new GameObject("HPCanvas");
        if (existingCanvas == null)
        {
            canvasObj.transform.SetParent(transform, false);
        }

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasObj.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.WorldSpace;

        if (canvasObj.GetComponent<CanvasScaler>() == null)
        {
            canvasObj.AddComponent<CanvasScaler>();
        }

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 15f);
        canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        canvasRect.localPosition = new Vector3(0f, 2f, 0f);

        Transform existingBg = canvasObj.transform.Find("Background");
        GameObject bgObj = existingBg != null ? existingBg.gameObject : new GameObject("Background");
        if (existingBg == null)
        {
            bgObj.transform.SetParent(canvasObj.transform, false);
        }
        Image bgImage = bgObj.GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = bgObj.AddComponent<Image>();
        }
        bgImage.color = new Color(0f, 0f, 0f, 0.8f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Transform existingFill = canvasObj.transform.Find("Fill");
        GameObject fillObj = existingFill != null ? existingFill.gameObject : new GameObject("Fill");
        if (existingFill == null)
        {
            fillObj.transform.SetParent(canvasObj.transform, false);
        }
        fillImage = fillObj.GetComponent<Image>();
        if (fillImage == null)
        {
            fillImage = fillObj.AddComponent<Image>();
        }
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.color = (battleCharactor != null && battleCharactor.IsPlayer) ? Color.green : Color.red;

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        if (battleCharactor != null)
        {
            UpdateHPBar(battleCharactor.CurrentHp, battleCharactor.MaxHp);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[UnitHPBar] HP 바 UI 생성 완료! 위치(y)나 색상을 입맛에 맞게 조절하세요.");
    }
#endif
}
