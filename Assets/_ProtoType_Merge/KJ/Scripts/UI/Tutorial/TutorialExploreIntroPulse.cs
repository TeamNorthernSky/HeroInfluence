using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>탐사 첫 진입 시 설정한 시간 동안 이동 안내와 테두리 파동을 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class TutorialExploreIntroPulse : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform card;
    [SerializeField] private Image pulseImage;
    [Header("크기")]
    [InspectorName("안내판 크기"), SerializeField] private Vector2 cardSize = new Vector2(620f, 150f);
    [Tooltip("안내판의 각 변에서 바깥으로 퍼질 수 있는 최대 거리입니다.")]
    [InspectorName("최대 확산 거리"), SerializeField, Min(0f)] private float spread = 65f;
    [Tooltip("최대 범위에 도달하기 직전에 부드럽게 사라지는 구간입니다.")]
    [InspectorName("경계 흐림 폭"), SerializeField, Min(0f)] private float boundaryFade = 12f;
    [Header("파동")]
    [Tooltip("한 묶음에서 차례로 나오는 테두리 개수입니다.")]
    [InspectorName("파동 개수"), SerializeField, Range(1, 32)] private int waveCount = 3;
    [Tooltip("같은 묶음 안에서 다음 테두리가 출발하기까지의 시간(초)입니다.")]
    [InspectorName("파동 간격 (초)"), SerializeField, Min(0.01f)] private float waveInterval = 0.35f;
    [Tooltip("테두리가 1초 동안 바깥으로 이동하는 거리입니다. 클수록 빠릅니다.")]
    [InspectorName("확산 속도 (거리/초)"), SerializeField, Min(0.01f)] private float waveSpeed = 72.22222f;
    [Tooltip("파동 묶음의 시작부터 다음 묶음 시작까지의 시간(초). 이전 묶음이 끝나기보다 짧으면 종료까지 기다립니다.")]
    [InspectorName("반복 주기 (초)"), SerializeField, Min(0.01f)] private float cyclePeriod = 2f;
    [Header("재생 시간")]
    [Tooltip("켜면 시간 제한 없이 반복합니다. 끄면 아래 재생 시간까지만 반복합니다.")]
    [InspectorName("Loop (계속 반복)"), SerializeField] private bool loop;
    [Tooltip("Loop가 꺼져 있을 때만 적용됩니다. 이 시간이 되면 안내와 효과가 사라집니다. 0이면 표시하지 않습니다.")]
    [InspectorName("재생 시간 (초)"), SerializeField, Min(0f)] private float playDuration = 2f;
    private const string SeenKey = "KJ.Tutorial.Explore.IntroPulse";
    private Material instanceMaterial;
    private Material sourceMaterial;

    private IEnumerator Start()
    {
        if (group == null || card == null || pulseImage == null) yield break;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        if (gameObject.scene.name != "TutorialExploreScene") yield break;
        var context = CombatContext.Instance;
        bool returningFromCombat = context != null && context.IsTutorial &&
            context.ReturnSceneName == gameObject.scene.name;
        yield return null;
        var progress = TutorialProgressRepository.EnsureInstance();
        if (returningFromCombat || progress == null || progress.IsMessageSeen(SeenKey)) yield break;
        progress.MarkMessageSeen(SeenKey);

        float elapsed = 0f;
        while (loop || elapsed < playDuration)
        {
            SetPreviewTime(elapsed);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        group.alpha = 0f;
    }

    /// <summary>진행 데이터를 바꾸지 않고 에디터에서 연출의 특정 시점을 확인한다.</summary>
    public void SetPreviewTime(float elapsed)
    {
        if (group == null || card == null || pulseImage == null) return;
        if (instanceMaterial == null)
        {
            sourceMaterial = pulseImage.material;
            instanceMaterial = new Material(sourceMaterial) { hideFlags = HideFlags.HideAndDontSave };
            pulseImage.material = instanceMaterial;
        }
        float fadeIn = loop ? 0.12f : Mathf.Min(0.12f, playDuration * 0.5f);
        float fadeOut = Mathf.Min(0.25f, playDuration * 0.5f);
        group.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, fadeIn));
        if (!loop)
            group.alpha *= Mathf.Clamp01((playDuration - elapsed) / Mathf.Max(0.001f, fadeOut));
        // 표시 영역은 고정하고 확산·테두리·범위 밖 제거는 셰이더에서 처리한다.
        card.sizeDelta = cardSize;
        Vector2 drawingSize = cardSize + Vector2.one * (spread * 2f + 4f);
        pulseImage.rectTransform.sizeDelta = drawingSize;
        float waveDuration = spread / Mathf.Max(0.01f, waveSpeed);
        float effectivePeriod = Mathf.Max(cyclePeriod, waveDuration + waveInterval * (waveCount - 1), 0.01f);
        float cycleTime = elapsed < 0f ? -1f : Mathf.Repeat(elapsed, effectivePeriod);
        UpdateMaterial(instanceMaterial, cycleTime, cardSize, drawingSize);
        // Mask가 생성한 스텐실 재질에도 현재 시각을 전달한다.
        Material renderingMaterial = pulseImage.materialForRendering;
        if (renderingMaterial != instanceMaterial)
            UpdateMaterial(renderingMaterial, cycleTime, cardSize, drawingSize);
    }

    private void UpdateMaterial(Material material, float elapsed, Vector2 cardSize, Vector2 drawingSize)
    {
        material.SetFloat("_Elapsed", elapsed);
        material.SetVector("_CardSize", new Vector4(cardSize.x, cardSize.y, 0f, 0f));
        material.SetVector("_RectSize", new Vector4(drawingSize.x, drawingSize.y, 0f, 0f));
        material.SetFloat("_WaveSpeed", waveSpeed);
        material.SetInt("_WaveCount", waveCount);
        material.SetFloat("_WaveInterval", waveInterval);
        material.SetFloat("_Spread", spread);
        material.SetFloat("_BoundaryFade", boundaryFade);
    }

    private void OnDestroy()
    {
        if (instanceMaterial == null) return;
        if (pulseImage != null) pulseImage.material = sourceMaterial;
        if (Application.isPlaying) Destroy(instanceMaterial);
        else DestroyImmediate(instanceMaterial);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (group != null) group.alpha = 0f;
    }

    private void OnValidate()
    {
        cardSize.x = Mathf.Max(1f, cardSize.x);
        cardSize.y = Mathf.Max(1f, cardSize.y);
        spread = Mathf.Max(0f, spread);
        boundaryFade = Mathf.Max(0f, boundaryFade);
        waveCount = Mathf.Clamp(waveCount, 1, 32);
        waveInterval = Mathf.Max(0.01f, waveInterval);
        waveSpeed = Mathf.Max(0.01f, waveSpeed);
        cyclePeriod = Mathf.Max(0.01f, cyclePeriod);
        playDuration = Mathf.Max(0f, playDuration);
    }
}
