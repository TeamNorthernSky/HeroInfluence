using UnityEngine;
using UnityEngine.UI;

namespace JC.Tutorial
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100), AddComponentMenu("JC Tutorial/전투 안내")]
    public sealed class JcTutorialBattleGuide : MonoBehaviour
    {
        [Tooltip("기존 안내 UI입니다. 기존 단계별 텍스트 활성 상태를 읽고 결과 설명 동안만 화면 입력을 차단합니다.")]
        public TutorialBattleUI source;
        [Tooltip("기존 단계별 텍스트입니다. 기존 TutorialBattleUI의 순서와 일치시킵니다.")]
        public GameObject[] stepTexts;
        [Tooltip("기존 단계별 진행 버튼입니다. 대응 버튼이 없는 단계는 비워 둡니다. 기존 클릭 이벤트를 그대로 사용합니다.")]
        public Button[] continueButtons;
        [Tooltip("승리 단계에서 강조할 기존 결과 확인 버튼입니다.")]
        public RectTransform resultAccept;
        [Tooltip("안내 패널과 강조 표현입니다.")] public JcTutorialGuideView view;
        [Tooltip("각 단계에서 표시할 상단 문구입니다. 본문은 기존 단계별 Text 오브젝트에서 수정합니다.")]
        public string[] headings = { "전투 · 행동 안내", "전투 · 지원 안내", "전투 · 결과 확인", "전투 안내" };
        [Tooltip("결과창 제목을 가리지 않도록 결과 단계에서만 적용할 패널 크기입니다. Canvas 기준 단위입니다.")]
        public Vector2 resultPanelSize = new Vector2(620, 166);
        [Tooltip("결과 단계 패널 위치입니다. 기존 패널의 상단 중앙 앵커 기준이며 Canvas 단위입니다.")]
        public Vector2 resultPanelPosition = new Vector2(-620, -93);
        [Tooltip("전투 결과 설명 중 모든 화면 클릭을 받는 투명 차단막입니다.")]
        public GameObject resultShield;
        [Min(.1f), Tooltip("전투 결과 설명이 자동으로 사라지는 시간(초)입니다. 화면 클릭으로 먼저 닫을 수 있습니다.")]
        public float resultNoticeDuration=3;
        private bool resultStarted,resultDismissed,releasePending;
        private float resultElapsed;
        public int ResultOpenedFrame { get; private set; }=-1;
        private RectTransform layoutPanel;
        private Vector2 originalSize, originalPosition;
        public int VisibleStep { get; private set; } = -1;
        private void Update() { RefreshPresentation();AdvanceResultNotice(Time.unscaledDeltaTime); }
        public void AdvanceResultNotice(float delta) {
            if(!resultStarted||resultDismissed||VisibleStep!=2)return;
            resultElapsed+=Mathf.Max(0,delta);
            if(resultElapsed>=resultNoticeDuration)DismissResult();
        }
        public void DismissResult() {
            if(!resultStarted||resultDismissed)return;
            resultDismissed=true;releasePending=true;view.SetPanelSuppressed(true);view.SetVisible(false);
        }
        private void LateUpdate() => ReleaseResultInput(Input.GetMouseButton(0));
        public void ReleaseResultInput(bool held) {
            if(!releasePending||held)return;
            releasePending=false;if(resultShield!=null)resultShield.SetActive(false);
            RefreshPresentation();
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
            if (active == 2 && resultAccept != null && resultAccept.gameObject.activeInHierarchy) focus = resultAccept;
            if (view == null) return;
            if (layoutPanel == null && view.panel != null)
            {
                layoutPanel = view.panel.transform as RectTransform;
                if (layoutPanel != null) { originalSize = layoutPanel.sizeDelta; originalPosition = layoutPanel.anchoredPosition; }
            }
            if (layoutPanel != null)
            {
                layoutPanel.sizeDelta = active == 2 ? resultPanelSize : originalSize;
                layoutPanel.anchoredPosition = active == 2 ? resultPanelPosition : originalPosition;
            }
            view.SetContent(active >= 0 && headings != null && active < headings.Length ? headings[active] : "전투 안내", "");
            if(active==2) {
                if(!resultStarted){resultStarted=true;resultElapsed=0;ResultOpenedFrame=Time.frameCount;if(resultShield!=null)resultShield.SetActive(true);}
                view.SetPanelSuppressed(resultDismissed);
                view.SetVisible(true,resultDismissed&&!releasePending?focus:null);
            } else {
                if(resultShield!=null)resultShield.SetActive(false);
                view.SetPanelSuppressed(false);view.SetVisible(active>=0,focus);
            }
        }
        private void OnDisable()
        {
            if(resultShield!=null)resultShield.SetActive(false);
            if (view != null) { view.SetPanelSuppressed(false);view.SetVisible(false); }
            if (layoutPanel != null) { layoutPanel.sizeDelta = originalSize; layoutPanel.anchoredPosition = originalPosition; }
            // 이 표시 컴포넌트만 해제했을 때 기존 진행 버튼을 사용할 수 있도록 복구한다.
            if (continueButtons != null) foreach (var button in continueButtons) if (button != null) button.gameObject.SetActive(true);
        }
    }
}
