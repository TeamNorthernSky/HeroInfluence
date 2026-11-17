// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace JC.BackgroundEditing
{
    public sealed partial class BackgroundWindow : EditorWindow
    {
        [SerializeField] BackgroundDocument document;
        // JC | 2026-11-16 | 사용자는 단일 원본만 편집합니다. 경로·작업본·적용 대상 선택 UI를 제거합니다.
        [SerializeField] bool showGuide;
        // JC | 2026-11-16 | 큰 부동 편집창에 가려지지 않도록 같은 창의 전용 Scene 탭을 재사용합니다.
        [SerializeField] SceneView editView;
        // JC | 2026-11-17 | 도시 Scene 탭 안에서도 도구를 바로 사용할 수 있게 접이식 도구모음 상태를 보관합니다.
        [SerializeField] bool showSceneTools=true;
        Vector2 sceneToolsScroll;
        static readonly string[] EditModes={"선택","선","사각형","배치물","스포이트","구획"};
        [SerializeField] int mode,kind,width=2,paletteIndex,viewMode=0;
        // JC | 2026-11-17 | 선/사각형 지우기를 종료하면 직전에 칠하던 지형으로 돌아갑니다.
        [SerializeField] int lastPaintKind;
        [SerializeField] bool snap=true,showHUD=true,autoPreview=true;
        [SerializeField] float angle;
        Vector2 scroll;Vector2Int start,hover;bool dragging;MeshFilter[] palette=Array.Empty<MeshFilter>();
        Vector2Int copyMin=new Vector2Int(18,32),copyMax=new Vector2Int(30,40),copyOffset=new Vector2Int(12,0);
        JC.BuildingColors.BgColorSetProfile colorSet;bool applyPlacementColors=true;string paletteSearch="";bool paletteExpanded;Color tint=Color.white;string status="고정 작업본을 열어 계속 편집하세요.";double nextPreview;int previewHash;
        [MenuItem("JC/보스 원경/필드 편집기")]
        public static void Open(){BackgroundOps.Guard();var w=GetWindow<BackgroundWindow>();w.titleContent=new GUIContent("보스 원경 필드");w.minSize=new Vector2(1000,650);w.document=FindDocument();w.TrackCloseState();w.Show();}
        // JC | 2026-11-05 | 편집기 수명에 맞춰 다른 씬의 표시를 제외하고, 종료/리로드/Play 전환에는 원래 상태로 돌립니다.
        void OnEnable(){SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=UndoChanged;EditorApplication.hierarchyChanged+=HierarchyChanged;EditorApplication.playModeStateChanged+=PlayModeChanged;EditorApplication.delayCall+=RefreshIsolation;EnableCloseTracking();}
        void OnDisable(){SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=UndoChanged;EditorApplication.hierarchyChanged-=HierarchyChanged;EditorApplication.playModeStateChanged-=PlayModeChanged;EditorApplication.delayCall-=RefreshIsolation;DisableCloseTracking();RestoreIsolation();DisposePreview();}
        // JC | 2026-11-16 | 닫기 확인에 필요한 작업 씬 목록은 계층 변경 때만 갱신합니다.
        void HierarchyChanged(){if(!document)document=FindDocument();RefreshIsolation();if(ownsWorkspaceSession)TrackCloseState();}
        // JC | 2026-11-16 | 예전 전체 Workspace 대신 단일 원본에서 만든 도시 작업본만 연결합니다.
        static BackgroundDocument FindDocument()=>BackgroundOps.FindSingleDocument();
        void UndoChanged(){var target=document;if(target)EditorApplication.delayCall+=()=>{if(target)Run(()=>BackgroundOps.Rebuild(target));};}
        void Run(Action action){try{BackgroundOps.Guard();action();previewHash=0;}catch(Exception e){status=e.Message;Debug.LogException(e);}Repaint();}
        // JC | 2026-11-17 | 이미 열린 작업본에 다시 연결하는 경우에도 기존 가로등을 선택 가능한 배치 그룹으로 준비합니다.
        void Bind(BackgroundDocument d){DisposePreview();dragging=false;document=d;BackgroundOps.ApplyLocks(d);TrackCloseState();RefreshIsolation();BuildPalette();Frame();status="도시 원경 원본 편집 중입니다. '원본에 저장'을 누르면 현재 씬 원본에 반영·저장됩니다.";previewHash=0;}
        void BuildPalette(){palette=document?document.baseline.GetComponentsInChildren<MeshFilter>().Where(BackgroundOps.IsProp).GroupBy(f=>f.sharedMesh.GetInstanceID()+":"+string.Join(",",f.GetComponent<Renderer>().sharedMaterials.Select(m=>m.GetInstanceID()))).Select(g=>g.First()).ToArray():Array.Empty<MeshFilter>();paletteIndex=Mathf.Clamp(paletteIndex,0,Math.Max(0,palette.Length-1));}
        // JC | 2026-11-16 | 이전 작업본의 숨김/Scene 뷰 상태를 풀고 실제 편집 중인 도시로 즉시 이동합니다. 버튼 클릭 때만 창을 전환합니다.
        void Frame()
        {
            if(!document)return;
            SceneManager.SetActiveScene(document.gameObject.scene);RefreshIsolation();
            SceneVisibilityManager.instance.Show(document.gameObject,true);
            var bounds=BackgroundOps.EditBounds(document);
            if(!editView)editView=CreateWindow<SceneView>("도시 원경 편집",typeof(BackgroundWindow));
            var v=editView;
            v.titleContent=new GUIContent("도시 원경 편집");
            v.Show();v.Focus();v.in2DMode=false;v.isRotationLocked=false;
            float aspect=Mathf.Max(.1f,v.position.width/Mathf.Max(1,v.position.height-40));
            v.LookAt(bounds.center,Quaternion.Euler(90,0,0),Mathf.Max(10,Mathf.Max(bounds.extents.z,bounds.extents.x/aspect)*1.2f),true,true);
            // JC | 2026-11-17 | 도구모음은 편집 탭 안에 표시하고 상세 색상·미리보기만 기존 필드 탭에 남깁니다.
            v.Repaint();status="'도시 원경 편집' 탭의 도구모음에서 지형·배치물 편집과 저장을 할 수 있습니다.";
        }
        void OnGUI()
        {
            // JC | 2026-11-16 | 브랜치 제한 없이 사용하되 Play·컴파일 중에는 편집 UI를 실행하지 않습니다.
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling){EditorGUILayout.HelpBox("플레이 종료 및 컴파일 완료 후 사용할 수 있습니다.",MessageType.Info);return;}
            // JC | 2026-11-16 | 실제 편집 화면만 종료 책임을 가지며, 재컴파일 후에는 문서 캐시를 다시 연결합니다.
            if(!document)document=FindDocument();
            if(document&&(!ownsWorkspaceSession||closingDocuments.Length==0))TrackCloseState();
            UpdateCloseState();
            float panelWidth=Mathf.Clamp(position.width*.28f,340f,440f);
            float contentWidth=panelWidth-36f;
            float savedLabelWidth=EditorGUIUtility.labelWidth,savedFieldWidth=EditorGUIUtility.fieldWidth;
            bool savedWideMode=EditorGUIUtility.wideMode;
            // 독립 Area와 내용 폭으로 자식 컨트롤의 최소 폭이 옆 패널까지 밀려나는 것을 막습니다.
            try
            {
                EditorGUIUtility.labelWidth=126f;EditorGUIUtility.fieldWidth=42f;EditorGUIUtility.wideMode=false;
                using(new GUILayout.AreaScope(new Rect(8,4,panelWidth-16,position.height-8)))
                {
                    scroll.x=0;
                    scroll=GUILayout.BeginScrollView(scroll,false,true,GUIStyle.none,GUI.skin.verticalScrollbar);
                    using(new EditorGUILayout.VerticalScope(GUILayout.Width(contentWidth)))
                    {
                    // JC | 2026-11-16 | 단일 원본 흐름으로 통일하고 예전 전체 Workspace는 표시·저장 대상으로 채택하지 않습니다.
                    EditorGUILayout.LabelField("도시 원경 (베이크) 원본 편집",EditorStyles.boldLabel);
                    if(!document||!document.IsCityTile)document=FindDocument();
                    EditorGUILayout.HelpBox("현재 씬의 '도시 원경 (베이크)' 하나를 수정합니다. 저장하면 이 원본에 바로 반영되며 씬도 저장됩니다.",MessageType.Info);
                    if(GUILayout.Button(document?"원본 편집 이어서 / 편집 화면 열기":"도시 원경 원본 편집 시작",GUILayout.Height(30)))Run(()=>Bind(BackgroundOps.OpenSingleOriginal()));
                    showGuide=EditorGUILayout.Foldout(showGuide,"저장 위치 / 사용 안내",true);
                    if(showGuide)
                    {
                        EditorGUILayout.HelpBox("원본 편집 시작 → 지형·건물·구획 편집 → 원본에 저장. 작업 보관 경로는 자동 관리됩니다. 원본을 복사하는 작업은 씬에서 직접 수행합니다.",MessageType.None);
                        // 한 줄 LabelField 대신 줄바꿈 표시와 전체 경로 툴팁으로 좁은 패널에서도 경로가 잘리지 않게 합니다.
                        EditorGUILayout.LabelField(new GUIContent(BackgroundOps.SingleWorkspacePath,BackgroundOps.SingleWorkspacePath),EditorStyles.wordWrappedLabel,GUILayout.Width(contentWidth));
                    }
                    if(document)
                    {
                        EditorGUILayout.Space();
                        if(GUILayout.Button("원본에 저장",GUILayout.Height(32)))Run(()=>{BackgroundOps.SaveOriginalCity(document);status="도시 원경 원본 반영 및 씬 저장 완료";});
                        // JC | 2026-11-16 | 저장 여부를 확인한 뒤 편집 씬을 닫고 원래 보스 씬으로 돌아갑니다.
                        if(GUILayout.Button("편집 종료 / 보스 씬으로 돌아가기")) { ExitEditing(); GUIUtility.ExitGUI(); }
                        if(GUILayout.Button("위에서 보기 / 편집 영역으로 이동"))Run(Frame);
                        // JC | 2026-11-17 | 선택 잠금과 배치 목록에 가로등이 포함됨을 기존 필드 탭에도 명시합니다.
                        EditorGUI.BeginChangeCheck();bool limit=EditorGUILayout.Toggle("편집 영역 제한",document.limitArea);var min=EditorGUILayout.Vector2IntField("영역 최소",document.editMin);var max=EditorGUILayout.Vector2IntField("영역 최대",document.editMax);bool locked=EditorGUILayout.Toggle("건물·가로등 잠금",document.lockBuildings);
                        if(EditorGUI.EndChangeCheck()){Undo.RecordObject(document,"편집 제한 변경");document.limitArea=limit;document.editMin=Vector2Int.Min(min,max);document.editMax=Vector2Int.Max(min,max);document.lockBuildings=locked;BackgroundOps.ApplyLocks(document);EditorUtility.SetDirty(document);}
                        int nextMode=GUILayout.SelectionGrid(mode,EditModes,3,GUILayout.Width(contentWidth),GUILayout.Height(44));if(nextMode!=mode){dragging=false;mode=nextMode;SceneView.RepaintAll();}
                        if(mode==1||mode==2||mode==4){DrawTerrainBrushControls();if(kind>=5){var tile=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_ProtoType_Merge/DH/AlphaAsset/Tile/"+BackgroundOps.TileTextures[kind]+".png");if(tile)GUILayout.Label(tile,GUILayout.Width(80),GUILayout.Height(80));}EditorGUILayout.HelpBox("Scene 뷰에서 드래그합니다. 선은 가로/세로로 맞춰집니다. 지형 지우기는 지면·수로·마감만 제거하며 건물·가로등은 유지합니다.",MessageType.Info);}
                        if(mode==3)
                        {
                            if(palette.Length==0)BuildPalette();snap=EditorGUILayout.Toggle("그리드 맞춤",snap);angle=EditorGUILayout.FloatField("배치 회전",angle);
                            string[] names=palette.Select(f=>f.name).ToArray();if(names.Length>0){paletteIndex=EditorGUILayout.Popup("건물·가로등",paletteIndex,names);var tex=AssetPreview.GetAssetPreview(palette[paletteIndex].sharedMesh);if(tex)GUILayout.Label(tex,GUILayout.Width(100),GUILayout.Height(80));}
                            paletteExpanded=EditorGUILayout.Foldout(paletteExpanded,"배치물 썸네일 목록",true);
                            if(paletteExpanded){paletteSearch=EditorGUILayout.TextField("검색",paletteSearch);var candidates=palette.Select((f,i)=>new{f,i}).Where(x=>string.IsNullOrEmpty(paletteSearch)||x.f.name.IndexOf(paletteSearch,StringComparison.OrdinalIgnoreCase)>=0).ToArray();var style=new GUIStyle(GUI.skin.button){imagePosition=ImagePosition.ImageAbove,wordWrap=true,fontSize=9};for(int row=0;row<candidates.Length;row+=3){using(new EditorGUILayout.HorizontalScope()){for(int j=row;j<Math.Min(row+3,candidates.Length);j++){var item=candidates[j];var tex=AssetPreview.GetAssetPreview(item.f.sharedMesh);if(GUILayout.Button(new GUIContent(item.f.name,tex),style,GUILayout.Width((contentWidth-12f)/3f),GUILayout.Height(85)))paletteIndex=item.i;}}}}
                            EditorGUILayout.HelpBox("Scene 뷰에서 클릭해 배치합니다. 선택 모드에서 Unity 이동·회전·크기 도구와 Ctrl+D / Delete를 사용합니다.",MessageType.None);
                        }
                        EditorGUI.BeginChangeCheck();bool autoDress=EditorGUILayout.Toggle(new GUIContent("칠할 때 전체 드레싱","끄면 지형을 빠르게 편집합니다. 마무리할 때 드레싱 다시 생성을 누르세요."),document.dressWhilePainting);if(EditorGUI.EndChangeCheck()){Undo.RecordObject(document,"자동 드레싱 변경");document.dressWhilePainting=autoDress;EditorUtility.SetDirty(document);}
                        if(GUILayout.Button("드레싱 다시 생성"))Run(()=>{BackgroundOps.RegenerateDressing(document);status="배경 전체의 호안·연석·차선 생성 완료";});
                        EditorGUILayout.Space();EditorGUILayout.LabelField("구획 복제",EditorStyles.boldLabel);copyMin=EditorGUILayout.Vector2IntField("최소",copyMin);copyMax=EditorGUILayout.Vector2IntField("최대",copyMax);copyOffset=EditorGUILayout.Vector2IntField("이동량",copyOffset);
                        // JC | 2026-11-17 | 두 편집 화면에 셀 수와 실제 복제 도착 범위를 표시해 전체 도시 복제 값을 확인할 수 있게 합니다.
                        EditorGUILayout.HelpBox(BlockCopySummary(),MessageType.None);
                        if(GUILayout.Button("구획의 지형 + 배치물 복제"))Run(()=>{BackgroundOps.CopyBlock(document,Vector2Int.Min(copyMin,copyMax),Vector2Int.Max(copyMin,copyMax),copyOffset);status="구획의 지형·마감·건물·가로등을 복제했습니다.";});
                        EditorGUILayout.Space();EditorGUILayout.LabelField("건물 색상",EditorStyles.boldLabel);
                        // JC | 2026-11-17 | 예전 기본 프로필 대신 탐사씬과 동일한 팔레트를 표시하고 배치·기본색에도 공유합니다.
                        colorSet=BackgroundOps.ExploreColorPalette();
                        using(new EditorGUI.DisabledScope(true))EditorGUILayout.ObjectField("탐사씬 색상 팔레트",colorSet,typeof(JC.BuildingColors.BgColorSetProfile),false,GUILayout.Width(contentWidth));
                        applyPlacementColors=EditorGUILayout.Toggle("배치 시 바리에이션",applyPlacementColors);
                        using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button("선택 건물 기본색"))Run(()=>ProfileSelected(false));if(GUILayout.Button("전체 건물 바리에이션 섞기"))Run(()=>ProfileSelected(true));}
                        EditorGUILayout.LabelField("전체 바리에이션: 가로등·잠긴 건물 제외",EditorStyles.miniLabel);tint=EditorGUILayout.ColorField("재질 색상 곱",tint);
                        if(GUILayout.Button("선택 건물에 적용"))Run(ApplyTint);
                    }
                    EditorGUILayout.HelpBox(status,MessageType.Info);
                    }
                    GUILayout.EndScrollView();
                }
                EditorGUIUtility.labelWidth=savedLabelWidth;EditorGUIUtility.fieldWidth=savedFieldWidth;EditorGUIUtility.wideMode=savedWideMode;
                using(new GUILayout.AreaScope(new Rect(panelWidth+8,4,Mathf.Max(1,position.width-panelWidth-16),position.height-8)))
                {
                    EditorGUILayout.LabelField("전투 구도 미리보기",EditorStyles.boldLabel);viewMode=GUILayout.Toolbar(viewMode,new[]{"실제 카메라 A","실제 카메라 B","넓은 검토 구도"});
                    using(new EditorGUILayout.HorizontalScope()){showHUD=GUILayout.Toggle(showHUD,"HUD",GUILayout.Width(70));autoPreview=GUILayout.Toggle(autoPreview,"변경 자동 반영",GUILayout.Width(130));if(GUILayout.Button("미리보기 갱신"))Run(()=>RefreshPreview(true));}
                    if(document){EditorGUI.BeginChangeCheck();float h=EditorGUILayout.Slider("시간",document.hour,0,24);if(EditorGUI.EndChangeCheck()){Undo.RecordObject(document,"미리보기 시간");document.hour=h;EditorUtility.SetDirty(document);}}
                    var rect=GUILayoutUtility.GetAspectRect(16f/9);if(previewTexture)GUI.DrawTexture(rect,previewTexture,ScaleMode.ScaleToFit,false);else EditorGUI.HelpBox(rect,"작업본을 열면 전투 구도를 표시합니다.",MessageType.None);
                }
            }
            finally{EditorGUIUtility.labelWidth=savedLabelWidth;EditorGUIUtility.fieldWidth=savedFieldWidth;EditorGUIUtility.wideMode=savedWideMode;}
        }
        void ProfileSelected(bool random)
        {
            // JC | 2026-11-17 | 바리에이션은 전체 건물, 기본색은 선택 건물에 탐사씬의 최신 팔레트를 적용합니다.
            int count=random?BackgroundOps.ShuffleBuildingVariants(document):BackgroundOps.ApplyBuildingProfiles(document,Selection.gameObjects,BackgroundOps.ExploreColorPalette(),false);
            status=count+"개 건물에 설정 색상을 적용했습니다. 가로등·잠긴 건물은 제외했습니다.";
        }
        void ApplyTint()
        {
            var r=Selection.activeGameObject?Selection.activeGameObject.GetComponent<Renderer>():null;if(!r||!r.transform.IsChildOf(document.Buildings))throw new Exception("작업본의 건물을 선택해 주세요.");
            if((r.gameObject.hideFlags&HideFlags.NotEditable)!=0)throw new Exception("잠긴 건물입니다.");Undo.RecordObject(r,"건물 색상");BackgroundOps.Folder(document.assetFolder+"/Colors");var materials=r.sharedMaterials;
            for(int i=0;i<materials.Length;i++){var m=new Material(materials[i]);if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",m.GetColor("_BaseColor")*tint);string p=AssetDatabase.GenerateUniqueAssetPath(document.assetFolder+"/Colors/Building.mat");AssetDatabase.CreateAsset(m,p);materials[i]=m;}r.sharedMaterials=materials;EditorSceneManager.MarkSceneDirty(document.gameObject.scene);
        }
        // JC | 2026-11-17 | 선택 잠금된 지형도 선 폭만큼 지울 수 있도록 기존 비우기 브러시를 명시적인 작업 버튼으로 노출합니다.
        void DrawTerrainBrushControls()
        {
            if(mode==1||mode==2)
            {
                bool erase=kind==4;
                int next=GUILayout.Toolbar(erase?1:0,new[]{"칠하기","지형 지우기"});
                if(next==1&&!erase){lastPaintKind=kind;kind=4;}
                else if(next==0&&erase)kind=lastPaintKind==4?0:Mathf.Clamp(lastPaintKind,0,BackgroundOps.TileLabels.Length-1);
                if(kind==4)EditorGUILayout.HelpBox("지면·수로·경계 마감만 지웁니다. 건물·가로등은 유지됩니다.",MessageType.None);
            }
            if(kind!=4||mode==4)kind=EditorGUILayout.Popup("지형",kind,BackgroundOps.TileLabels);
            width=EditorGUILayout.IntSlider("선 폭 (셀)",width,1,12);
        }
        // JC | 2026-11-17 | 영역 끝을 포함하는 셀 수와 이동 후 좌표를 같은 규칙으로 안내합니다.
        string BlockCopySummary()
        {
            var min=Vector2Int.Min(copyMin,copyMax);var max=Vector2Int.Max(copyMin,copyMax);
            long x=(long)max.x-min.x+1,z=(long)max.y-min.y+1;
            return $"{x} × {z}셀 (한 번에 최대 10,000셀)\n복제 위치: ({(long)min.x+copyOffset.x}, {(long)min.y+copyOffset.y}) ~ ({(long)max.x+copyOffset.x}, {(long)max.y+copyOffset.y})";
        }
        // JC | 2026-11-17 | Scene 탭과 필드 탭이 같은 편집 상태/명령을 사용합니다. 패널 위 클릭은 지형 칠하기로 전달하지 않습니다.
        bool DrawSceneTools(SceneView view)
        {
            if(view!=editView)return false;
            var rect=new Rect(64,12,Mathf.Min(328,Mathf.Max(180,view.position.width-84)),showSceneTools?Mathf.Min(480,Mathf.Max(100,view.position.height-64)):28);
            bool over=rect.Contains(Event.current.mousePosition);
            float labelWidth=EditorGUIUtility.labelWidth;
            Handles.BeginGUI();
            try
            {
                GUI.Box(rect,GUIContent.none,EditorStyles.helpBox);
                using(new GUILayout.AreaScope(new Rect(rect.x+6,rect.y+4,rect.width-12,rect.height-8)))
                {
                    showSceneTools=EditorGUILayout.Foldout(showSceneTools,"도시 원경 도구모음",true);
                    if(showSceneTools)
                    {
                        EditorGUIUtility.labelWidth=96;
                        sceneToolsScroll=EditorGUILayout.BeginScrollView(sceneToolsScroll);
                        int next=GUILayout.SelectionGrid(mode,EditModes,3,GUILayout.Height(44));
                        if(next!=mode){mode=next;dragging=false;Repaint();view.Repaint();}
                        EditorGUILayout.HelpBox("선택: 건물·가로등 클릭 후 이동/회전. Ctrl+D 복제, Delete 삭제.",MessageType.None);
                        EditorGUI.BeginChangeCheck();bool locked=EditorGUILayout.Toggle("배치물 잠금",document.lockBuildings);
                        if(EditorGUI.EndChangeCheck()){Undo.RecordObject(document,"배치물 잠금");document.lockBuildings=locked;BackgroundOps.ApplyLocks(document);EditorUtility.SetDirty(document);}
                        if(mode==1||mode==2||mode==4)DrawTerrainBrushControls();
                        if(mode==3)
                        {
                            if(palette.Length==0)BuildPalette();
                            if(palette.Length>0)paletteIndex=EditorGUILayout.Popup("건물·가로등",paletteIndex,palette.Select(f=>f.name).ToArray());
                            snap=EditorGUILayout.Toggle("그리드 맞춤",snap);angle=EditorGUILayout.FloatField("배치 회전",angle);
                        }
                        if(mode==5)
                        {
                            copyMin=EditorGUILayout.Vector2IntField("구획 최소",copyMin);copyMax=EditorGUILayout.Vector2IntField("구획 최대",copyMax);copyOffset=EditorGUILayout.Vector2IntField("복제 이동량",copyOffset);
                            EditorGUILayout.HelpBox(BlockCopySummary(),MessageType.None);
                            if(GUILayout.Button("구획 지형 + 배치물 복제"))Run(()=>BackgroundOps.CopyBlock(document,Vector2Int.Min(copyMin,copyMax),Vector2Int.Max(copyMin,copyMax),copyOffset));
                        }
                        if(GUILayout.Button("드레싱 다시 생성"))Run(()=>{BackgroundOps.RegenerateDressing(document);status="호안·연석·차선 생성 완료";});
                        if(GUILayout.Button("원본에 저장"))Run(()=>{BackgroundOps.SaveOriginalCity(document);status="도시 원경 원본 반영 및 씬 저장 완료";});
                        if(GUILayout.Button("상세 설정 / 전투 미리보기")){Show();Focus();}
                        EditorGUILayout.HelpBox(status,MessageType.None);
                        EditorGUILayout.EndScrollView();
                    }
                }
            }
            finally{EditorGUIUtility.labelWidth=labelWidth;Handles.EndGUI();}
            return over;
        }
        void SceneGUI(SceneView view)
        {
            if(!document||EditorApplication.isPlayingOrWillChangePlaymode||!document.gameObject.scene.isLoaded)return;var e=Event.current;
            // JC | 2026-11-17 | 도구모음 안에서 드래그를 끝내면 브러시를 취소하고 마우스 점유를 해제합니다.
            bool overTools=DrawSceneTools(view);
            if(overTools||e.type==EventType.Used)
            {
                if(dragging&&e.rawType==EventType.MouseUp){dragging=false;GUIUtility.hotControl=0;}
                // JC | 2026-11-17 | 버튼 사이 빈 공간도 기본 Scene 선택/휠 이동에 전달하지 않습니다.
                if(overTools&&e.type==EventType.Layout)HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                if(overTools&&(e.isMouse||e.type==EventType.ScrollWheel))e.Use();
                return;
            }
            Handles.color=new Color(.2f,1,.6f,.8f);var min=document.editMin;var max=document.editMax;Handles.DrawWireCube(new Vector3((min.x+max.x)*.5f,.1f,(min.y+max.y)*.5f),new Vector3(max.x-min.x+1,.05f,max.y-min.y+1));
            if(mode==0||e.alt){dragging=false;return;}
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))return;var point=ray.GetPoint(distance);hover=new Vector2Int(Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.z));
            int control=GUIUtility.GetControlID("MiscBackgroundBrush".GetHashCode(),FocusType.Passive);if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(control);
            var previousZ=Handles.zTest;Handles.zTest=UnityEngine.Rendering.CompareFunction.Always;
            try{
                Handles.color=Color.cyan;
                if(mode==1||mode==2){try{foreach(var p in BackgroundOps.PaintCells(document,dragging?start:hover,hover,width,mode==2))Handles.DrawWireCube(new Vector3(p.x,.1f,p.y),new Vector3(1,.01f,1));}catch(InvalidOperationException){}}
                else Handles.DrawWireCube(new Vector3(hover.x,.1f,hover.y),new Vector3(1,.05f,1));
                Handles.Label(new Vector3(hover.x,.15f,hover.y),$"X {hover.x} / Z {hover.y}");
            }finally{Handles.zTest=previousZ;}
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){dragging=false;GUIUtility.hotControl=0;e.Use();return;}
            if(e.type==EventType.MouseDown&&e.button==0)
            {
                if(EditorWindow.mouseOverWindow!=view||HandleUtility.nearestControl!=control||GUIUtility.hotControl!=0)return;
                if(document.limitArea&&(hover.x<min.x||hover.x>max.x||hover.y<min.y||hover.y>max.y))return;
                if(mode==3){Run(()=>{if(palette.Length==0)BuildPalette();if(palette.Length==0)throw new Exception("건물 목록 없음");var placed=BackgroundOps.Place(document,palette[paletteIndex],snap?new Vector3(hover.x,.18f,hover.y):new Vector3(point.x,.18f,point.z),angle);if(applyPlacementColors&&BackgroundOps.Definition(placed.GetComponent<Renderer>()))BackgroundOps.ApplyBuildingProfile(document,placed.GetComponent<Renderer>(),BackgroundOps.ExploreColorPalette(),true);});}
                else if(mode==4){int sampled=BackgroundOps.Sample(document,hover);kind=sampled<0?4:sampled;status="해당 위치의 지형을 가져왔습니다.";Repaint();}
                else{start=hover;dragging=true;GUIUtility.hotControl=control;}e.Use();
            }
            if(e.type==EventType.MouseUp&&e.button==0&&dragging){dragging=false;GUIUtility.hotControl=0;if(mode==5){copyMin=Vector2Int.Min(start,hover);copyMax=Vector2Int.Max(start,hover);status="구획을 선택했습니다. 이동량을 지정해 복제하세요.";Repaint();}else Run(()=>{BackgroundOps.Paint(document,start,hover,kind==4?-1:kind,width,mode==2);status=$"{BackgroundOps.TileLabels[kind]} 적용: {BackgroundOps.PaintCells(document,start,hover,width,mode==2).Count}셀 (X/Z {start} → {hover})";});e.Use();}
            if(e.type==EventType.MouseDrag&&dragging)e.Use();view.Repaint();
        }
    }
}
