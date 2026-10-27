// [CHEAT_ONLY / 다른 기능 재사용 금지 — 2026-10-27 사용자 요청]
// 이 파일은 1구역 플레이테스트 진입 시간을 줄이는 치트 전용입니다.
// 치트 전용 모달/목적지 UI입니다. 정상 HUD·공통 모달·다른 기능의 UI로 연결하거나 재사용하지 마세요.
// 의존 방향은 치트 → 기존 게임 API만 허용합니다. 일반 게임 코드는 이 폴더를 참조하지 마세요.
// 공통 기능이 필요하면 기존 정식 시스템을 사용하세요. 치트 구현을 공용 코드로 승격하거나 복사하지 마세요.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JC.DebugTools
{
    // 전용 테스트 UI. 기존 게임 HUD/프리팹을 수정하지 않고 모달 스택을 함께 사용합니다.
    public sealed class JcZone1WarpView : MonoBehaviour, IPointerClickHandler
    {
        Action cancel;
        Action<Vector2> mapClick;
        RectTransform panel;
        TMP_Text status;
        bool mapMode;
        TMP_FontAsset font;

        public static JcZone1WarpView Create(Transform parent, Action cancel, Action<Vector2> click)
        {
            var go = new GameObject("JC 구간 테스트", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            go.GetComponent<Image>().color = Color.clear;
            var view = go.AddComponent<JcZone1WarpView>(); view.cancel = cancel; view.mapClick = click;
            view.font = TMP_Settings.defaultFontAsset;
            // 현재 HUD가 사용하는 한글 폰트를 우선 재사용합니다.
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                if (text.font != null && text.font.name.IndexOf("NotoSansKR", StringComparison.OrdinalIgnoreCase) >= 0)
                { view.font = text.font; break; }
            return view;
        }

        void ResetPanel(bool map)
        {
            mapMode = map;
            if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
            var go = new GameObject("선택", typeof(RectTransform), typeof(Image));
            panel = go.GetComponent<RectTransform>(); panel.SetParent(transform, false);
            panel.anchorMin = panel.anchorMax = map ? new Vector2(0, .5f) : new Vector2(.5f, .5f);
            panel.pivot = map ? new Vector2(0, .5f) : new Vector2(.5f, .5f);
            panel.anchoredPosition = map ? new Vector2(12, 0) : Vector2.zero;
            panel.sizeDelta = new Vector2(map ? 520 : 840, 980);
            go.GetComponent<Image>().color = new Color(.025f, .045f, .09f, .98f);
        }

        TMP_Text Label(string name, string value, float top, float height, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(panel, false);
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(24, -top-height); rect.offsetMax = new Vector2(-24, -top);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value;
            text.fontSize = size; text.color = Color.white; text.enableWordWrapping = true; text.raycastTarget = false;
            return text;
        }

        void Button(string text, float top, Action click)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(panel, false);
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(24, -top-56); rect.offsetMax = new Vector2(-24, -top);
            go.GetComponent<Image>().color = new Color(.08f, .25f, .42f, 1);
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var tr = label.GetComponent<RectTransform>(); tr.SetParent(rect, false); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(12, 0); tr.offsetMax = new Vector2(-12, 0);
            var tmp = label.GetComponent<TextMeshProUGUI>(); tmp.font = font; tmp.text = text; tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
            var button = go.GetComponent<Button>(); button.onClick.AddListener(() => click());
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        }

        public void ShowChoices(string title, string summary, IReadOnlyList<JcZone1WarpRoute> options, Action<JcZone1WarpRoute> choose, Action stop)
        {
            ResetPanel(false);
            Label("제목", title, 24, 55, 32);
            Label("요약", summary, 90, 370, 23);
            float top = 470;
            foreach (var route in options) { var option = route; Button(route.Label, top, () => choose(option)); top += 66; }
            Button("여기까지만 선택 → 목적지 선택", top + 12, stop);
            Button("취소 (Esc)", top + 80, cancel);
            status = Label("상태", options.Count == 0 ? "선택 가능한 미수행 이벤트가 없습니다. 현재 상태로 이동할 수 있습니다." : "현재 선택하지 않은 단계와 이후 이벤트는 맵에 남습니다.", 870, 90, 21);
        }

        public void ShowMap(string summary, Action back)
        {
            ResetPanel(true);
            Label("제목", "1구역 · 목적지 선택", 24, 65, 30);
            Label("요약", summary, 100, 450, 23);
            Label("설명", "초록색 셀을 클릭하면 적용 후 이동합니다.\n\n전투 조우·자원·이벤트·다리 트리거 셀은 선택할 수 없습니다.\n\n턴과 이동력은 소비하지 않습니다.", 570, 180, 22);
            Button("분기 선택 처음부터", 760, back);
            Button("취소 (Esc)", 826, cancel);
            status = Label("상태", "", 900, 70, 20);
        }

        public void SetStatus(string value) { if (status != null) status.text = value; }
        public void OnPointerClick(PointerEventData data)
        {
            if (!mapMode || data.button != PointerEventData.InputButton.Left ||
                RectTransformUtility.RectangleContainsScreenPoint(panel, data.position)) return;
            mapClick?.Invoke(data.position);
        }
    }
}
