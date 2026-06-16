using PrimeTween;
using TMPro;
using UnityEngine;

/// <summary>
/// 데미지 팝업 연출 컴포넌트.
/// DamagePopupPresenter가 프리팹을 Instantiate하면 Start에서 자동 실행됩니다.
/// 프리팹 루트에 CanvasGroup, TextMeshProUGUI를 붙여두세요.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DamagePopupEffect : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float _riseHeight = 1.2f;
    [SerializeField] private float _riseDuration = 0.5f;
    [SerializeField] private Ease _riseEase = Ease.OutCubic;

    [Header("Fade")]
    [SerializeField] private float _holdDuration = 0.3f;
    [SerializeField] private float _fadeDuration = 0.3f;

    [Header("Scale Punch (크리티컬 등 강조)")]
    [SerializeField] private float _punchScale = 1.4f;
    [SerializeField] private float _punchDuration = 0.12f;

    private CanvasGroup _canvasGroup;
    private bool _isCritical;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    private void LateUpdate()
    {
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }

    /// <summary>DamagePopupPresenter에서 텍스트 설정 후 호출합니다.</summary>
    public void Play(bool isCritical = false)
    {
        _isCritical = isCritical;
        StartCoroutine(PlayRoutine());
    }

    private System.Collections.IEnumerator PlayRoutine()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * _riseHeight;

        // 크리티컬이면 스케일 펀치
        if (_isCritical)
        {
            transform.localScale = Vector3.one;
            yield return Tween.Scale(transform, _punchScale, _punchDuration, _riseEase)
                .ToYieldInstruction();
            Tween.Scale(transform, 1f, _punchDuration * 0.5f);
        }

        // 위로 이동 (홀드 시간 포함)
        Tween.Position(transform, endPos, _riseDuration + _holdDuration, _riseEase);

        // 홀드 후 페이드 아웃
        yield return Tween.Delay(_holdDuration + _riseDuration * 0.4f).ToYieldInstruction();
        yield return Tween.Alpha(_canvasGroup, 0f, _fadeDuration).ToYieldInstruction();

        Destroy(gameObject);
    }
}
