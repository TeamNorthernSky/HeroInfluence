using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

namespace JC.VFX.Validation
{
    // 독립 검증 씬 전용입니다. 전투 등록부 및 프로젝트 렌더 설정을 수정하지 않습니다.
    public sealed class JcImportedVfxPreview : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string package;
            public string label;
            public string assetPath;
            public string warning;
            public VisualEffectAsset graph;
            public GameObject prefab;
        }

        public Entry[] entries = Array.Empty<Entry>();
        public Camera previewCamera;
        public Transform effectOrigin;
        public GameObject intersectionObjects;
        public Font interfaceFont;
        public int excludedScriptedPrefabs;
        public int excludedIncompletePrefabs;

        [Header("공간 기준 표시")]
        [Tooltip("플레이 중 옅은 1m 간격 바닥 그리드를 표시합니다. 효과 재생에는 영향을 주지 않습니다.")]
        public bool showGroundGrid = true;
        [Tooltip("플레이 중 카메라 반대편에 불투명 배경 벽을 표시합니다.")]
        public bool showBackdrop = true;
        [Tooltip("이펙트 생성 원점을 바닥면 중심으로 하는 1.5m 사각 와이어 큐브를 표시합니다. 효과의 실제 범위가 아닌 고정 크기 기준입니다.")]
        public bool showOriginFrame = true;
        private GameObject guideRoot, gridGuide, frameGuide, backdropGuide;
        private readonly List<Material> guideMaterials = new List<Material>();

        private readonly List<int> visible = new List<int>();
        private GameObject instance;
        private VisualEffect[] visualEffects = Array.Empty<VisualEffect>();
        private ParticleSystem[] particles = Array.Empty<ParticleSystem>();
        private Vector2 scroll;
        private string search = "";
        private bool searchFocused;
        private int packageFilter;
        private int kindFilter;
        private int selected = -1;
        private bool repeat = true;
        private bool paused;
        private bool depth = true;
        private float interval = 5;
        private float nextReplay;
        private float scale = 1;
        private float distance = 3;
        private float yaw = -35;
        private float pitch = 20;
        private float targetHeight = .5f;
        private Vector2 settingsScroll;
        private Vector3 focusPosition;
        private GUISkin panelSkin;
        private readonly List<Texture2D> uiTextures = new List<Texture2D>();
        private GUIStyle frameStatusStyle;
        private UnityEngine.UI.RawImage runtimeCursorImage;
        private readonly Vector3[] cursorCorners = new Vector3[4];
        private float PanelWidth => Mathf.Min(Mathf.Clamp(Screen.width * .30f, 380, 560), Screen.width * .6f);

        private void Start()
        {
            BuildSpatialGuides();
            Refilter();
            SetDepth(true);
            ResetCamera();
            if (visible.Count > 0) Select(visible[0]);
        }

        private void Update()
        {
            if (repeat && !paused && Time.unscaledTime >= nextReplay) Replay();
            if (!searchFocused)
            {
                if (Input.GetKeyDown(KeyCode.R)) Replay();
                if (Input.GetKeyDown(KeyCode.F)) ResetCamera();
                if (Input.GetKeyDown(KeyCode.RightArrow)) Step(1);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
            }
            if (Input.mousePosition.x > PanelWidth)
            {
                distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y, 2, 100);
                if (Input.GetMouseButton(1))
                {
                    yaw += Input.GetAxis("Mouse X") * 3;
                    pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 3, -10, 85);
                }
            }
            PositionCamera();
            UpdateSpatialGuides();
        }

        private void PositionCamera()
        {
            if (previewCamera == null) return;
            float left = PanelWidth / Mathf.Max(1, Screen.width);
            previewCamera.rect = new Rect(left, 0, 1 - left, 1);
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var target = focusPosition + Vector3.up * targetHeight;
            previewCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
        }

        private void BuildSpatialGuides()
        {
            if (guideRoot != null || effectOrigin == null) return;
            var ground = GameObject.Find("Ground");
            var renderer = ground != null ? ground.GetComponent<Renderer>() : null;
            if (renderer == null || renderer.sharedMaterial == null) return;
            guideRoot = new GameObject("Spatial Guides (Preview Only)");
            guideRoot.transform.SetParent(transform, false);
            // 부분 뷰포트 밖에도 매 프레임 유효한 배경을 제공해 GUI 여백의 잔상을 방지합니다.
            if (previewCamera != null)
            {
                var clearObject = new GameObject("Full Screen Background Clear");
                clearObject.transform.SetParent(guideRoot.transform, false);
                var clearCamera = clearObject.AddComponent<Camera>();
                clearCamera.cullingMask = 0;
                clearCamera.depth = previewCamera.depth - 1;
                clearCamera.clearFlags = CameraClearFlags.SolidColor;
                clearCamera.backgroundColor = new Color(.025f, .055f, .095f);
                clearCamera.targetDisplay = previewCamera.targetDisplay;
                clearCamera.targetTexture = previewCamera.targetTexture;
                clearCamera.allowHDR = false;
                clearCamera.allowMSAA = false;
            }
            gridGuide = new GameObject("Ground Grid - 1m");
            gridGuide.transform.SetParent(guideRoot.transform, false);
            frameGuide = new GameObject("Effect Origin - 1.5m Wire Cube");
            frameGuide.transform.SetParent(guideRoot.transform, false);
            var gridMaterial = GuideMaterial(renderer.sharedMaterial, new Color(.27f, .29f, .31f));
            var frameMaterial = GuideMaterial(renderer.sharedMaterial, new Color(.5f, .68f, .73f));
            var wallMaterial = GuideMaterial(renderer.sharedMaterial, new Color(.16f, .18f, .21f));
            float floor = renderer.bounds.max.y + .004f;
            var origin = effectOrigin.position;
            for (int i = -15; i <= 15; i++)
            {
                GuideBox("Grid X", gridGuide.transform, new Vector3(origin.x, floor, origin.z + i), new Vector3(30, .004f, .012f), gridMaterial);
                GuideBox("Grid Z", gridGuide.transform, new Vector3(origin.x + i, floor, origin.z), new Vector3(.012f, .004f, 30), gridMaterial);
            }
            // 면 없이 12개 모서리만 표시합니다.
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    GuideBox("Frame X", frameGuide.transform, origin + new Vector3(0, .75f + a * .75f, b * .75f), new Vector3(1.5f, .014f, .014f), frameMaterial);
                    GuideBox("Frame Y", frameGuide.transform, origin + new Vector3(a * .75f, .75f, b * .75f), new Vector3(.014f, 1.5f, .014f), frameMaterial);
                    GuideBox("Frame Z", frameGuide.transform, origin + new Vector3(a * .75f, .75f + b * .75f, 0), new Vector3(.014f, .014f, 1.5f), frameMaterial);
                }
            backdropGuide = GuideBox("Camera Backdrop", guideRoot.transform, origin + Vector3.forward * 12 + Vector3.up * 10, new Vector3(80, 24, .15f), wallMaterial);
            UpdateSpatialGuides();
        }

        private Material GuideMaterial(Material source, Color color)
        {
            var material = new Material(source) { name = "Preview Spatial Guide" };
            material.color = color;
            guideMaterials.Add(material);
            return material;
        }

        private GameObject GuideBox(string label, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private void UpdateSpatialGuides()
        {
            if (guideRoot == null) return;
            gridGuide.SetActive(showGroundGrid);
            frameGuide.SetActive(showOriginFrame);
            backdropGuide.SetActive(showBackdrop);
            if (previewCamera == null || effectOrigin == null) return;
            var away = effectOrigin.position - previewCamera.transform.position;
            away.y = 0;
            if (away.sqrMagnitude < .001f) away = Vector3.forward;
            away.Normalize();
            backdropGuide.transform.SetPositionAndRotation(effectOrigin.position + away * 12 + Vector3.up * 10, Quaternion.LookRotation(away));
        }

        private void Refilter()
        {
            visible.Clear();
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (packageFilter == 1 && e.package != "TwoUncle") continue;
                if (packageFilter == 2 && e.package != "Ultra Mega") continue;
                if (kindFilter == 0 && e.graph == null) continue;
                if (kindFilter == 1 && e.prefab == null) continue;
                if (!string.IsNullOrWhiteSpace(search) && e.assetPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                visible.Add(i);
            }
            scroll = Vector2.zero;
        }

        private void Select(int index)
        {
            selected = index;
            Replay();
        }

        private void Step(int direction)
        {
            if (visible.Count == 0) return;
            int position = visible.IndexOf(selected);
            Select(visible[(position + direction + visible.Count) % visible.Count]);
        }

        private void Clear()
        {
            if (instance != null)
            {
                instance.SetActive(false);
                Destroy(instance);
            }
            visualEffects = Array.Empty<VisualEffect>();
            particles = Array.Empty<ParticleSystem>();
        }

        private void Replay()
        {
            if (selected < 0 || selected >= entries.Length) return;
            Clear();
            var e = entries[selected];
            if (e.graph != null)
            {
                instance = new GameObject(e.label);
                instance.transform.SetParent(effectOrigin, false);
                instance.transform.localScale = Vector3.one * scale;
                var vfx = instance.AddComponent<VisualEffect>();
                vfx.visualEffectAsset = e.graph;
            }
            else if (e.prefab != null)
            {
                instance = Instantiate(e.prefab, effectOrigin, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localScale = e.prefab.transform.localScale * scale;
                instance.SetActive(true);
            }
            if (instance == null) return;
            visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
            particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var vfx in visualEffects)
            {
                if (!vfx.isActiveAndEnabled) continue;
                vfx.pause = false;
                vfx.Reinit();
            }
            // 최상위 시스템만 재생해 자식의 지연 및 서브 이미터 구성을 보존합니다.
            foreach (var ps in particles)
                if (ps.gameObject.activeInHierarchy && ps.main.playOnAwake &&
                    ps.transform.parent.GetComponentInParent<ParticleSystem>() == null)
                    ps.Play(true);
            paused = false;
            nextReplay = Time.unscaledTime + interval;
        }

        private void Pause(bool value)
        {
            paused = value;
            foreach (var vfx in visualEffects) if (vfx != null) vfx.pause = value;
            foreach (var ps in particles)
            {
                if (ps == null) continue;
                if (value && ps.isPlaying) ps.Pause(false);
                else if (!value && ps.isPaused) ps.Play(false);
            }
            nextReplay = Time.unscaledTime + interval;
        }

        private void SetDepth(bool value)
        {
            depth = value;
            if (previewCamera != null) previewCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = value;
        }

        private void ResetCamera()
        {
            distance = 3;
            if (previewCamera != null) previewCamera.fieldOfView = 50;
            yaw = -35;
            pitch = 20;
            targetHeight = .5f;
            focusPosition = instance != null ? instance.transform.position : effectOrigin.position;
            PositionCamera();
        }

        private Texture2D UiTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Validation UI Solid", hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            uiTextures.Add(texture);
            return texture;
        }

        private void StyleBackground(GUIStyle style, Texture2D normal, Texture2D selected = null)
        {
            var states = new[] { style.normal, style.hover, style.active, style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused };
            for (int i = 0; i < states.Length; i++)
            {
                states[i].background = i >= 4 && selected != null ? selected : normal;
                // 에디터 스킨에서 상속된 고해상도 아틀라스와 9-slice 경계를 사용하지 않습니다.
#if UNITY_EDITOR
                states[i].scaledBackgrounds = Array.Empty<Texture2D>();
#endif
                states[i].textColor = i >= 4 && selected != null ? new Color(.06f, .09f, .14f) : Color.white;
            }
            style.border = new RectOffset();
            style.overflow = new RectOffset();
        }

        private void PrepareSkin()
        {
            if (panelSkin != null) return;
            panelSkin = Instantiate(GUI.skin);
            if (interfaceFont != null) panelSkin.font = interfaceFont;
            var panel = UiTexture(new Color(.025f, .055f, .095f, .9f));
            var button = UiTexture(new Color(.12f, .19f, .27f, .96f));
            var yellow = UiTexture(new Color(1f, .82f, .25f));
            var white = UiTexture(new Color(.82f, .88f, .95f));
            var track = UiTexture(new Color(.42f, .53f, .66f));
            StyleBackground(panelSkin.box, panel);
            panelSkin.box.padding = new RectOffset(8, 8, 8, 8);
            StyleBackground(panelSkin.scrollView, null);
            StyleBackground(panelSkin.label, null);
            StyleBackground(panelSkin.button, button, yellow);
            StyleBackground(panelSkin.toggle, button, yellow);
            StyleBackground(panelSkin.textField, button);
            foreach (var style in new[] { panelSkin.label, panelSkin.button, panelSkin.toggle, panelSkin.textField })
            {
                style.font = panelSkin.font;
                style.fontSize = 16;
                style.fixedHeight = 0;
                style.wordWrap = true;
                style.padding = new RectOffset(10, 10, 9, 9);
            }
            panelSkin.button.alignment = TextAnchor.MiddleLeft;
            panelSkin.toggle.alignment = TextAnchor.MiddleLeft;
            StyleBackground(panelSkin.horizontalSlider, track);
            StyleBackground(panelSkin.horizontalSliderThumb, yellow);
            panelSkin.horizontalSlider.fixedHeight = 8;
            panelSkin.horizontalSlider.margin = new RectOffset(12, 12, 12, 18);
            panelSkin.horizontalSlider.padding = new RectOffset();
            panelSkin.horizontalSliderThumb.fixedWidth = 18;
            panelSkin.horizontalSliderThumb.fixedHeight = 22;
            panelSkin.horizontalSliderThumb.overflow = new RectOffset(0, 0, 7, 7);
            StyleBackground(panelSkin.verticalScrollbar, track);
            StyleBackground(panelSkin.verticalScrollbarThumb, white);
            panelSkin.verticalScrollbar.fixedWidth = 14;
            panelSkin.verticalScrollbar.padding = new RectOffset();
            panelSkin.verticalScrollbarThumb.fixedWidth = 14;
            panelSkin.verticalScrollbarThumb.fixedHeight = 0;
            foreach (var style in new[] { panelSkin.verticalScrollbarUpButton, panelSkin.verticalScrollbarDownButton, panelSkin.horizontalScrollbarLeftButton, panelSkin.horizontalScrollbarRightButton })
            {
                StyleBackground(style, null);
                style.fixedWidth = 0;
                style.fixedHeight = 0;
            }
            StyleBackground(panelSkin.horizontalScrollbar, track);
            StyleBackground(panelSkin.horizontalScrollbarThumb, white);
            frameStatusStyle = new GUIStyle(panelSkin.box) { font = panelSkin.font, fontSize = 13, alignment = TextAnchor.MiddleCenter };
        }

        // 폰트의 실제 줄 높이를 반영해 한글과 긴 경로가 잘리지 않게 합니다.
        private void Label(string text, float width)
        {
            float height = GUI.skin.label.CalcHeight(new GUIContent(text), width);
            GUILayout.Label(text, GUILayout.Width(width), GUILayout.Height(Mathf.Max(38, height)));
        }

        private bool Button(string text, float width)
        {
            float height = GUI.skin.button.CalcHeight(new GUIContent(text), width);
            return GUILayout.Button(text, GUILayout.Width(width), GUILayout.Height(Mathf.Max(44, height)));
        }

        private void OnGUI()
        {
            PrepareSkin();
            var previousSkin = GUI.skin;
            GUI.skin = panelSkin;
            float panel = PanelWidth;
            float width = panel - 44;
            float headerHeight = 116;
            float listHeight = Mathf.Clamp((Screen.height - headerHeight) * .45f, 140, 440);
            GUILayout.BeginArea(new Rect(8, 8, panel - 16, headerHeight), GUI.skin.box);
            Label("상용 VFX 독립 검증", width);
            GUILayout.BeginHorizontal();
            if (Button("카메라 초기화 [F]", width - 136)) ResetCamera();
            if (GUILayout.Button(new GUIContent(showOriginFrame ? "프레임 ON" : "프레임 OFF", "생성 위치를 표시하는 1.5m 와이어 큐브를 켜거나 끕니다."), GUILayout.Width(128), GUILayout.Height(44)))
            {
                showOriginFrame = !showOriginFrame;
                UpdateSpatialGuides();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // 목록과 설정에 독립된 세로 스크롤을 제공하며 가로 폭은 고정합니다.
            GUILayout.BeginArea(new Rect(8, headerHeight + 12, panel - 16, listHeight), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll, false, true);
            float contentWidth = width - 20;
            int newPackage = GUILayout.Toolbar(packageFilter, new[] { "전체", "TwoUncle", "Ultra Mega" }, GUILayout.Width(contentWidth), GUILayout.Height(46));
            int newKind = GUILayout.Toolbar(kindFilter, new[] { "그래프 단독", "효과 프리팹" }, GUILayout.Width(contentWidth), GUILayout.Height(46));
            GUI.SetNextControlName("VfxSearch");
            string newSearch = GUILayout.TextField(search, GUILayout.Width(contentWidth), GUILayout.Height(44));
            searchFocused = GUI.GetNameOfFocusedControl() == "VfxSearch";
            if (newPackage != packageFilter || newKind != kindFilter || newSearch != search)
            {
                packageFilter = newPackage; kindFilter = newKind; search = newSearch; Refilter();
                if (visible.Count == 0) { Clear(); selected = -1; }
                else if (!visible.Contains(selected)) Select(visible[0]);
            }
            Label($"표시 {visible.Count}개 · 검색어로 목록 필터", contentWidth);
            foreach (int i in visible)
            {
                string text = (selected == i ? "▶ " : "") + (string.IsNullOrEmpty(entries[i].warning) ? "" : "[주의] ") + entries[i].package + " / " + entries[i].label;
                if (Button(text, contentWidth)) Select(i);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            float settingsTop = headerHeight + listHeight + 20;
            GUILayout.BeginArea(new Rect(8, settingsTop, panel - 16, Mathf.Max(80, Screen.height - settingsTop - 8)), GUI.skin.box);
            settingsScroll = GUILayout.BeginScrollView(settingsScroll, false, true);
            Label("재생 및 화면 조절", contentWidth);
            Label("← / → 선택 · R 재생 · 우클릭 회전 · 휠 확대", contentWidth);
            GUILayout.BeginHorizontal();
            if (Button("이전", contentWidth / 2 - 4)) Step(-1);
            if (Button("다음", contentWidth / 2 - 4)) Step(1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Button("재생 (R)", contentWidth / 2 - 4)) Replay();
            if (Button(paused ? "계속" : "일시정지", contentWidth / 2 - 4)) Pause(!paused);
            GUILayout.EndHorizontal();
            if (selected >= 0)
            {
                Label(entries[selected].assetPath, contentWidth);
                if (!string.IsNullOrEmpty(entries[selected].warning)) Label(entries[selected].warning, contentWidth);
            }
            bool newRepeat = GUILayout.Toggle(repeat, new GUIContent((repeat ? "[ON] " : "[OFF] ") + "자동 반복", "설정한 간격으로 선택한 이펙트를 다시 재생합니다."), GUILayout.Width(contentWidth), GUILayout.Height(44));
            if (newRepeat != repeat) { repeat = newRepeat; nextReplay = Time.unscaledTime + interval; }
            Label($"반복 간격: {interval:F1}초", contentWidth);
            interval = GUILayout.HorizontalSlider(interval, 2, 20, GUILayout.Width(contentWidth));
            bool newDepth = GUILayout.Toggle(depth, new GUIContent((depth ? "[ON] " : "[OFF] ") + "검증 카메라 Depth Texture", "깊이 텍스처를 사용하는 효과의 동작을 비교합니다."), GUILayout.Width(contentWidth), GUILayout.Height(44));
            if (newDepth != depth) SetDepth(newDepth);
            if (intersectionObjects != null)
            {
                bool enabled = GUILayout.Toggle(intersectionObjects.activeSelf, new GUIContent((intersectionObjects.activeSelf ? "[ON] " : "[OFF] ") + "교차 확인용 구·벽 표시", "교차 효과 검사용 구와 벽을 표시합니다. 배경 벽과는 별개입니다."), GUILayout.Width(contentWidth), GUILayout.Height(44));
                if (enabled != intersectionObjects.activeSelf) intersectionObjects.SetActive(enabled);
            }
            Label($"크기: {scale:F2} (변경 후 재생)", contentWidth);
            scale = GUILayout.HorizontalSlider(scale, .1f, 5, GUILayout.Width(contentWidth));
            Label($"카메라 거리: {distance:F1}", contentWidth);
            distance = GUILayout.HorizontalSlider(distance, 2, 100, GUILayout.Width(contentWidth));
            Label($"카메라 기준 높이: {targetHeight:F1}", contentWidth);
            targetHeight = GUILayout.HorizontalSlider(targetHeight, 0, 15, GUILayout.Width(contentWidth));
            Label($"목록 제외: 데모 스크립트 {excludedScriptedPrefabs}개 · 참조 누락 {excludedIncompletePrefabs}개", contentWidth);
            Label("그래프는 기본 이벤트로 실행합니다. 투사체 이동·타격 등 데모 스크립트 연동은 별도 검증 대상입니다.", contentWidth);
            if (!SystemInfo.supportsComputeShaders) Label("이 기기는 Compute Shader를 지원하지 않습니다.", contentWidth);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.Box(new Rect(Screen.width - 158, 12, 146, 28), showOriginFrame ? "프레임 ON · 1.5m" : "프레임 OFF", frameStatusStyle);
            GUI.skin = previousSkin;
            DrawCursorAbovePanel();
        }

        // Canvas 커서는 IMGUI보다 먼저 렌더됩니다. 검증 UI 위에 동일한 표시를 덧그립니다.
        // 공용 커서의 텍스처·핫스폿·해상도 배율·화면 경계 처리는 변경하지 않습니다.
        private void DrawCursorAbovePanel()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (runtimeCursorImage == null)
            {
                var root = GameObject.Find("JC Runtime Cursor");
                if (root != null) runtimeCursorImage = root.GetComponentInChildren<UnityEngine.UI.RawImage>();
            }
            if (runtimeCursorImage == null || !runtimeCursorImage.isActiveAndEnabled ||
                runtimeCursorImage.texture == null || Cursor.visible) return;
            runtimeCursorImage.rectTransform.GetWorldCorners(cursorCorners);
            var lower = RectTransformUtility.WorldToScreenPoint(null, cursorCorners[0]);
            var upper = RectTransformUtility.WorldToScreenPoint(null, cursorCorners[2]);
            var area = new Rect(lower.x, Screen.height - upper.y, upper.x - lower.x, upper.y - lower.y);
            var previousColor = GUI.color;
            var previousDepth = GUI.depth;
            GUI.color = runtimeCursorImage.color;
            GUI.depth = -10000;
            GUI.DrawTextureWithTexCoords(area, runtimeCursorImage.texture, runtimeCursorImage.uvRect, true);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        private void OnDestroy()
        {
            Clear();
            if (panelSkin != null) Destroy(panelSkin);
            foreach (var texture in uiTextures) if (texture != null) Destroy(texture);
            if (guideRoot != null) Destroy(guideRoot);
            foreach (var material in guideMaterials) if (material != null) Destroy(material);
        }
    }
}
