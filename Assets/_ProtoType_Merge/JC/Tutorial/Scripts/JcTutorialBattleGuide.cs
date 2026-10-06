using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JC.Tutorial
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100), AddComponentMenu("JC Tutorial/전투 안내")]
    public sealed class JcTutorialBattleGuide : MonoBehaviour
    {
        [Tooltip("기존 전투 튜토리얼의 단계별 활성 상태를 읽습니다.")]
        public TutorialBattleUI source;
        [Tooltip("기존 단계별 본문입니다. TutorialBattleUI의 순서와 일치시킵니다.")]
        public GameObject[] stepTexts;
        [Tooltip("각 단계의 기존 진행 버튼입니다. 기존 클릭 이벤트를 유지합니다.")]
        public Button[] continueButtons;
        [Tooltip("결과 설명 이후 강조할 기존 확인 버튼입니다.")]
        public RectTransform resultAccept;
        [Tooltip("기존 안내 패널과 강조 표현입니다.")]
        public JcTutorialGuideView view;
        [Tooltip("단계별 제목입니다. 결과 설명 본문은 기존 Text 오브젝트에서 수정합니다.")]
        public string[] headings = { "전투 · 행동 안내", "전투 · 지원 안내", "전투 · 결과 확인", "전투 안내" };
        [Tooltip("전투 결과 설명의 크기와 화면 중앙으로부터의 위치입니다.")]
        public Vector2 resultPanelSize = new Vector2(660, 250);
        public Vector2 resultPanelPosition = Vector2.zero;
        [Tooltip("기존 전체 화면 입력 차단막입니다. 디밍 영역도 이 하위에 생성됩니다.")]
        public GameObject resultShield;
        [Min(.1f), Tooltip("결과·첫 스킬 설명이 자동으로 닫히는 시간(초)입니다.")]
        public float resultNoticeDuration = 5;
        public const string SkillNotice = "새로운 스킬이 추가되거나, 기존 스킬이 강화되고 다음 전투부터 사용 가능합니다.";

        private bool resultStarted, skillStarted, noticeOpen, releasePending;
        private float resultElapsed;
        private int instructionStep = -1;
        private bool instructionDismissPending;
        private BattleResultPanel result;
        private Button lockedButton;
        private bool originalInteractable;
        private RectTransform layoutPanel;
        private Vector2 originalSize, originalPosition, originalAnchorMin, originalAnchorMax;
        private TMP_Text body;
        private string originalBody;
        private JcTutorialSpotlight dim;
        public int ResultOpenedFrame { get; private set; } = -1;
        public int VisibleStep { get; private set; } = -1;

        private void Update() { RefreshPresentation(); AdvanceResultNotice(Time.unscaledDeltaTime); }
        public void AdvanceResultNotice(float delta)
        {
            if (!noticeOpen || instructionStep >= 0) return;
            resultElapsed += Mathf.Max(0, delta);
            if (resultElapsed >= resultNoticeDuration) DismissResult();
        }
        public void DismissResult()
        {
            if (!noticeOpen) return;
            if (instructionStep >= 0) instructionDismissPending = true;
            noticeOpen = false; releasePending = true;
            if (dim != null) dim.gameObject.SetActive(false);
            view.SetPanelSuppressed(true); view.SetVisible(false);
        }
        private void LateUpdate() => ReleaseResultInput(Input.GetMouseButton(0));
        public void ReleaseResultInput(bool held)
        {
            if (!releasePending || held) return;
            releasePending = false;
            if (instructionDismissPending)
            {
                int step = instructionStep;
                instructionDismissPending = false; instructionStep = -1;
                // 기존 버튼 이벤트가 전투 플로 잠금을 해제한다. 월드 클릭 처리가 끝난 뒤 한 번만 호출한다.
                if (continueButtons != null && step >= 0 && step < continueButtons.Length && continueButtons[step] != null)
                    continueButtons[step].onClick.Invoke();
            }
            UnlockInput();
            RefreshPresentation();
        }
        private void UnlockInput()
        {
            if (resultShield != null) resultShield.SetActive(false);
            if (lockedButton != null) lockedButton.interactable = originalInteractable;
            lockedButton = null;
            if (result != null) result.SetTutorialNoticeBlocked(false);
        }
        private void BeginNotice(Button button, RectTransform skill = null)
        {
            noticeOpen = true; resultElapsed = 0; ResultOpenedFrame = Time.frameCount;
            lockedButton = button;
            if (button != null) { originalInteractable = button.interactable; button.interactable = false; }
            if (result != null) result.SetTutorialNoticeBlocked(true);
            if (resultShield != null)
            {
                if (dim == null)
                {
                    var go = new GameObject("TutorialSpotlight", typeof(RectTransform), typeof(JcTutorialSpotlight));
                    go.layer = resultShield.layer;
                    go.transform.SetParent(resultShield.transform, false);
                    dim = go.GetComponent<JcTutorialSpotlight>();
                    dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
                    dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
                    dim.color = new Color(0, 0, 0, .68f);
                }
                dim.SetTargets(layoutPanel, skill); dim.gameObject.SetActive(true);
                resultShield.SetActive(true);
            }
        }
        private void CacheLayout()
        {
            if (layoutPanel != null || view.panel == null) return;
            layoutPanel = view.panel.transform as RectTransform;
            originalSize = layoutPanel.sizeDelta; originalPosition = layoutPanel.anchoredPosition;
            originalAnchorMin = layoutPanel.anchorMin; originalAnchorMax = layoutPanel.anchorMax;
            if (stepTexts != null && stepTexts.Length > 2 && stepTexts[2] != null)
            {
                body = stepTexts[2].GetComponent<TMP_Text>();
                if (body != null) { originalBody = body.text; JcTutorialGuideView.BindWordWrapping(body); }
            }
        }
        public void RefreshPresentation()
        {
            int active = -1;
            if (source != null && source.isActiveAndEnabled && stepTexts != null)
                for (int i = 0; i < stepTexts.Length; i++)
                    if (stepTexts[i] != null && stepTexts[i].activeInHierarchy) { active = i; break; }
            VisibleStep = active;
            RectTransform focus = null;
            if (continueButtons != null)
                for (int i = 0; i < continueButtons.Length; i++)
                {
                    if (continueButtons[i] == null) continue;
                    bool show = i == active;
                    if (continueButtons[i].gameObject.activeSelf != show) continueButtons[i].gameObject.SetActive(show);
                    if (show) focus = continueButtons[i].transform as RectTransform;
                }
            if (view == null) return;
            CacheLayout();
            foreach (var background in layoutPanel.GetComponentsInChildren<JcTutorialGraphic>(true)) background.SetOpaqueFill(active == 2);
            if (active != 2)
            {
                bool instruction = active == 0 || active == 1;
                if (!instruction) { noticeOpen = releasePending = instructionDismissPending = false; instructionStep = -1; UnlockInput(); }
                else if (instructionStep != active)
                {
                    instructionStep = active; instructionDismissPending = false;
                    BeginNotice(null);
                }
                if (layoutPanel != null)
                {
                    layoutPanel.anchorMin = originalAnchorMin; layoutPanel.anchorMax = originalAnchorMax;
                    layoutPanel.sizeDelta = originalSize; layoutPanel.anchoredPosition = originalPosition;
                }
                view.SetContent(active >= 0 && headings != null && active < headings.Length ? headings[active] : "전투 안내", "");
                view.SetPanelSuppressed(instruction && !noticeOpen);
                view.SetVisible(active >= 0 && (!instruction || noticeOpen), instruction ? null : focus);
                return;
            }
            if (result == null && resultAccept != null) result = resultAccept.GetComponentInParent<BattleResultPanel>();
            if (result == null) result = FindFirstObjectByType<BattleResultPanel>();
            var skill = result != null ? result.CurrentSkillNotice : null;
            bool pending = result != null && result.HasPendingSkillNotices;
            layoutPanel.anchorMin = layoutPanel.anchorMax = new Vector2(.5f, .5f);
            if (pending)
            {
                if (skill != null && !skillStarted)
                {
                    skillStarted = true;
                    layoutPanel.sizeDelta = resultPanelSize; layoutPanel.anchoredPosition = resultPanelPosition;
                    if (body != null) body.text = SkillNotice;
                    view.SetContent("전투 · 스킬 획득", "");
                    BeginNotice(skill.ConfirmButton);
                }
                focus = skill != null && skill.ConfirmButton != null ? skill.ConfirmButton.transform as RectTransform : null;
            }
            else
            {
                layoutPanel.sizeDelta = resultPanelSize; layoutPanel.anchoredPosition = resultPanelPosition;
                if (body != null) body.text = originalBody;
                view.SetContent(headings != null && headings.Length > 2 ? headings[2] : "전투 · 결과 확인", "");
                if (!resultStarted)
                {
                    resultStarted = true;
                    BeginNotice(resultAccept != null ? resultAccept.GetComponent<Button>() : null);
                }
                focus = resultAccept;
            }
            view.SetPanelSuppressed(!noticeOpen);
            view.SetVisible(true, !noticeOpen && !releasePending ? focus : null);
        }
        private void OnDisable()
        {
            instructionStep = -1; instructionDismissPending = false; noticeOpen = releasePending = false;
            UnlockInput();
            if (dim != null) dim.gameObject.SetActive(false);
            if (layoutPanel != null) foreach (var background in layoutPanel.GetComponentsInChildren<JcTutorialGraphic>(true)) background.SetOpaqueFill(false);
            if (body != null) body.text = originalBody;
            if (view != null) { view.SetPanelSuppressed(false); view.SetVisible(false); }
            if (layoutPanel != null)
            {
                layoutPanel.anchorMin = originalAnchorMin; layoutPanel.anchorMax = originalAnchorMax;
                layoutPanel.sizeDelta = originalSize; layoutPanel.anchoredPosition = originalPosition;
            }
            if (continueButtons != null) foreach (var button in continueButtons) if (button != null) button.gameObject.SetActive(true);
        }
    }
}
