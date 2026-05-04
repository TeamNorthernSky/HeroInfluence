using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Orora.ImageObjectForge
{
    public class ImageObjectForgeWindow : EditorWindow
    {
        public enum ToolMode { Brush, Pen, Marquee, Outline }
        public enum BrushMode { Paint, Erase }
        public enum MarqueeShape { Rect, Ellipse }

        // Outline 기본값
        const float OutlineDefaultColorTol = 12f;
        const float OutlineDefaultSigma = 1.0f;
        const float OutlineDefaultCannyLow = 50f;
        const float OutlineDefaultCannyHigh = 150f;

        const float SidebarWidth = 220f;
        const float ToolbarHeight = 28f;
        const float StatusHeight = 22f;

        [MenuItem("Tools/Image Object Forge/Open Editor", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<ImageObjectForgeWindow>("Image Object Forge");
            w.minSize = new Vector2(960, 640);
        }

        ForgeDocument _doc;
        ForgeViewport _vp;
        ForgeUndoStack _undo;

        ToolMode _tool = ToolMode.Brush;
        BrushMode _brushMode = BrushMode.Paint;
        ForgeBrush.BrushShape _brushShape = ForgeBrush.BrushShape.Circle;
        float _brushRadius = 12f;

        // Pen 상태 (베지어)
        readonly List<PenVertex> _penVerts = new List<PenVertex>();
        const float PenCloseDistScreen = 10f;
        const float PenHandleHitDist = 8f;
        bool _penDraggingHandle;       // 핸들 또는 새 버텍스 드래그 중
        int _penDragVertIdx = -1;      // 드래그 대상 버텍스 인덱스
        bool _penDragIsOut;            // true=HandleOut, false=HandleIn
        bool _penDragIndependent;      // Alt 홀드 시 독립 핸들
        bool _penPlacingNew;           // 새 버텍스 배치 + 핸들 드래그 중
        bool _penClosingDrag;          // 닫기 클릭 후 첫 버텍스 핸들 드래그 중 (MouseUp 시 commit)

        // 브러시 스트로크
        bool _strokeActive;
        Vector2 _strokeLastImg;
        byte[] _preOpMask;

        // 마키(사각/타원 드래그) 상태
        bool _marqueeActive;
        bool _marqueeAdd = true;            // 사이드바 모드 (영구)
        bool _marqueeEffectiveAdd = true;   // 이번 드래그의 실제 동작 (Alt 적용 후)
        MarqueeShape _marqueeShape = MarqueeShape.Rect;
        Vector2 _marqueeStartImg;           // 시작점 (이미지 y-up)
        Vector2 _marqueeCurImg;             // 현재 점

        // Outline (외곽선 검출) 상태
        bool _outlineActive;                                // 시드가 1개 이상 있고 미리보기 표시 중
        readonly List<Vector2Int> _outlineSeeds = new List<Vector2Int>();
        bool _outlineAdd = true;                            // 사이드바 모드 (영구)
        bool _outlineEffectiveAdd = true;                   // 이번 시드 추가 시점에서 Alt 일회성 적용
        bool _outlineEdgeAware = true;                      // Canny 가중치 사용
        float _outlineColorTol = OutlineDefaultColorTol;    // LAB 거리 임계
        float _outlineCannySigma = OutlineDefaultSigma;
        float _outlineCannyLow = OutlineDefaultCannyLow;
        float _outlineCannyHigh = OutlineDefaultCannyHigh;
        ForgeOutlineDetect.SourceCache _outlineCache;
        string _outlineCacheSourcePath;                     // 캐시가 어떤 SourceAssetPath 기준인지
        float _outlineCachedSigma = -1f, _outlineCachedLow = -1f, _outlineCachedHigh = -1f; // 마지막 Canny 파라미터
        byte[] _outlinePreviewMask;                         // 후보 마스크 (Fill Holes 적용 후)
        Texture2D _outlinePreviewTex;
        bool _outlineShowCanny;                             // Canny edge map 직접 표시
        Texture2D _cannyEdgeTex;

        // 뷰 내비게이션
        bool _spaceDown;
        bool _panDragging;
        Vector2 _panLastScreen;

        // 마우스 위치(이미지 좌표, y-up). 현재 커서가 캔버스 위에 있을 때만 유효.
        Vector2 _cursorImg;
        bool _cursorInside;

        string _statusMsg;
        double _statusExpireAt;

        void OnEnable()
        {
            _doc = new ForgeDocument();
            _vp = new ForgeViewport();
            _undo = new ForgeUndoStack();
            wantsMouseMove = true;
            wantsMouseEnterLeaveWindow = true;
        }

        void OnDisable()
        {
            ReleaseOutlinePreviewTex();
            ReleaseCannyEdgeTex();
            _outlineCache = null;
            _doc?.DisposeInternal();
        }

        void OnGUI()
        {
            var e = Event.current;

            // Space 키 트래킹
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
            { _spaceDown = true; Repaint(); }
            else if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Space)
            { _spaceDown = false; Repaint(); }

            DrawToolbar();

            var body = new Rect(0, ToolbarHeight, position.width, position.height - ToolbarHeight - StatusHeight);
            var sidebar = new Rect(body.x, body.y, SidebarWidth, body.height);
            var canvas = new Rect(body.x + SidebarWidth, body.y, body.width - SidebarWidth, body.height);

            DrawSidebar(sidebar);
            DrawCanvas(canvas);
            DrawStatusBar(new Rect(0, position.height - StatusHeight, position.width, StatusHeight));

            HandleGlobalShortcuts(e);

            if (e.type == EventType.MouseMove) Repaint();
        }

        // -------- Toolbar --------
        void DrawToolbar()
        {
            var rect = new Rect(0, 0, position.width, ToolbarHeight);
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            GUILayout.BeginArea(rect);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Load…", EditorStyles.toolbarButton, GUILayout.Width(70))) EditorApplication.delayCall += DoLoad;
                GUI.enabled = _doc.HasImage;
                if (GUILayout.Button("Save…", EditorStyles.toolbarButton, GUILayout.Width(70))) EditorApplication.delayCall += DoSave;
                GUI.enabled = true;
                GUILayout.Space(12);
                GUI.enabled = _undo.CanUndo;
                if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(60))) DoUndo();
                GUI.enabled = _undo.CanRedo;
                if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(60))) DoRedo();
                GUI.enabled = true;
                GUILayout.Space(12);
                GUI.enabled = _doc.HasImage;
                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(50))) _vp.Fit(CanvasRectCached(), _doc.Width, _doc.Height);
                if (GUILayout.Button("Clear Mask", EditorStyles.toolbarButton, GUILayout.Width(90))) DoClearMask();
                GUI.enabled = true;
                GUILayout.Space(12);
                if (GUILayout.Button("Create Prefab…", EditorStyles.toolbarButton, GUILayout.Width(110))) ImageObjectForgePrefabWindow.Open();
                GUILayout.FlexibleSpace();
                GUILayout.Label(_doc.HasImage ? $"{_doc.Width} × {_doc.Height}" : "(no image)", EditorStyles.miniLabel);
            }
            GUILayout.EndArea();
        }

        Rect _cachedCanvasRect;
        Rect CanvasRectCached() => _cachedCanvasRect;

        // -------- Sidebar --------
        void DrawSidebar(Rect r)
        {
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 16));

            EditorGUILayout.LabelField("Tool", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (ToggleButton(_tool == ToolMode.Brush, "Brush (B)")) SwitchTool(ToolMode.Brush);
                if (ToggleButton(_tool == ToolMode.Pen, "Pen (P)")) SwitchTool(ToolMode.Pen);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (ToggleButton(_tool == ToolMode.Marquee, "Marquee (M)")) SwitchTool(ToolMode.Marquee);
                if (ToggleButton(_tool == ToolMode.Outline, "Outline (E)")) SwitchTool(ToolMode.Outline);
            }

            EditorGUILayout.Space(8);

            if (_tool == ToolMode.Brush)
            {
                EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToggleButton(_brushMode == BrushMode.Paint, "Paint")) _brushMode = BrushMode.Paint;
                    if (ToggleButton(_brushMode == BrushMode.Erase, "Erase")) _brushMode = BrushMode.Erase;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToggleButton(_brushShape == ForgeBrush.BrushShape.Circle, "Circle")) _brushShape = ForgeBrush.BrushShape.Circle;
                    if (ToggleButton(_brushShape == ForgeBrush.BrushShape.Square, "Square")) _brushShape = ForgeBrush.BrushShape.Square;
                }
                EditorGUILayout.LabelField("Size", Mathf.RoundToInt(_brushRadius * 2).ToString() + " px");
                _brushRadius = EditorGUILayout.Slider(_brushRadius, 1f, 256f);
                EditorGUILayout.LabelField("[ ]  키: 크기 조절", EditorStyles.miniLabel);
            }
            else if (_tool == ToolMode.Marquee)
            {
                EditorGUILayout.LabelField("Marquee", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToggleButton(_marqueeAdd, "Add (Paint)")) _marqueeAdd = true;
                    if (ToggleButton(!_marqueeAdd, "Subtract (Erase)")) _marqueeAdd = false;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToggleButton(_marqueeShape == MarqueeShape.Rect, "Rect")) _marqueeShape = MarqueeShape.Rect;
                    if (ToggleButton(_marqueeShape == MarqueeShape.Ellipse, "Ellipse")) _marqueeShape = MarqueeShape.Ellipse;
                }
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("드래그: bbox 영역 선택", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Shift+드래그: 정사각/정원", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Alt+드래그: Subtract 강제 (사이드바 무시)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Esc: 드래그 취소", EditorStyles.miniLabel);
            }
            else if (_tool == ToolMode.Outline)
            {
                EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToggleButton(_outlineAdd, "Add (Paint)")) _outlineAdd = true;
                    if (ToggleButton(!_outlineAdd, "Subtract (Erase)")) _outlineAdd = false;
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Color Tolerance (LAB)", _outlineColorTol.ToString("0.0"));
                float newTol = EditorGUILayout.Slider(_outlineColorTol, 1f, 80f);
                if (!Mathf.Approximately(newTol, _outlineColorTol))
                {
                    _outlineColorTol = newTol;
                    if (_outlineActive) RecomputeOutlinePreview();
                }

                bool newEdgeAware = EditorGUILayout.Toggle("Edge-aware (Canny)", _outlineEdgeAware);
                if (newEdgeAware != _outlineEdgeAware)
                {
                    _outlineEdgeAware = newEdgeAware;
                    if (_outlineEdgeAware) EnsureOutlineEdgeReady();
                    if (_outlineActive) RecomputeOutlinePreview();
                }

                bool newShowCanny = EditorGUILayout.Toggle("Show Canny Edges", _outlineShowCanny);
                if (newShowCanny != _outlineShowCanny)
                {
                    _outlineShowCanny = newShowCanny;
                    if (_outlineShowCanny && _doc.HasImage)
                    {
                        EnsureOutlineCacheReady();
                        EnsureOutlineEdgeReady();
                        RebuildCannyEdgeTex();
                    }
                    else
                    {
                        ReleaseCannyEdgeTex();
                    }
                }

                bool cannySlidersEnabled = _outlineEdgeAware || _outlineShowCanny;
                using (new EditorGUI.DisabledScope(!cannySlidersEnabled))
                {
                    EditorGUILayout.LabelField("Canny σ", _outlineCannySigma.ToString("0.00"));
                    float ns = EditorGUILayout.Slider(_outlineCannySigma, 0.5f, 3f);
                    EditorGUILayout.LabelField("Canny Low", _outlineCannyLow.ToString("0"));
                    float nl = EditorGUILayout.Slider(_outlineCannyLow, 0f, 200f);
                    EditorGUILayout.LabelField("Canny High", _outlineCannyHigh.ToString("0"));
                    float nh = EditorGUILayout.Slider(_outlineCannyHigh, 50f, 400f);
                    if (nh < nl) nh = nl;
                    bool cannyChanged = !Mathf.Approximately(ns, _outlineCannySigma)
                                        || !Mathf.Approximately(nl, _outlineCannyLow)
                                        || !Mathf.Approximately(nh, _outlineCannyHigh);
                    _outlineCannySigma = ns;
                    _outlineCannyLow = nl;
                    _outlineCannyHigh = nh;
                    if (cannyChanged && cannySlidersEnabled)
                    {
                        EnsureOutlineEdgeReady();
                        if (_outlineShowCanny) RebuildCannyEdgeTex();
                        if (_outlineActive && _outlineEdgeAware) RecomputeOutlinePreview();
                    }
                }

                EditorGUILayout.Space(4);
                GUI.enabled = _outlineActive;
                if (GUILayout.Button("Confirm (Enter)", GUILayout.Height(24))) CommitOutline();
                if (GUILayout.Button("Cancel (Esc)", GUILayout.Height(20))) ClearOutline();
                GUI.enabled = true;

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Seeds: {_outlineSeeds.Count}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("클릭: 새 시드 (기존 초기화)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Shift+클릭: 시드 추가 (Union)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Alt+클릭: Subtract 강제 (사이드바 무시)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Enter: 확정 / Esc: 취소", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Pen (Bezier)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Vertices: {_penVerts.Count}");
                GUI.enabled = _penVerts.Count >= 3;
                if (GUILayout.Button("Add (Enter)", GUILayout.Height(24))) CommitPen(true);
                if (GUILayout.Button("Subtract (Shift+Enter)", GUILayout.Height(24))) CommitPen(false);
                GUI.enabled = _penVerts.Count > 0;
                if (GUILayout.Button("Cancel (Esc)", GUILayout.Height(20))) ClearPen();
                GUI.enabled = true;
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("클릭: 코너 포인트", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("클릭+드래그: 스무스 (핸들 생성)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Alt+드래그: 핸들 독립 조작", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Alt+클릭 앵커: 코너로 변환", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("시작점 근처 클릭 = 닫고 Add", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Backspace: 직전 버텍스 제거", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Zoom", (_vp.Zoom * 100f).ToString("0.#") + " %");
            EditorGUILayout.LabelField("휠: 줌, 중클릭/Space+드래그: 팬", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("F: 뷰 피팅", EditorStyles.miniLabel);

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Shortcuts", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Ctrl+Z / Ctrl+Y : Undo/Redo", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Ctrl+O / Ctrl+S : Load/Save", EditorStyles.miniLabel);

            GUILayout.EndArea();
        }

        static bool ToggleButton(bool active, string label)
        {
            var style = new GUIStyle(GUI.skin.button);
            if (active) style.normal.background = style.active.background;
            return GUILayout.Button(label, active ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Height(22));
        }

        // -------- Canvas --------
        void DrawCanvas(Rect canvasRect)
        {
            _cachedCanvasRect = canvasRect;
            EditorGUI.DrawRect(canvasRect, new Color(0.18f, 0.18f, 0.18f));

            if (!_doc.HasImage)
            {
                var center = new Rect(canvasRect.x, canvasRect.y + canvasRect.height * 0.5f - 10, canvasRect.width, 20);
                var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(center, "Load 버튼으로 이미지 로드", style);
                return;
            }

            var imgRect = _vp.GetImageDisplayRect(canvasRect, _doc.Width, _doc.Height);

            // 클리핑을 위해 BeginGroup 사용
            GUI.BeginClip(canvasRect);
            var localImgRect = new Rect(imgRect.x - canvasRect.x, imgRect.y - canvasRect.y, imgRect.width, imgRect.height);

            // 체커보드 배경 (타일 텍스처 1회 draw)
            ForgeGfx.DrawCheckerboard(localImgRect);

            GUI.DrawTexture(localImgRect, _doc.Source, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(localImgRect, _doc.MaskOverlay, ScaleMode.StretchToFill, true);

            // 이미지 테두리
            ForgeGfx.DrawRectOutline(localImgRect, new Color(1f, 1f, 1f, 0.25f), 1f);

            // Pen 미리보기 (local 좌표 기준)
            if (_tool == ToolMode.Pen && _penVerts.Count > 0)
            {
                DrawPenPreviewClipped(canvasRect);
            }

            // 마키 미리보기
            if (_tool == ToolMode.Marquee && _marqueeActive)
            {
                DrawMarqueePreviewClipped(canvasRect);
            }

            // Outline 미리보기 (Canny edges → 후보 마스크 오버레이 → 시드 마커)
            if (_tool == ToolMode.Outline)
            {
                if (_outlineShowCanny && _cannyEdgeTex != null)
                    GUI.DrawTexture(localImgRect, _cannyEdgeTex, ScaleMode.StretchToFill, true);
                if (_outlineActive && _outlinePreviewTex != null)
                    GUI.DrawTexture(localImgRect, _outlinePreviewTex, ScaleMode.StretchToFill, true);
                if (_outlineSeeds.Count > 0)
                {
                    Color seedCol = _outlineEffectiveAdd
                        ? new Color(0.25f, 1f, 0.4f, 1f)
                        : new Color(1f, 0.3f, 0.3f, 1f);
                    for (int i = 0; i < _outlineSeeds.Count; i++)
                    {
                        var s = _outlineSeeds[i];
                        var sImg = new Vector2(s.x + 0.5f, s.y + 0.5f);
                        var local = ImgToLocal(canvasRect, sImg);
                        ForgeGfx.DrawSeedMarker(local, seedCol);
                    }
                }
            }

            // 브러시 커서
            if (_tool == ToolMode.Brush && _cursorInside && !_spaceDown && !_panDragging)
            {
                var sp = _vp.ImageToScreen(canvasRect, _cursorImg, _doc.Height);
                var local = new Vector2(sp.x - canvasRect.x, sp.y - canvasRect.y);
                Color c = _brushMode == BrushMode.Paint
                    ? new Color(0.25f, 1f, 0.4f, 0.95f)
                    : new Color(1f, 0.3f, 0.3f, 0.95f);
                float rScreen = _brushRadius * _vp.Zoom;
                if (_brushShape == ForgeBrush.BrushShape.Square)
                {
                    var rect = new Rect(local.x - rScreen, local.y - rScreen, rScreen * 2f, rScreen * 2f);
                    ForgeGfx.DrawRectOutline(rect, c, 1.5f);
                }
                else
                {
                    ForgeGfx.DrawRing(local, rScreen, c);
                }

                if (_brushMode == BrushMode.Erase && !_strokeActive)
                {
                    var labelRect = new Rect(local.x - rScreen - 2f, local.y - rScreen - 14f, 40f, 14f);
                    ForgeGfx.FilledRect(labelRect, new Color(0f, 0f, 0f, 0.3f));
                    var st = new GUIStyle(EditorStyles.miniLabel);
                    st.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
                    GUI.Label(labelRect, " Erase", st);
                }
            }

            GUI.EndClip();

            HandleCanvasEvents(canvasRect);
        }

        Vector2 ImgToLocal(Rect canvasRect, Vector2 imgPt)
        {
            return _vp.ImageToScreen(canvasRect, imgPt, _doc.Height) - canvasRect.position;
        }

        void DrawPenPreviewClipped(Rect canvasRect)
        {
            var col = new Color(1f, 0.7f, 0.2f, 1f);
            var colDim = new Color(1f, 0.7f, 0.2f, 0.5f);
            var colHandle = new Color(0.5f, 0.8f, 1f, 0.9f);
            var colFirst = new Color(0.4f, 1f, 0.4f, 1f);

            // 확정된 세그먼트 곡선 그리기
            if (_penVerts.Count >= 2)
            {
                var flat = ForgeBezier.FlattenOpenPath(_penVerts, 1f / _vp.Zoom);
                for (int i = 0; i < flat.Count - 1; i++)
                    ForgeGfx.DrawLine(ImgToLocal(canvasRect, flat[i]), ImgToLocal(canvasRect, flat[i + 1]), col, 2f);
            }

            // 닫기 드래그 중이면 닫는 세그먼트 (마지막 → 첫) 도 그리기
            if (_penClosingDrag && _penVerts.Count >= 2)
            {
                var last = _penVerts[_penVerts.Count - 1];
                var first = _penVerts[0];
                Vector2 cp1 = last.IsCorner ? last.Anchor : last.HandleOut;
                Vector2 cp2 = first.IsCorner ? first.Anchor : first.HandleIn;
                var seg = new System.Collections.Generic.List<Vector2> { last.Anchor };
                ForgeBezier.FlattenSegment(last.Anchor, cp1, cp2, first.Anchor, seg, 1f / _vp.Zoom);
                for (int i = 0; i < seg.Count - 1; i++)
                    ForgeGfx.DrawLine(ImgToLocal(canvasRect, seg[i]), ImgToLocal(canvasRect, seg[i + 1]), colFirst, 2f);
            }

            // 커서까지의 임시 세그먼트 프리뷰
            if (_cursorInside && _penVerts.Count >= 1 && !_penDraggingHandle)
            {
                var last = _penVerts[_penVerts.Count - 1];
                var cursorLocal = ImgToLocal(canvasRect, _cursorImg);
                // 마지막 버텍스의 handleOut → 커서까지 직선 or 곡선
                if (last.HasHandleOut)
                {
                    var tempPts = new List<Vector2>();
                    tempPts.Add(last.Anchor);
                    ForgeBezier.FlattenSegment(last.Anchor, last.HandleOut, _cursorImg, _cursorImg, tempPts, 1f / _vp.Zoom);
                    for (int i = 0; i < tempPts.Count - 1; i++)
                        ForgeGfx.DrawLine(ImgToLocal(canvasRect, tempPts[i]), ImgToLocal(canvasRect, tempPts[i + 1]), colDim, 1.5f);
                }
                else
                {
                    ForgeGfx.DrawLine(ImgToLocal(canvasRect, last.Anchor), cursorLocal, colDim, 1.5f);
                }

                // 닫기 가능 프리뷰
                if (_penVerts.Count >= 3)
                {
                    var firstScr = ImgToLocal(canvasRect, _penVerts[0].Anchor);
                    bool canClose = Vector2.Distance(firstScr, cursorLocal) <= PenCloseDistScreen;
                    var closeCol = canClose ? colFirst : colDim;
                    ForgeGfx.DrawLine(cursorLocal, firstScr, closeCol, canClose ? 2f : 1f);
                }
            }

            // 앵커 + 핸들 그리기
            for (int i = 0; i < _penVerts.Count; i++)
            {
                var v = _penVerts[i];
                var aLocal = ImgToLocal(canvasRect, v.Anchor);

                // 핸들 선 + 점
                if (v.HasHandleIn)
                {
                    var hIn = ImgToLocal(canvasRect, v.HandleIn);
                    ForgeGfx.DrawLine(aLocal, hIn, colHandle, 1f);
                    ForgeGfx.FilledRect(new Rect(hIn.x - 3, hIn.y - 3, 6, 6), colHandle);
                }
                if (v.HasHandleOut)
                {
                    var hOut = ImgToLocal(canvasRect, v.HandleOut);
                    ForgeGfx.DrawLine(aLocal, hOut, colHandle, 1f);
                    ForgeGfx.FilledRect(new Rect(hOut.x - 3, hOut.y - 3, 6, 6), colHandle);
                }

                // 앵커 점 (사각)
                bool isFirst = (i == 0);
                var dotCol = isFirst ? colFirst : col;
                float dotSize = isFirst ? 8f : 6f;
                ForgeGfx.FilledRect(new Rect(aLocal.x - dotSize * 0.5f, aLocal.y - dotSize * 0.5f, dotSize, dotSize), dotCol);
            }
        }

        void HandleCanvasEvents(Rect canvasRect)
        {
            var e = Event.current;
            bool over = canvasRect.Contains(e.mousePosition);

            // 마우스 위치 갱신
            if (over && _doc.HasImage)
            {
                _cursorInside = true;
                _cursorImg = _vp.ScreenToImage(canvasRect, e.mousePosition, _doc.Height);
            }
            else
            {
                _cursorInside = false;
            }

            if (!_doc.HasImage) return;

            // Scroll Wheel: Zoom
            if (e.type == EventType.ScrollWheel && over)
            {
                _vp.ZoomAt(canvasRect, e.mousePosition, e.delta.y, _doc.Height);
                e.Use();
                Repaint();
                return;
            }

            // 팬 시작/진행/종료
            bool panInit = over && e.type == EventType.MouseDown && (e.button == 2 || (e.button == 0 && _spaceDown));
            if (panInit)
            {
                _panDragging = true;
                _panLastScreen = e.mousePosition;
                e.Use();
                return;
            }
            if (_panDragging)
            {
                if (e.type == EventType.MouseDrag)
                {
                    _vp.PanBy(e.mousePosition - _panLastScreen);
                    _panLastScreen = e.mousePosition;
                    e.Use();
                    Repaint();
                    return;
                }
                if (e.type == EventType.MouseUp)
                {
                    _panDragging = false;
                    e.Use();
                    Repaint();
                    return;
                }
            }

            // 브러시
            if (_tool == ToolMode.Brush && over && !_spaceDown)
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    StartBrushStroke();
                    var imgPt = _vp.ScreenToImage(canvasRect, e.mousePosition, _doc.Height);
                    _strokeLastImg = imgPt;
                    bool paint = _brushMode == BrushMode.Paint;
                    if (ForgeBrush.Stamp(_doc.Mask, _doc.Width, _doc.Height, imgPt, _brushRadius, paint, _brushShape, out var d))
                        _doc.RebuildOverlayRect(d);
                    else
                        _doc.RebuildOverlayRect(d);
                    e.Use();
                    Repaint();
                    return;
                }
                if (_strokeActive && e.type == EventType.MouseDrag && e.button == 0)
                {
                    var imgPt = _vp.ScreenToImage(canvasRect, e.mousePosition, _doc.Height);
                    bool paint = _brushMode == BrushMode.Paint;
                    if (ForgeBrush.Stroke(_doc.Mask, _doc.Width, _doc.Height, _strokeLastImg, imgPt, _brushRadius, paint, _brushShape, out var d))
                        _doc.RebuildOverlayRect(d);
                    else
                        _doc.RebuildOverlayRect(d);
                    _strokeLastImg = imgPt;
                    e.Use();
                    Repaint();
                    return;
                }
                if (_strokeActive && e.type == EventType.MouseUp && e.button == 0)
                {
                    EndBrushStroke();
                    e.Use();
                    Repaint();
                    return;
                }
            }

            // 펜툴 (베지어)
            if (_tool == ToolMode.Pen && !_spaceDown)
            {
                HandlePenEvents(canvasRect, e, over);
            }

            // 마키 (사각형 드래그)
            if (_tool == ToolMode.Marquee && !_spaceDown)
            {
                HandleMarqueeEvents(canvasRect, e, over);
            }

            // Outline (시드 클릭 + 미리보기 + 확정)
            if (_tool == ToolMode.Outline && !_spaceDown)
            {
                HandleOutlineEvents(canvasRect, e, over);
            }
        }

        void StartBrushStroke()
        {
            _strokeActive = true;
            _preOpMask = (byte[])_doc.Mask.Clone();
        }

        void EndBrushStroke()
        {
            if (!_strokeActive) return;
            _strokeActive = false;
            if (_preOpMask != null)
            {
                _undo.Push(_preOpMask, _doc.Mask, _doc.Width, _doc.Height);
                _preOpMask = null;
            }
        }

        // -------- Pen 이벤트 (베지어) --------
        void HandlePenEvents(Rect canvasRect, Event e, bool over)
        {
            // 드래그 진행 중 (핸들 조정 또는 새 포인트 핸들 드래그)
            if (_penDraggingHandle)
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    var imgPt = _vp.ScreenToImage(canvasRect, e.mousePosition, _doc.Height);
                    var v = _penVerts[_penDragVertIdx];
                    if (_penDragIndependent || e.alt)
                    {
                        // 독립 핸들 조작
                        if (_penDragIsOut) v.HandleOut = imgPt;
                        else v.HandleIn = imgPt;
                        v.IsCorner = false;
                    }
                    else
                    {
                        // 대칭 핸들
                        if (_penDragIsOut) v.SetHandleOutSymmetric(imgPt);
                        else v.SetHandleInSymmetric(imgPt);
                        v.IsCorner = false;
                    }
                    _penVerts[_penDragVertIdx] = v;
                    e.Use();
                    Repaint();
                    return;
                }
                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    bool wasClosing = _penClosingDrag;
                    _penDraggingHandle = false;
                    _penPlacingNew = false;
                    _penClosingDrag = false;
                    e.Use();
                    if (wasClosing) CommitPen(true);
                    else Repaint();
                    return;
                }
                return;
            }

            if (!over) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var imgPt = _vp.ScreenToImage(canvasRect, e.mousePosition, _doc.Height);

                // Alt+클릭 기존 앵커 → 코너 변환
                if (e.alt)
                {
                    int hitIdx = HitTestAnchor(canvasRect, e.mousePosition);
                    if (hitIdx >= 0)
                    {
                        var v = _penVerts[hitIdx];
                        v.MakeCorner();
                        _penVerts[hitIdx] = v;
                        e.Use();
                        Repaint();
                        return;
                    }
                    // Alt+클릭 기존 핸들 → 독립 드래그 시작
                    if (HitTestHandle(canvasRect, e.mousePosition, out int hIdx, out bool isOut))
                    {
                        _penDraggingHandle = true;
                        _penDragVertIdx = hIdx;
                        _penDragIsOut = isOut;
                        _penDragIndependent = true;
                        e.Use();
                        return;
                    }
                }

                // Ctrl+클릭 기존 핸들 → 해당 핸들만 제거 (앵커로 수축)
                if (e.control)
                {
                    if (HitTestHandle(canvasRect, e.mousePosition, out int cIdx, out bool cIsOut))
                    {
                        var v = _penVerts[cIdx];
                        if (cIsOut) v.HandleOut = v.Anchor;
                        else v.HandleIn = v.Anchor;
                        if (!v.HasHandleIn && !v.HasHandleOut) v.IsCorner = true;
                        _penVerts[cIdx] = v;
                        e.Use();
                        Repaint();
                        return;
                    }
                }

                // 시작점 근처 클릭 → 첫 버텍스 핸들 드래그 모드 진입 (MouseUp에서 닫고 commit)
                if (_penVerts.Count >= 3 && !e.alt && !e.control)
                {
                    var first = _vp.ImageToScreen(canvasRect, _penVerts[0].Anchor, _doc.Height);
                    if (Vector2.Distance(first, e.mousePosition) <= PenCloseDistScreen)
                    {
                        _penDraggingHandle = true;
                        _penDragVertIdx = 0;
                        _penDragIsOut = true;
                        _penDragIndependent = false;
                        _penClosingDrag = true;
                        e.Use();
                        Repaint();
                        return;
                    }
                }

                // 기존 핸들 히트 → 대칭 드래그
                if (HitTestHandle(canvasRect, e.mousePosition, out int handleIdx, out bool handleIsOut))
                {
                    _penDraggingHandle = true;
                    _penDragVertIdx = handleIdx;
                    _penDragIsOut = handleIsOut;
                    _penDragIndependent = false;
                    e.Use();
                    return;
                }

                // 새 버텍스 배치 (코너로 시작, 드래그하면 스무스로 전환)
                _penVerts.Add(PenVertex.Corner(imgPt));
                _penDraggingHandle = true;
                _penDragVertIdx = _penVerts.Count - 1;
                _penDragIsOut = true;
                _penDragIndependent = false;
                _penPlacingNew = true;
                e.Use();
                Repaint();
                return;
            }

            if (e.type == EventType.MouseMove)
            {
                Repaint();
            }
        }

        int HitTestAnchor(Rect canvasRect, Vector2 screenPos)
        {
            for (int i = 0; i < _penVerts.Count; i++)
            {
                var sp = _vp.ImageToScreen(canvasRect, _penVerts[i].Anchor, _doc.Height);
                if (Vector2.Distance(sp, screenPos) <= PenHandleHitDist) return i;
            }
            return -1;
        }

        bool HitTestHandle(Rect canvasRect, Vector2 screenPos, out int vertIdx, out bool isOut)
        {
            vertIdx = -1; isOut = false;
            for (int i = 0; i < _penVerts.Count; i++)
            {
                var v = _penVerts[i];
                if (v.HasHandleOut)
                {
                    var sp = _vp.ImageToScreen(canvasRect, v.HandleOut, _doc.Height);
                    if (Vector2.Distance(sp, screenPos) <= PenHandleHitDist) { vertIdx = i; isOut = true; return true; }
                }
                if (v.HasHandleIn)
                {
                    var sp = _vp.ImageToScreen(canvasRect, v.HandleIn, _doc.Height);
                    if (Vector2.Distance(sp, screenPos) <= PenHandleHitDist) { vertIdx = i; isOut = false; return true; }
                }
            }
            return false;
        }

        void ClearPen()
        {
            _penVerts.Clear();
            _penDraggingHandle = false;
            _penPlacingNew = false;
            _penClosingDrag = false;
        }

        void CommitPen(bool add)
        {
            if (_penVerts.Count < 3) return;
            // 베지어 경로를 폴리곤 점 목록으로 평탄화
            var flatPts = ForgeBezier.FlattenPath(_penVerts, 0.5f);
            if (flatPts.Count < 3) { ClearPen(); return; }
            var pre = (byte[])_doc.Mask.Clone();
            var dirty = ForgePolygon.FillToMask(_doc.Mask, _doc.Width, _doc.Height, flatPts, add);
            if (dirty.width > 0 && dirty.height > 0)
            {
                _undo.PushHinted(pre, _doc.Mask, _doc.Width, dirty);
                _doc.RebuildOverlayRect(dirty);
            }
            ClearPen();
            SetStatus(add ? "베지어 Add 완료" : "베지어 Subtract 완료");
            Repaint();
        }

        // -------- Marquee --------
        void HandleMarqueeEvents(Rect canvasRect, Event e, bool over)
        {
            if (_marqueeActive)
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    _marqueeCurImg = _vp.ScreenToImage(canvasRect, e.mousePosition, _doc.Height);
                    e.Use(); Repaint();
                    return;
                }
                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    CommitMarquee(e.shift);
                    _marqueeActive = false;
                    e.Use(); Repaint();
                    return;
                }
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    _marqueeActive = false;
                    e.Use(); Repaint();
                    return;
                }
                return;
            }

            if (!over) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                _marqueeStartImg = _vp.ScreenToImage(canvasRect, e.mousePosition, _doc.Height);
                _marqueeCurImg = _marqueeStartImg;
                // 사이드바 모드(_marqueeAdd)는 영구. 이번 드래그만 Alt가 눌리면 Subtract 강제.
                _marqueeEffectiveAdd = e.alt ? false : _marqueeAdd;
                _marqueeActive = true;
                e.Use(); Repaint();
                return;
            }
        }

        void CommitMarquee(bool square)
        {
            var rect = ForgeMarquee.RectFromPoints(_marqueeStartImg, _marqueeCurImg, square);
            if (rect.width <= 0 || rect.height <= 0) return;
            var pre = (byte[])_doc.Mask.Clone();
            RectInt dirty = _marqueeShape == MarqueeShape.Ellipse
                ? ForgeMarquee.FillEllipse(_doc.Mask, _doc.Width, _doc.Height, rect, _marqueeEffectiveAdd)
                : ForgeMarquee.FillRect(_doc.Mask, _doc.Width, _doc.Height, rect, _marqueeEffectiveAdd);
            if (dirty.width > 0 && dirty.height > 0)
            {
                _undo.PushHinted(pre, _doc.Mask, _doc.Width, dirty);
                _doc.RebuildOverlayRect(dirty);
            }
            string shapeName = _marqueeShape == MarqueeShape.Ellipse ? "Ellipse" : "Rect";
            SetStatus($"Marquee {shapeName} {(_marqueeEffectiveAdd ? "Add" : "Subtract")} 완료");
        }

        void DrawMarqueePreviewClipped(Rect canvasRect)
        {
            bool square = Event.current != null && Event.current.shift;
            var rect = ForgeMarquee.RectFromPoints(_marqueeStartImg, _marqueeCurImg, square);
            if (rect.width <= 0 || rect.height <= 0) return;

            // 이미지(y-up) RectInt → 화면(y-down) Rect
            var blImg = new Vector2(rect.xMin, rect.yMin);
            var trImg = new Vector2(rect.xMax, rect.yMax);
            var bl = ImgToLocal(canvasRect, blImg);
            var tr = ImgToLocal(canvasRect, trImg);
            float xMin = Mathf.Min(bl.x, tr.x);
            float xMax = Mathf.Max(bl.x, tr.x);
            float yMin = Mathf.Min(bl.y, tr.y);
            float yMax = Mathf.Max(bl.y, tr.y);
            var screenRect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

            Color edge = _marqueeEffectiveAdd
                ? new Color(0.25f, 1f, 0.4f, 0.95f)
                : new Color(1f, 0.3f, 0.3f, 0.95f);

            if (_marqueeShape == MarqueeShape.Ellipse)
            {
                // 타원은 외곽선만. bbox 가이드는 옅은 점선 대신 옅은 사각 외곽선으로 표시
                Color guide = new Color(edge.r, edge.g, edge.b, 0.25f);
                ForgeGfx.DrawRectOutline(screenRect, guide, 1f);
                ForgeGfx.DrawEllipseOutline(screenRect, edge, 1.5f);
            }
            else
            {
                Color fill = new Color(edge.r, edge.g, edge.b, 0.15f);
                ForgeGfx.FilledRect(screenRect, fill);
                ForgeGfx.DrawRectOutline(screenRect, edge, 1.5f);
            }
        }

        // -------- Outline (외곽선 검출) --------
        void HandleOutlineEvents(Rect canvasRect, Event e, bool over)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && over)
            {
                var imgPt = _vp.ScreenToImage(canvasRect, e.mousePosition, _doc.Height);
                int sx = Mathf.FloorToInt(imgPt.x);
                int sy = Mathf.FloorToInt(imgPt.y);
                if (sx < 0 || sx >= _doc.Width || sy < 0 || sy >= _doc.Height) return;

                EnsureOutlineCacheReady();
                if (_outlineEdgeAware) EnsureOutlineEdgeReady();

                bool addSeed = e.shift && _outlineActive && _outlineSeeds.Count > 0;
                if (!addSeed)
                {
                    _outlineSeeds.Clear();
                    _outlineEffectiveAdd = e.alt ? false : _outlineAdd;
                }
                _outlineSeeds.Add(new Vector2Int(sx, sy));
                _outlineActive = true;
                RecomputeOutlinePreview();
                e.Use(); Repaint();
                return;
            }
        }

        void EnsureOutlineCacheReady()
        {
            if (!_doc.HasImage) return;
            if (_outlineCache != null && _outlineCacheSourcePath == _doc.SourceAssetPath
                && _outlineCache.W == _doc.Width && _outlineCache.H == _doc.Height)
                return;
            _outlineCache = ForgeOutlineDetect.BuildCache(_doc.Source, false, _outlineCannySigma, _outlineCannyLow, _outlineCannyHigh);
            _outlineCacheSourcePath = _doc.SourceAssetPath;
            _outlineCachedSigma = -1f; _outlineCachedLow = -1f; _outlineCachedHigh = -1f;
        }

        void EnsureOutlineEdgeReady()
        {
            EnsureOutlineCacheReady();
            if (_outlineCache == null) return;
            if (_outlineCache.HasEdge
                && Mathf.Approximately(_outlineCachedSigma, _outlineCannySigma)
                && Mathf.Approximately(_outlineCachedLow, _outlineCannyLow)
                && Mathf.Approximately(_outlineCachedHigh, _outlineCannyHigh))
                return;
            ForgeOutlineDetect.RecomputeEdge(_outlineCache, _outlineCannySigma, _outlineCannyLow, _outlineCannyHigh);
            _outlineCachedSigma = _outlineCannySigma;
            _outlineCachedLow = _outlineCannyLow;
            _outlineCachedHigh = _outlineCannyHigh;
        }

        void RecomputeOutlinePreview()
        {
            if (_outlineCache == null || _outlineSeeds.Count == 0)
            {
                _outlinePreviewMask = null;
                ReleaseOutlinePreviewTex();
                _outlineActive = false;
                return;
            }
            var mask = ForgeOutlineDetect.RegionGrow(_outlineCache, _outlineSeeds, _outlineColorTol, _outlineEdgeAware, out _);
            ForgeOutlineDetect.FillHoles(mask, _outlineCache.W, _outlineCache.H);
            _outlinePreviewMask = mask;
            EnsureOutlinePreviewTex(_outlineCache.W, _outlineCache.H);
            RebuildOutlinePreviewTex();
        }

        void EnsureOutlinePreviewTex(int W, int H)
        {
            if (_outlinePreviewTex != null && _outlinePreviewTex.width == W && _outlinePreviewTex.height == H) return;
            ReleaseOutlinePreviewTex();
            _outlinePreviewTex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        void ReleaseOutlinePreviewTex()
        {
            if (_outlinePreviewTex != null) { Object.DestroyImmediate(_outlinePreviewTex); _outlinePreviewTex = null; }
        }

        void RebuildOutlinePreviewTex()
        {
            if (_outlinePreviewMask == null || _outlinePreviewTex == null) return;
            int n = _outlinePreviewMask.Length;
            Color32 on = _outlineEffectiveAdd
                ? new Color32(60, 220, 90, 130)
                : new Color32(255, 60, 60, 130);
            Color32 off = new Color32(0, 0, 0, 0);
            var cols = new Color32[n];
            for (int i = 0; i < n; i++) cols[i] = _outlinePreviewMask[i] != 0 ? on : off;
            _outlinePreviewTex.SetPixels32(cols);
            _outlinePreviewTex.Apply(false);
        }

        void RebuildCannyEdgeTex()
        {
            if (_outlineCache == null || !_outlineCache.HasEdge || _outlineCache.EdgeMag == null) return;
            int W = _outlineCache.W, H = _outlineCache.H;
            if (_cannyEdgeTex == null || _cannyEdgeTex.width != W || _cannyEdgeTex.height != H)
            {
                ReleaseCannyEdgeTex();
                _cannyEdgeTex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            int n = W * H;
            Color32 on = new Color32(255, 240, 0, 220);   // 옅은 노란색
            Color32 off = new Color32(0, 0, 0, 0);
            var cols = new Color32[n];
            for (int i = 0; i < n; i++) cols[i] = _outlineCache.EdgeMag[i] != 0 ? on : off;
            _cannyEdgeTex.SetPixels32(cols);
            _cannyEdgeTex.Apply(false);
        }

        void ReleaseCannyEdgeTex()
        {
            if (_cannyEdgeTex != null) { Object.DestroyImmediate(_cannyEdgeTex); _cannyEdgeTex = null; }
        }

        void CommitOutline()
        {
            if (!_outlineActive || _outlinePreviewMask == null) { ClearOutline(); return; }
            var pre = (byte[])_doc.Mask.Clone();
            var dirty = ForgeOutlineDetect.ApplyToMask(_doc.Mask, _outlinePreviewMask, _doc.Width, _doc.Height, _outlineEffectiveAdd);
            if (dirty.width > 0 && dirty.height > 0)
            {
                _undo.PushHinted(pre, _doc.Mask, _doc.Width, dirty);
                _doc.RebuildOverlayRect(dirty);
                SetStatus($"Outline {(_outlineEffectiveAdd ? "Add" : "Subtract")} 완료 (시드 {_outlineSeeds.Count}개)");
            }
            else
            {
                SetStatus("Outline: 변경 없음");
            }
            ClearOutline();
            Repaint();
        }

        void ClearOutline()
        {
            _outlineActive = false;
            _outlineSeeds.Clear();
            _outlinePreviewMask = null;
            ReleaseOutlinePreviewTex();
        }

        // -------- Tool 전환 --------
        void SwitchTool(ToolMode next)
        {
            if (_tool == next) return;
            // 진행 중이던 도구별 임시 상태 정리
            if (_marqueeActive) _marqueeActive = false;
            if (_penVerts.Count > 0) ClearPen();
            if (_outlineActive || _outlineSeeds.Count > 0) ClearOutline();
            _tool = next;
            Repaint();
        }

        // -------- StatusBar --------
        void DrawStatusBar(Rect r)
        {
            GUI.Box(r, GUIContent.none, EditorStyles.toolbar);
            GUILayout.BeginArea(r);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_doc.HasImage)
                {
                    string cur = _cursorInside
                        ? $"({Mathf.FloorToInt(_cursorImg.x)}, {Mathf.FloorToInt(_cursorImg.y)})"
                        : "-";
                    GUILayout.Label($"cursor: {cur}   zoom: {_vp.Zoom * 100f:0}%   tool: {_tool}"
                                    + (_tool == ToolMode.Brush ? $"/{_brushMode}" : "")
                                    + $"   undo: {_undo.UndoCount}/{ForgeUndoStack.MaxEntries}", EditorStyles.miniLabel);
                }
                GUILayout.FlexibleSpace();
                if (EditorApplication.timeSinceStartup < _statusExpireAt && !string.IsNullOrEmpty(_statusMsg))
                    GUILayout.Label(_statusMsg, EditorStyles.miniLabel);
            }
            GUILayout.EndArea();
        }

        // -------- Shortcuts --------
        void HandleGlobalShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            // Ctrl 조합
            if (e.control || e.command)
            {
                if (e.keyCode == KeyCode.Z) { DoUndo(); e.Use(); Repaint(); return; }
                if (e.keyCode == KeyCode.Y) { DoRedo(); e.Use(); Repaint(); return; }
                if (e.keyCode == KeyCode.O) { EditorApplication.delayCall += DoLoad; e.Use(); return; }
                if (e.keyCode == KeyCode.S) { EditorApplication.delayCall += DoSave; e.Use(); return; }
            }

            if (e.keyCode == KeyCode.B) { SwitchTool(ToolMode.Brush); e.Use(); return; }
            if (e.keyCode == KeyCode.P) { SwitchTool(ToolMode.Pen); e.Use(); return; }
            if (e.keyCode == KeyCode.M) { SwitchTool(ToolMode.Marquee); e.Use(); return; }
            if (e.keyCode == KeyCode.E) { SwitchTool(ToolMode.Outline); e.Use(); return; }
            if (e.keyCode == KeyCode.F && _doc.HasImage)
            {
                _vp.Fit(_cachedCanvasRect, _doc.Width, _doc.Height); e.Use(); Repaint(); return;
            }
            if (e.keyCode == KeyCode.LeftBracket) { _brushRadius = Mathf.Max(1f, _brushRadius - 2f); e.Use(); Repaint(); return; }
            if (e.keyCode == KeyCode.RightBracket) { _brushRadius = Mathf.Min(256f, _brushRadius + 2f); e.Use(); Repaint(); return; }

            if (_tool == ToolMode.Pen)
            {
                if (e.keyCode == KeyCode.Escape) { ClearPen(); e.Use(); Repaint(); return; }
                if (e.keyCode == KeyCode.Backspace && _penVerts.Count > 0)
                { _penVerts.RemoveAt(_penVerts.Count - 1); e.Use(); Repaint(); return; }
                if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && _penVerts.Count >= 3)
                {
                    CommitPen(!e.shift);
                    e.Use(); Repaint(); return;
                }
            }

            if (_tool == ToolMode.Outline && _outlineActive)
            {
                if (e.keyCode == KeyCode.Escape) { ClearOutline(); e.Use(); Repaint(); return; }
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                { CommitOutline(); e.Use(); Repaint(); return; }
            }
        }

        // -------- Actions --------
        void DoLoad()
        {
            if (ForgeIO.LoadImageFromDialog(out var tex, out var assetPath, out var err))
            {
                _doc.SetSource(tex, assetPath);
                _undo.Clear();
                ClearPen();
                ClearOutline();
                ReleaseCannyEdgeTex();
                _outlineCache = null;
                _outlineCacheSourcePath = null;
                _vp.Fit(_cachedCanvasRect.width > 0 ? _cachedCanvasRect : new Rect(0, 0, position.width - SidebarWidth, position.height - ToolbarHeight - StatusHeight),
                        _doc.Width, _doc.Height);
                if (_outlineShowCanny)
                {
                    EnsureOutlineCacheReady();
                    EnsureOutlineEdgeReady();
                    RebuildCannyEdgeTex();
                }
                SetStatus($"로드 완료: {assetPath}");
                Repaint();
            }
            else if (!string.IsNullOrEmpty(err) && err != "취소됨")
            {
                EditorUtility.DisplayDialog("Load 실패", err, "OK");
            }
        }

        void DoSave()
        {
            if (!_doc.HasImage) return;
            var bounds = ForgeIO.ComputeMaskBounds(_doc.Mask, _doc.Width, _doc.Height);
            if (bounds.width <= 0 || bounds.height <= 0)
            {
                EditorUtility.DisplayDialog("Save 불가", "선택된 영역이 없습니다.", "OK");
                return;
            }
            var preview = ForgeIO.BuildCropped(_doc.Source, _doc.Mask, bounds);
            string defaultName = ForgeIO.NextAvailableOutputName(_doc.SourceStem);
            ForgeSavePreviewWindow.Show(preview, bounds, defaultName, filename =>
            {
                // preview 텍스처 재생성 후 저장(preview는 SavePreviewWindow가 Dispose할 것이므로 별도 텍스처 생성)
                var outTex = ForgeIO.BuildCropped(_doc.Source, _doc.Mask, bounds);
                string path = ForgeIO.SavePng(outTex, filename, out var err);
                Object.DestroyImmediate(outTex);
                if (path != null)
                {
                    var meta = new ForgeCropMeta
                    {
                        canvasSize = new ForgeCropMeta.CanvasSize { width = _doc.Width, height = _doc.Height },
                        cropBounds = new ForgeCropMeta.CropBounds { x = bounds.x, y = bounds.y, width = bounds.width, height = bounds.height },
                        createdAt = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };
                    if (!ForgeIO.WriteCropSidecar(path, meta, out var sidecarErr))
                    {
                        Debug.LogWarning($"[ImageObjectForge] Sidecar 저장 실패: {sidecarErr}");
                    }
                    SetStatus($"저장: {path}");
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (obj != null) EditorGUIUtility.PingObject(obj);
                }
                else
                {
                    EditorUtility.DisplayDialog("Save 실패", err ?? "unknown", "OK");
                }
                Repaint();
            });
        }

        void DoUndo()
        {
            var r = _undo.Undo(_doc.Mask, _doc.Width);
            if (r.width > 0) _doc.RebuildOverlayRect(r);
        }

        void DoRedo()
        {
            var r = _undo.Redo(_doc.Mask, _doc.Width);
            if (r.width > 0) _doc.RebuildOverlayRect(r);
        }

        void DoClearMask()
        {
            if (!_doc.HasImage) return;
            var pre = (byte[])_doc.Mask.Clone();
            System.Array.Clear(_doc.Mask, 0, _doc.Mask.Length);
            _undo.Push(pre, _doc.Mask, _doc.Width, _doc.Height);
            _doc.RebuildOverlayAll();
            Repaint();
        }

        void SetStatus(string msg)
        {
            _statusMsg = msg;
            _statusExpireAt = EditorApplication.timeSinceStartup + 3.0;
        }

        // Checkerboard — ForgeGfx.DrawCheckerboard로 이전됨 (타일 텍스처 1회 draw)
    }
}
