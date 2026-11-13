using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JC.LoadingPreview
{
    // JC | 2026-11-12 | 원본 로딩 컴포넌트를 그대로 호출하는 독립 비교 씬입니다. 게임 진입/저장/씬 전환은 수행하지 않습니다.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class JcLoadingGlyphComparison : MonoBehaviour
    {
        [SerializeField, Tooltip("비교 씬에서만 사용하는 대칭 버스트 셰이더입니다.")] private Shader burstShader;
        [SerializeField, Tooltip("비교 화면의 한글 표시 폰트입니다. 원본 폰트 설정은 수정하지 않습니다.")] private TMP_FontAsset font;
        [SerializeField, Tooltip("비교 화면을 표시하는 전용 카메라입니다.")] private Camera viewCamera;
        // JC | 2026-11-12 | 별도 조절 오브젝트의 초안을 수정 주먹에만 전달합니다. 원본 열과 다른 글리프는 유지합니다.
        [SerializeField, Tooltip("저스티스 수정안의 위치·크기 조절 오브젝트입니다.")] private JcJusticeGlyphSettings justiceSettings;
        [SerializeField, Tooltip("편집 모드와 플레이 모드에서 원본/수정안을 같은 시간으로 재생합니다.")] private bool playing = true;
        [SerializeField, Range(.25f, 2f), Tooltip("시안 확인용 재생 배속입니다. 실제 로딩 설정에는 영향이 없습니다.")] private float speed = 1f;
        [SerializeField, Range(0f, 6.6f), Tooltip("등장을 포함한 비교 구간입니다. 6.6초마다 처음부터 반복합니다.")] private float previewTime = 1.12f;
        private GameObject content;
        private readonly SceneLoadingEmblem[] emblems = new SceneLoadingEmblem[6];
        private Slider timeline;
        private TMP_Text timeLabel, playLabel;
#if UNITY_EDITOR
        private double lastEditorTime;
#endif

        public void Configure(Shader shader, TMP_FontAsset labelFont, Camera camera)
        {
            burstShader = shader; font = labelFont; viewCamera = camera;
            EnsureBuilt(); Sample(previewTime);
        }

        private void OnEnable()
        {
            EnsureBuilt(); Sample(previewTime);
#if UNITY_EDITOR
            lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update += EditorTick;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
            // JC | 2026-11-12 | 비교용 임시 UI만 재질과 함께 해제합니다. 원본 씬·소재는 변경하지 않습니다.
            if (content)
            {
                if (Application.isPlaying) Destroy(content); else DestroyImmediate(content);
                content = null;
            }
        }

        private void Update() { if (Application.isPlaying) Advance(Time.unscaledDeltaTime); }
#if UNITY_EDITOR
        private void EditorTick()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float delta = (float)(now - lastEditorTime); lastEditorTime = now;
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEditor.EditorApplication.isCompiling || !this || !isActiveAndEnabled || !playing) return;
            Advance(delta);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
        private void Advance(float delta)
        {
            if (!playing) return;
            previewTime = Mathf.Repeat(previewTime + Mathf.Clamp(delta, 0f, .05f) * speed, 6.6f);
            Sample(previewTime);
        }

        // JC | 2026-11-12 | 여섯 표시를 동시에 평가하여 원본과 수정안의 재생 시간이 어긋나지 않게 합니다.
        public void Sample(float time)
        {
            previewTime = Mathf.Clamp(time, 0f, 6.6f);
            if (!content) return;
            if (justiceSettings && emblems[1])
                emblems[1].SetJusticeComparison(justiceSettings.StartCenter, justiceSettings.EndCenter, justiceSettings.InitialSize, justiceSettings.FinalSize);
            foreach (var emblem in emblems) if (emblem) emblem.SampleComparison(previewTime);
            if (timeline) timeline.SetValueWithoutNotify(previewTime);
            if (timeLabel) timeLabel.text = previewTime.ToString("0.00") + " s";
            if (playLabel) playLabel.text = playing ? "일시정지" : "재생";
        }
        public void SetPlaying(bool value) { playing = value; Sample(previewTime); }
        public void Restart() { Sample(0f); }
        // JC | 2026-11-12 | 조절기가 명시적으로 참조한 재생기에 자신을 연결합니다. 컴파일/씬 로드 순서와 무관하게 정지 프레임에도 즉시 반영합니다.
        public void ApplyJusticeSettings(JcJusticeGlyphSettings settings) { justiceSettings = settings; Sample(previewTime); }

        private void EnsureBuilt()
        {
            if (content || !burstShader || !font || !viewCamera) return;
            // JC | 2026-11-12 | 생성 UI/재질은 씬에 저장하지 않습니다. 씬에는 참조와 비교 설정만 남겨 로딩 본편과 분리합니다.
            content = new GameObject("Comparison preview (temporary)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            content.transform.SetParent(transform, false);
            content.hideFlags = HideFlags.DontSave;
            var canvas = content.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = viewCamera; canvas.planeDistance = 1f;
            var scaler = content.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)content.transform;
            Text(root, "로딩 글리프 · 원본 / 수정안", new Vector2(0, 465), new Vector2(1500, 60), 36, Color.white);
            Text(root, "같은 시간 · 같은 크기 기준으로 비교", new Vector2(0, 415), new Vector2(1500, 42), 21, new Color(.65f,.74f,.85f));
            Text(root, "원본", new Vector2(-360, 360), new Vector2(600, 40), 27, new Color(.8f,.84f,.9f));
            Text(root, "수정안", new Vector2(360, 360), new Vector2(600, 40), 27, new Color(.4f,.8f,1));
            int[] icons = { 0, 2, 1 };
            string[] names = { "저스티스 · 주먹", "루미나 · 버스트", "블랙불릿 · 쌍권총" };
            string[] notes = { "왼쪽 준비 → 중앙을 살짝 넘는 타격", "8방향 대칭 · 중심 회전\n부드러운 경계", "총구에서 시작하는 불꽃" };
            for (int row = 0; row < 3; row++)
            {
                float y = 205 - row * 245;
                for (int col = 0; col < 2; col++)
                {
                    float x = col == 0 ? -360 : 360;
                    var panel = Panel(root, "Panel " + row + " " + col, new Vector2(x,y), new Vector2(680,232), new Color(.018f,.026f,.045f));
                    Text(panel, names[row], new Vector2(-190,65), new Vector2(250,42), 23, Color.white);
                    Text(panel, col == 0 ? "현재 로딩 효과" : notes[row], new Vector2(-190,-5), new Vector2(250,90), 18, new Color(.62f,.72f,.85f));
                    var host = new GameObject((col == 0 ? "Original " : "Revised ") + icons[row], typeof(RectTransform), typeof(SceneLoadingEmblem));
                    var emblem = host.GetComponent<SceneLoadingEmblem>();
                    emblem.InitializeComparison(panel, icons[row], col == 1, burstShader);
                    ((RectTransform)host.transform).anchoredPosition = new Vector2(140,0);
                    emblems[row*2+col] = emblem;
                }
            }
            playLabel = Button(root, "일시정지", new Vector2(-655,-455), () => SetPlaying(!playing));
            Button(root, "처음부터", new Vector2(-455,-455), Restart);
            var sliderHost = new GameObject("Time slider", typeof(RectTransform), typeof(Slider));
            sliderHost.transform.SetParent(root,false);var sliderRect=(RectTransform)sliderHost.transform;
            sliderRect.sizeDelta=new Vector2(740,36);sliderRect.anchoredPosition=new Vector2(90,-455);
            Panel(sliderRect,"Track",Vector2.zero,new Vector2(740,8),new Color(.12f,.2f,.3f));
            var handle=Panel(sliderRect,"Handle",Vector2.zero,new Vector2(20,32),new Color(.3f,.72f,1));
            timeline=sliderHost.GetComponent<Slider>();timeline.minValue=0;timeline.maxValue=6.6f;timeline.handleRect=handle;timeline.targetGraphic=handle.GetComponent<Image>();
            timeline.onValueChanged.AddListener(t=>{playing=false;Sample(t);});
            timeLabel=Text(root,"",new Vector2(620,-455),new Vector2(180,40),23,Color.white);
            Text(root,"등장 포함 6.6초 반복 · 편집 모드: Inspector에서 정지 / 시간 조절 · 실제 로딩씬에는 미적용",new Vector2(0,-515),new Vector2(1760,36),18,new Color(.6f,.7f,.8f));
        }

        private RectTransform Panel(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.sizeDelta=size;rect.anchoredPosition=position;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return rect;
        }
        private TMP_Text Text(Transform parent,string text,Vector2 position,Vector2 size,float fontSize,Color color)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var label=go.GetComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.fontSize=fontSize;label.color=color;
            label.alignment=TextAlignmentOptions.Center;label.enableWordWrapping=true;label.raycastTarget=false;
            label.rectTransform.sizeDelta=size;label.rectTransform.anchoredPosition=position;return label;
        }
        private TMP_Text Button(Transform parent,string text,Vector2 position,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(parent,text,position,new Vector2(180,48),new Color(.08f,.2f,.34f));var image=rect.GetComponent<Image>();image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            return Text(rect,text,Vector2.zero,new Vector2(170,44),22,Color.white);
        }
    }
}
