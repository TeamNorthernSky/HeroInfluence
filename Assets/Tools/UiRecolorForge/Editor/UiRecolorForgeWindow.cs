using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JC.UiRecolor
{
    /// <summary>
    /// UI Recolor Forge — AI 생성 UI 리소스의 특정 색역 색감 조절 툴.
    /// 구조: 격리 작업 폴더(Source→Out 미러) 모델. Assets 와 무관하게 PNG 를 직접 읽고 쓴다.
    /// 조절값은 메모리 유지, 디스크 쓰기는 명시 버튼만(이미지 저장/배치 적용/프리셋 저장) — 튜닝 저장 공식 규격.
    /// </summary>
    public class UiRecolorForgeWindow : EditorWindow
    {
        private const string PrefWorkRoot = "JC.UiRecolor.WorkRoot";
        private const string PrefLastImageDir = "JC.UiRecolor.LastImageDir";
        private const float SidebarWidth = 380f;

        private enum PreviewMode { Original, Result, Mask }
        private enum PickTarget { None, Reference, Target }

        // 이미지 상태
        private string _imagePath;
        private Color32[] _srcPixels;
        private int _imgW, _imgH;
        private Texture2D _srcTex, _outTex, _maskTex;

        // 편집 상태 (메모리 유지 — 디스크 쓰기는 명시 버튼만)
        private RecolorPreset _preset = new RecolorPreset();
        private int _activeSlot;
        private bool _dirty;

        // 뷰 상태
        private PreviewMode _mode = PreviewMode.Result;
        private PickTarget _pick = PickTarget.None;
        private float _zoom = 1f;
        private Vector2 _scroll;
        private Vector2 _sideScroll;
        private string _workRoot;
        private string _status = "이미지를 열어 시작하세요.";

        [MenuItem("Tools/JC/UI Recolor Forge")]
        private static void Open()
        {
            var w = GetWindow<UiRecolorForgeWindow>("UI Recolor Forge");
            w.minSize = new Vector2(900f, 520f);
        }

        private void OnEnable()
        {
            _workRoot = EditorPrefs.GetString(PrefWorkRoot, "");
            if (_preset.slots.Count == 0) _preset.slots.Add(new RecolorSlot { label = "슬롯 1" });
        }

        private void OnDisable()
        {
            if (_srcTex != null) DestroyImmediate(_srcTex);
            if (_outTex != null) DestroyImmediate(_outTex);
            if (_maskTex != null) DestroyImmediate(_maskTex);
        }

        private void OnGUI()
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewPane();
                DrawSidebar();
            }
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);

            if (_dirty && _srcPixels != null)
            {
                _dirty = false;
                RecomputePreview();
                Repaint();
            }
        }

        // ─────────────────────────── 툴바 ───────────────────────────

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("이미지 열기", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    LoadImageDialog();
                if (GUILayout.Button("프리셋 열기", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    LoadPresetDialog();
                if (GUILayout.Button("💾 프리셋 저장", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    SavePresetDialog();

                GUILayout.Space(12f);
                _mode = (PreviewMode)GUILayout.Toolbar((int)_mode,
                    new[] { "원본", "결과", "마스크" }, EditorStyles.toolbarButton, GUILayout.Width(180f));

                GUILayout.Space(12f);
                GUILayout.Label("줌", GUILayout.Width(24f));
                _zoom = GUILayout.HorizontalSlider(_zoom, 0.25f, 8f, GUILayout.Width(120f));
                GUILayout.Label($"{_zoom:0.0}x", GUILayout.Width(36f));
                if (GUILayout.Button("맞춤", EditorStyles.toolbarButton, GUILayout.Width(44f)) && _imgW > 0)
                {
                    float availW = position.width - SidebarWidth - 40f;
                    float availH = position.height - 80f;
                    _zoom = Mathf.Clamp(Mathf.Min(availW / _imgW, availH / _imgH), 0.25f, 8f);
                }
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrEmpty(_imagePath))
                    GUILayout.Label(Path.GetFileName(_imagePath), EditorStyles.toolbarButton);
            }
        }

        // ─────────────────────────── 프리뷰 ───────────────────────────

        private void DrawPreviewPane()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                if (_srcPixels == null)
                {
                    EditorGUILayout.HelpBox("PNG 를 열면 여기에 프리뷰가 표시됩니다.\n스포이드 토글 후 프리뷰를 클릭하면 색을 픽업합니다.", MessageType.Info);
                    return;
                }

                Texture2D tex = _mode switch
                {
                    PreviewMode.Original => _srcTex,
                    PreviewMode.Mask => _maskTex,
                    _ => _outTex,
                };
                if (tex == null) tex = _srcTex;

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                Rect imgRect = GUILayoutUtility.GetRect(_imgW * _zoom, _imgH * _zoom,
                    GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                EditorGUI.DrawTextureTransparent(imgRect, tex, ScaleMode.StretchToFill);

                HandleEyedropper(imgRect);
                EditorGUILayout.EndScrollView();
            }
        }

        private void HandleEyedropper(Rect imgRect)
        {
            if (_pick == PickTarget.None) return;
            EditorGUIUtility.AddCursorRect(imgRect, MouseCursor.ArrowPlus);

            Event e = Event.current;
            if (e.type != EventType.MouseDown || !imgRect.Contains(e.mousePosition)) return;

            int px = Mathf.Clamp((int)((e.mousePosition.x - imgRect.x) / _zoom), 0, _imgW - 1);
            int pyFromTop = Mathf.Clamp((int)((e.mousePosition.y - imgRect.y) / _zoom), 0, _imgH - 1);
            int py = _imgH - 1 - pyFromTop; // GUI 는 위에서 아래, 텍스처 행은 아래에서 위

            Color picked = SampleAverage3x3(px, py);
            var slot = ActiveSlot();
            if (slot != null)
            {
                if (_pick == PickTarget.Reference) slot.referenceColor = picked;
                else slot.targetColor = picked;
                _status = $"픽업: ({px},{py}) → {(_pick == PickTarget.Reference ? "기준색" : "목표색")} #{ColorUtility.ToHtmlStringRGB(picked)}";
                _dirty = true;
            }
            _pick = PickTarget.None;
            e.Use();
        }

        private Color SampleAverage3x3(int cx, int cy)
        {
            float r = 0f, g = 0f, b = 0f; int n = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= _imgW || y >= _imgH) continue;
                    Color32 p = _srcPixels[y * _imgW + x];
                    if (p.a < 32) continue; // 투명 잔털 제외
                    r += p.r; g += p.g; b += p.b; n++;
                }
            if (n == 0) { Color32 c = _srcPixels[cy * _imgW + cx]; return new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f); }
            return new Color(r / n / 255f, g / n / 255f, b / n / 255f, 1f);
        }

        // ─────────────────────────── 사이드바 ───────────────────────────

        private void DrawSidebar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth)))
            {
                _sideScroll = EditorGUILayout.BeginScrollView(_sideScroll);

                EditorGUI.BeginChangeCheck();
                DrawSlots();
                GUILayout.Space(8f);
                DrawBoundarySection();
                if (EditorGUI.EndChangeCheck()) _dirty = true;

                GUILayout.Space(8f);
                DrawSaveSection();
                GUILayout.Space(8f);
                DrawBatchSection();

                EditorGUILayout.EndScrollView();
            }
        }

        private RecolorSlot ActiveSlot()
            => _activeSlot >= 0 && _activeSlot < _preset.slots.Count ? _preset.slots[_activeSlot] : null;

        private void DrawSlots()
        {
            EditorGUILayout.LabelField("색역 슬롯", EditorStyles.boldLabel);
            for (int i = 0; i < _preset.slots.Count; i++)
            {
                var s = _preset.slots[i];
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool isActive = _activeSlot == i;
                        bool nowActive = GUILayout.Toggle(isActive, $"▶ {s.label}", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                        if (nowActive && !isActive) { _activeSlot = i; _dirty = true; } // 마스크 뷰 대상 변경
                        s.enabled = EditorGUILayout.ToggleLeft("활성", s.enabled, GUILayout.Width(50f));
                        if (_preset.slots.Count > 1 && GUILayout.Button("✕", GUILayout.Width(22f)))
                        {
                            _preset.slots.RemoveAt(i);
                            _activeSlot = Mathf.Clamp(_activeSlot, 0, _preset.slots.Count - 1);
                            _dirty = true;
                            return; // 컬렉션 변경 — 이번 프레임 그리기 중단
                        }
                    }
                    if (_activeSlot != i) continue; // 비활성 슬롯은 헤더만(패널 슬림 유지)

                    s.label = EditorGUILayout.TextField("이름", s.label);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        s.referenceColor = EditorGUILayout.ColorField(new GUIContent("기준색"), s.referenceColor, true, false, false);
                        bool on = GUILayout.Toggle(_pick == PickTarget.Reference, "스포이드", "Button", GUILayout.Width(64f));
                        _pick = on ? PickTarget.Reference : (_pick == PickTarget.Reference ? PickTarget.None : _pick);
                    }

                    EditorGUILayout.LabelField("게이트(임계값·스무딩)", EditorStyles.miniBoldLabel);
                    s.hueRadius = EditorGUILayout.Slider(new GUIContent("색상 반경(°)", "완전 선택되는 hue 거리"), s.hueRadius, 0f, 90f);
                    s.hueFalloff = EditorGUILayout.Slider(new GUIContent("색상 감쇠(°)", "경계 스무딩 폭"), s.hueFalloff, 0f, 60f);
                    s.chromaMin = EditorGUILayout.Slider(new GUIContent("채도 하한", "무채색(흰 글자·검정 테두리) 제외선"), s.chromaMin, 0f, 0.2f);
                    s.chromaSoft = EditorGUILayout.Slider(new GUIContent("채도 감쇠", "채도 게이트 스무딩 폭"), s.chromaSoft, 0f, 0.1f);
                    s.useLightGate = EditorGUILayout.ToggleLeft("명도 창 게이트", s.useLightGate);
                    if (s.useLightGate)
                    {
                        EditorGUILayout.MinMaxSlider(new GUIContent("명도 범위"), ref s.lightMin, ref s.lightMax, 0f, 1f);
                        s.lightSoft = EditorGUILayout.Slider("명도 감쇠", s.lightSoft, 0f, 0.2f);
                    }

                    EditorGUILayout.LabelField("편집", EditorStyles.miniBoldLabel);
                    s.absoluteMode = EditorGUILayout.Popup("모드", s.absoluteMode ? 1 : 0,
                        new[] { "상대(델타)", "절대(기준→목표)" }) == 1;
                    if (s.absoluteMode)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            s.targetColor = EditorGUILayout.ColorField(new GUIContent("목표색"), s.targetColor, true, false, false);
                            bool on = GUILayout.Toggle(_pick == PickTarget.Target, "스포이드", "Button", GUILayout.Width(64f));
                            _pick = on ? PickTarget.Target : (_pick == PickTarget.Target ? PickTarget.None : _pick);
                        }
                        EditorGUILayout.HelpBox("배치 시: 이미지별 자동 앵커 → 목표색 매핑(AI 편차 흡수). 단일 편집 시: 기준색 → 목표색.", MessageType.None);
                    }
                    else
                    {
                        s.hueShift = EditorGUILayout.Slider(new GUIContent("색상 회전(°)"), s.hueShift, -180f, 180f);
                        s.chromaScale = EditorGUILayout.Slider(new GUIContent("채도 배율"), s.chromaScale, 0f, 2f);
                        s.chromaOffset = EditorGUILayout.Slider(new GUIContent("채도 오프셋"), s.chromaOffset, -0.2f, 0.2f);
                        s.lightScale = EditorGUILayout.Slider(new GUIContent("명도 배율"), s.lightScale, 0f, 2f);
                        s.lightOffset = EditorGUILayout.Slider(new GUIContent("명도 오프셋"), s.lightOffset, -0.5f, 0.5f);
                    }

                    s.tighten = EditorGUILayout.Slider(
                        new GUIContent("색 조임", "이 색역 픽셀들의 색상 산포를 대표색 쪽으로 당김 — AI 잡색 정리(0=끔)"), s.tighten, 0f, 1f);

                    s.anchorWindow = EditorGUILayout.Slider(new GUIContent("배치 앵커 창(°)", "이미지별 대표색 검출에 쓰는 넓은 hue 창"), s.anchorWindow, 10f, 120f);
                }
            }

            if (_preset.slots.Count < RecolorPreset.MaxSlots && GUILayout.Button("+ 슬롯 추가"))
            {
                _preset.slots.Add(new RecolorSlot { label = $"슬롯 {_preset.slots.Count + 1}" });
                _activeSlot = _preset.slots.Count - 1;
                _dirty = true;
            }
        }

        private void DrawBoundarySection()
        {
            var bu = _preset.boundary;
            EditorGUILayout.LabelField("경계 혼합 보정 (2색 언믹싱)", EditorStyles.boldLabel);
            bu.enabled = EditorGUILayout.ToggleLeft(
                new GUIContent("사용", "두 색이 만나는 경계의 혼합 픽셀을 「A색+B색」으로 분해해 편집된 색으로 재합성 — 극단 변화 시 경계 띠 잔류 방지"),
                bu.enabled);
            if (!bu.enabled) return;

            var names = new string[_preset.slots.Count];
            for (int i = 0; i < names.Length; i++) names[i] = $"{i + 1}. {_preset.slots[i].label}";
            bu.slotA = EditorGUILayout.Popup("슬롯 A (바꾸는 색)", Mathf.Clamp(bu.slotA, 0, names.Length - 1), names);
            bu.slotB = EditorGUILayout.Popup("슬롯 B (맞닿은 색)", Mathf.Clamp(bu.slotB, 0, names.Length - 1), names);
            if (bu.slotA == bu.slotB)
                EditorGUILayout.HelpBox("슬롯 A와 B가 같습니다 — 서로 다른 두 색역을 지정해야 작동합니다.", MessageType.Warning);
            bu.distRadius = EditorGUILayout.Slider(
                new GUIContent("경계 폭", "두 색을 잇는 혼합 경로에서 이 거리 안의 픽셀만 보정 대상"), bu.distRadius, 0.01f, 0.3f);
            bu.distFalloff = EditorGUILayout.Slider(
                new GUIContent("경계 감쇠", "보정 강도가 0으로 떨어지는 완충 폭"), bu.distFalloff, 0.01f, 0.3f);
            bu.residualSuppress = EditorGUILayout.Slider(
                new GUIContent("잡색 억제", "경계에서 '두 색의 순수 혼합'을 벗어난 잡색을 지움(1=완전 재합성) — 원본 경계가 지저분할 때"), bu.residualSuppress, 0f, 1f);
            bu.smoothRadius = EditorGUILayout.IntSlider(
                new GUIContent("경계 평활 반경(px)", "혼합 비율을 공간적으로 골라 뭉개진 전이를 매끈한 그라데이션으로 다시 그림(0=끔)"), bu.smoothRadius, 0, 4);
            EditorGUILayout.HelpBox("맞닿은 색(B)은 편집 없이 기준색만 스포이드로 등록해 두면 됩니다(그 색은 안 바뀜).", MessageType.None);
        }

        private void DrawSaveSection()
        {
            EditorGUILayout.LabelField("저장 (명시 버튼만 디스크 기록)", EditorStyles.boldLabel);
            _preset.dither = EditorGUILayout.ToggleLeft(new GUIContent("저장 시 미세 디더", "8bit 그라데이션 밴딩 완화"), _preset.dither);
            using (new EditorGUI.DisabledScope(_srcPixels == null))
            {
                if (GUILayout.Button("이미지 저장 (PNG)")) SaveImageDialog();
            }
        }

        private void DrawBatchSection()
        {
            EditorGUILayout.LabelField("배치 (작업루트: Source → Out 미러)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _workRoot = EditorGUILayout.TextField("작업루트", _workRoot);
                if (GUILayout.Button("...", GUILayout.Width(28f)))
                {
                    string sel = EditorUtility.OpenFolderPanel("작업루트 선택 (Source/ 하위에 원본)", _workRoot, "");
                    if (!string.IsNullOrEmpty(sel)) { _workRoot = sel; EditorPrefs.SetString(PrefWorkRoot, sel); }
                }
            }
            _preset.batchSkipDeltaE = EditorGUILayout.Slider(
                new GUIContent("스킵 ΔE", "앵커가 기준색에서 이 지각 색차를 넘으면 그 슬롯은 적용하지 않고 플래그"),
                _preset.batchSkipDeltaE, 0.02f, 0.4f);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_workRoot)))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("드라이런 (리포트만)")) RunBatch(dryRun: true);
                if (GUILayout.Button("배치 적용")) RunBatch(dryRun: false);
            }
        }

        // ─────────────────────────── 이미지 IO ───────────────────────────

        private void LoadImageDialog()
        {
            string dir = EditorPrefs.GetString(PrefLastImageDir, "");
            string path = EditorUtility.OpenFilePanel("PNG 열기", dir, "png");
            if (string.IsNullOrEmpty(path)) return;
            EditorPrefs.SetString(PrefLastImageDir, Path.GetDirectoryName(path));
            LoadImage(path);
        }

        private void LoadImage(string path)
        {
            // ★임포트 파이프라인을 우회해 PNG 바이트를 직접 읽는다 — Read/Write 토글 불요, 압축 아티팩트 배제.
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) { _status = $"로드 실패: {path}"; DestroyImmediate(tex); return; }

            _imagePath = path;
            _imgW = tex.width; _imgH = tex.height;
            _srcPixels = tex.GetPixels32();

            ReplaceTex(ref _srcTex, tex);
            _dirty = true;
            _status = $"로드: {Path.GetFileName(path)} ({_imgW}×{_imgH})";
        }

        private void SaveImageDialog()
        {
            string defDir = Path.GetDirectoryName(_imagePath);
            string defName = Path.GetFileName(_imagePath);
            // Source 밑에서 열었다면 Out 미러 경로를 기본 제안
            if (!string.IsNullOrEmpty(_workRoot))
            {
                string srcRoot = Path.Combine(_workRoot, "Source");
                string full = Path.GetFullPath(_imagePath);
                if (full.StartsWith(Path.GetFullPath(srcRoot), System.StringComparison.OrdinalIgnoreCase))
                {
                    string rel = full.Substring(Path.GetFullPath(srcRoot).Length).TrimStart('\\', '/');
                    string outPath = Path.Combine(_workRoot, "Out", rel);
                    defDir = Path.GetDirectoryName(outPath);
                    Directory.CreateDirectory(defDir);
                }
            }
            string path = EditorUtility.SaveFilePanel("결과 PNG 저장", defDir, defName, "png");
            if (string.IsNullOrEmpty(path)) return;

            var rts = RecolorEngine.BuildRuntimes(_preset);
            var outPixels = RecolorEngine.Process(_srcPixels, _imgW, _imgH, rts, _preset);
            WritePng(path, outPixels, _imgW, _imgH);
            _status = $"저장: {path}";
        }

        private static void WritePng(string path, Color32[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);
        }

        // ─────────────────────────── 프리셋 IO ───────────────────────────

        private string DefaultPresetPath()
            => string.IsNullOrEmpty(_workRoot) ? "" : Path.Combine(_workRoot, "preset.json");

        private void SavePresetDialog()
        {
            string path = EditorUtility.SaveFilePanel("프리셋 저장(JSON)",
                string.IsNullOrEmpty(_workRoot) ? "" : _workRoot, "preset", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, _preset.ToJson(), new UTF8Encoding(false));
            _status = $"프리셋 저장: {path}";
        }

        private void LoadPresetDialog()
        {
            string path = EditorUtility.OpenFilePanel("프리셋 열기(JSON)",
                string.IsNullOrEmpty(_workRoot) ? "" : _workRoot, "json");
            if (string.IsNullOrEmpty(path)) return;
            _preset = RecolorPreset.FromJson(File.ReadAllText(path));
            if (_preset.slots.Count == 0) _preset.slots.Add(new RecolorSlot { label = "슬롯 1" });
            _activeSlot = 0;
            _dirty = true;
            _status = $"프리셋 로드: {path}";
        }

        // ─────────────────────────── 프리뷰 재계산 ───────────────────────────

        private void RecomputePreview()
        {
            var rts = RecolorEngine.BuildRuntimes(_preset);
            var mask = new float[_srcPixels.Length];
            var outPixels = RecolorEngine.Process(_srcPixels, _imgW, _imgH, rts, _preset, _activeSlot, mask);

            var outTex = new Texture2D(_imgW, _imgH, TextureFormat.RGBA32, false);
            outTex.SetPixels32(outPixels);
            outTex.Apply(false);
            ReplaceTex(ref _outTex, outTex);

            // 마스크 뷰: 활성 슬롯의 w 흑백(투명 픽셀은 검정)
            var maskPixels = new Color32[_srcPixels.Length];
            for (int i = 0; i < mask.Length; i++)
            {
                byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(mask[i]) * 255f);
                maskPixels[i] = new Color32(v, v, v, 255);
            }
            var maskTex = new Texture2D(_imgW, _imgH, TextureFormat.RGBA32, false);
            maskTex.SetPixels32(maskPixels);
            maskTex.Apply(false);
            ReplaceTex(ref _maskTex, maskTex);
        }

        private static void ReplaceTex(ref Texture2D field, Texture2D fresh)
        {
            if (field != null && field != fresh) DestroyImmediate(field);
            fresh.filterMode = FilterMode.Point; // 픽셀 검수용 — 확대 시 또렷하게
            fresh.hideFlags = HideFlags.HideAndDontSave;
            field = fresh;
        }

        // ─────────────────────────── 배치 ───────────────────────────

        private void RunBatch(bool dryRun)
        {
            string srcRoot = Path.Combine(_workRoot, "Source");
            string outRoot = Path.Combine(_workRoot, "Out");
            if (!Directory.Exists(srcRoot))
            {
                _status = $"Source 폴더가 없습니다: {srcRoot}";
                return;
            }

            string[] files = Directory.GetFiles(srcRoot, "*.png", SearchOption.AllDirectories);
            var rows = new List<BatchRow>();
            int applied = 0, flagged = 0;

            try
            {
                for (int fi = 0; fi < files.Length; fi++)
                {
                    string file = files[fi];
                    string rel = Path.GetFullPath(file).Substring(Path.GetFullPath(srcRoot).Length).TrimStart('\\', '/');
                    if (EditorUtility.DisplayCancelableProgressBar("UI Recolor 배치",
                        $"{(dryRun ? "[드라이런] " : "")}{rel} ({fi + 1}/{files.Length})", (float)(fi + 1) / files.Length))
                        break;

                    byte[] bytes = File.ReadAllBytes(file);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(bytes))
                    {
                        rows.Add(new BatchRow { file = rel, slot = "-", deltaE = -1f, action = "skipped-loadfail" });
                        DestroyImmediate(tex);
                        continue;
                    }
                    Color32[] px = tex.GetPixels32();
                    int w = tex.width, h = tex.height;
                    DestroyImmediate(tex);

                    // ★이미지별 앵커 검출(절대 모드 슬롯만) — AI 편차를 앵커→목표 매핑으로 흡수
                    var anchors = new Vector3?[_preset.slots.Count];
                    bool anyFlag = false;
                    for (int si = 0; si < _preset.slots.Count; si++)
                    {
                        var s = _preset.slots[si];
                        if (!s.enabled) { rows.Add(new BatchRow { file = rel, slot = s.label, deltaE = -1f, action = "disabled" }); continue; }
                        Vector3 refLch = RecolorMath.SrgbToLch(s.referenceColor);
                        Vector3? anchor = RecolorEngine.DetectAnchor(px, s, refLch);
                        if (!s.absoluteMode)
                        {
                            // ★상대 모드도 앵커 검출은 수행 — 적용은 그대로 하되, 편차 큰 이미지가
                            //   조용히 통과하지 않도록 리포트에 경고를 남긴다(절대 모드와 달리 스킵하지 않음).
                            if (!anchor.HasValue)
                            {
                                rows.Add(new BatchRow { file = rel, slot = s.label, deltaE = -1f, action = "applied-relative-nocolor" });
                            }
                            else
                            {
                                float rdE = RecolorMath.DeltaE(anchor.Value, refLch);
                                bool drift = rdE > _preset.batchSkipDeltaE;
                                if (drift) anyFlag = true;
                                rows.Add(new BatchRow { file = rel, slot = s.label, deltaE = rdE, action = drift ? "applied-relative-DRIFT" : "applied-relative" });
                            }
                            continue;
                        }
                        if (!anchor.HasValue)
                        {
                            anchors[si] = null; anyFlag = true;
                            rows.Add(new BatchRow { file = rel, slot = s.label, deltaE = -1f, action = "skipped-noanchor" });
                            continue;
                        }
                        float dE = RecolorMath.DeltaE(anchor.Value, refLch);
                        if (dE > _preset.batchSkipDeltaE)
                        {
                            anchors[si] = null; anyFlag = true;
                            rows.Add(new BatchRow { file = rel, slot = s.label, deltaE = dE, action = "skipped-drift" });
                            continue;
                        }
                        anchors[si] = anchor;
                        rows.Add(new BatchRow { file = rel, slot = s.label, deltaE = dE, action = "applied" });
                    }
                    if (anyFlag) flagged++;

                    if (!dryRun)
                    {
                        var rts = RecolorEngine.BuildRuntimes(_preset, anchors);
                        var outPx = RecolorEngine.Process(px, w, h, rts, _preset);
                        WritePng(Path.Combine(outRoot, rel), outPx, w, h);
                        applied++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            WriteReport(rows, dryRun);
            _status = dryRun
                ? $"드라이런 완료: {files.Length}파일 검사, 플래그 {flagged}건 — report.csv 확인"
                : $"배치 완료: {applied}파일 저장, 플래그 {flagged}건 — report.csv 확인";
            Debug.Log($"[UiRecolorForge] {_status}");
        }

        private void WriteReport(List<BatchRow> rows, bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# UI Recolor 배치 리포트 ({(dryRun ? "드라이런" : "적용")})");
            sb.AppendLine("file,slot,deltaE,action");
            foreach (var r in rows)
                sb.AppendLine($"{r.file},{r.slot},{(r.deltaE < 0f ? "-" : r.deltaE.ToString("0.0000"))},{r.action}");
            // UTF-8 BOM — 엑셀에서 한글 슬롯명 안 깨지게
            File.WriteAllText(Path.Combine(_workRoot, "report.csv"), sb.ToString(), new UTF8Encoding(true));
        }
    }
}
