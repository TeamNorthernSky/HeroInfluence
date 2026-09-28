using System.Collections.Generic;
using UnityEngine;

/// <summary>튜토리얼 협회 또는 탐사 홍보 패널의 지정 영역에 전용 카드를 생성한다.</summary>
public sealed class TutorialPublicityRoster : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private TutorialPublicityHeroCard itemPrefab;
    [SerializeField] private TutorialPublicityController controller;
    private readonly List<TutorialPublicityHeroCard> cards = new List<TutorialPublicityHeroCard>();
    private float nextRefresh;

    private void Start()
    {
        if (content == null || itemPrefab == null || controller == null) return;
        foreach (string key in controller.HeroKeys)
        {
            var card = Instantiate(itemPrefab, content);
            card.Bind(key, controller);
            card.gameObject.SetActive(true);
            cards.Add(card);
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;
        foreach (var card in cards) if (card != null) card.Refresh();
    }

    private void OnDestroy()
    {
        foreach (var card in cards) if (card != null) Destroy(card.gameObject);
    }
}
