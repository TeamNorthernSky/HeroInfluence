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
        [SerializeField] int mode,kind,width=2,paletteIndex,viewMode=0;
        [SerializeField] bool snap=true,showHUD=true,autoPreview=true;
        [SerializeField] float angle;
        Vector2 scroll;Vector2Int start,hover;bool dragging;MeshFilter[] palette=Array.Empty<MeshFilter>();
        Vector2Int copyMin=new Vector2Int(18,32),copyMax=new Vector2Int(30,40),copyOffset=new Vector2Int(12,0);
        JC.BuildingColors.BgColorSetProfile colorSet;bool applyPlacementColors=true;string paletteSearch="";bool paletteExpanded;Color tint=Color.white;string status="고정 작업본을 열어 계속 편집하세요.";double nextPreview;int previewHash;
        [MenuItem("JC/보스 원경/필드 편집기")]
        public static void Open(){BackgroundOps.Guard();var w=GetWindow<BackgroundWindow>();w.titleContent=new GUIContent("보스 원경 필드");w.minSize=new Vector2(1000,650);w.Show();}
        // JC | 2026-11-05 | 편집기 수명에 맞춰 다른 씬의 표시를 제외하고, 종료/리로드/Play 전환에는 원래 상태로 돌립니다.
        void OnEnable(){SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=UndoChanged;EditorApplication.hierarchyChanged+=HierarchyChanged;EditorApplication.playModeStateChanged+=PlayModeChanged;EditorApplication.delayCall+=RefreshIsolation;}
        void OnDisable(){SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=UndoChanged;EditorApplication.hierarchyChanged-=HierarchyChanged;EditorApplication.playModeStateChanged-=PlayModeChanged;EditorApplication.delayCall-=RefreshIsolation;RestoreIsolation();DisposePreview();}
        void HierarchyChanged(){if(!document)document=FindDocument();RefreshIsolation();}
        static BackgroundDocument FindDocument()=>Resources.FindObjectsOfTypeAll<BackgroundDocument>().FirstOrDefault(d=>d.gameObject.scene.IsValid()&&d.gameObject.scene.isLoaded&&!EditorSceneManager.IsPreviewScene(d.gameObject.scene));
        void UndoChanged(){var target=document;if(target)EditorApplication.delayCall+=()=>{if(target)Run(()=>BackgroundOps.Rebuild(target));};}
        void Run(Action action){try{BackgroundOps.Guard();action();previewHash=0;}catch(Exception e){status=e.Message;Debug.LogException(e);}Repaint();}
        void Bind(BackgroundDocument d){dragging=false;document=d;RefreshIsolation();BuildPalette();Frame();status="작업본을 열었습니다. Scene 뷰에서 편집하고 오른쪽에서 결과를 확인하세요.";previewHash=0;}
        void BuildPalette(){palette=document?document.baseline.GetComponentsInChildren<MeshFilter>().Where(BackgroundOps.IsProp).GroupBy(f=>f.sharedMesh.GetInstanceID()+":"+string.Join(",",f.GetComponent<Renderer>().sharedMaterials.Select(m=>m.GetInstanceID()))).Select(g=>g.First()).ToArray():Array.Empty<MeshFilter>();paletteIndex=Mathf.Clamp(paletteIndex,0,Math.Max(0,palette.Length-1));}
        void Frame(){if(!document)return;var v=SceneView.lastActiveSceneView??GetWindow<SceneView>();// JC | 2026-11-05 | 새 월드 전체의 크기를 기준으로 편집 화면을 잡습니다.
            var all=document.GetComponentsInChildren<Renderer>();var bounds=all.Length>0?all[0].bounds:new Bounds(Vector3.zero,Vector3.one*20);foreach(var r in all)bounds.Encapsulate(r.bounds);v.LookAt(bounds.center,Quaternion.Euler(90,0,0),Mathf.Max(10,Mathf.Max(bounds.extents.x,bounds.extents.z)),true,true);v.Show();}
        void OnGUI()
        {
            if(Application.dataPath!="C:/Dev/HeroInfluence_Polish/Assets"){EditorGUILayout.HelpBox("폴리싱 프로젝트에서만 사용합니다.",MessageType.Error);return;}
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
                    // JC | 2026-11-05 | 이름/경로 선택과 새 작업본 분기를 없애고 단일 저장 파일을 표시합니다.
                    EditorGUILayout.LabelField("보스 원경 작업",EditorStyles.boldLabel);
                    if(!document)document=FindDocument();
                    if(GUILayout.Button("간단 사용 매뉴얼 열기"))AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_ProtoType_Merge/JC/Docs/BossEnvironment.md"));
                    EditorGUILayout.LabelField("고정 작업 파일", "World/Workspace.unity");
                    if(GUILayout.Button("작업본 열기 / 이어서 편집"))Run(()=>Bind(BackgroundOps.OpenLatest()));
                    if(document)
                    {
                        EditorGUILayout.Space();EditorGUILayout.LabelField(System.IO.Path.GetFileNameWithoutExtension(document.gameObject.scene.path),EditorStyles.boldLabel);
                        if(GUILayout.Button("저장"))Run(()=>{BackgroundOps.Save(document);status="고정 작업 파일에 저장했습니다.";});
                        if(GUILayout.Button("위에서 보기 / 편집 영역으로 이동"))Frame();
                        EditorGUI.BeginChangeCheck();bool limit=EditorGUILayout.Toggle("편집 영역 제한",document.limitArea);var min=EditorGUILayout.Vector2IntField("영역 최소",document.editMin);var max=EditorGUILayout.Vector2IntField("영역 최대",document.editMax);bool locked=EditorGUILayout.Toggle("건물 선택 잠금",document.lockBuildings);
                        if(EditorGUI.EndChangeCheck()){Undo.RecordObject(document,"편집 제한 변경");document.limitArea=limit;document.editMin=Vector2Int.Min(min,max);document.editMax=Vector2Int.Max(min,max);document.lockBuildings=locked;BackgroundOps.ApplyLocks(document);EditorUtility.SetDirty(document);}
                        int nextMode=GUILayout.SelectionGrid(mode,new[]{"선택","선","사각형","건물","스포이트","구획"},3,GUILayout.Width(contentWidth),GUILayout.Height(44));if(nextMode!=mode){dragging=false;mode=nextMode;SceneView.RepaintAll();}
                        if(mode==1||mode==2||mode==4){kind=EditorGUILayout.Popup("지형",kind,BackgroundOps.TileLabels);width=EditorGUILayout.IntSlider("선 폭 (셀)",width,1,12);if(kind>=5){var tile=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_ProtoType_Merge/DH/AlphaAsset/Tile/"+BackgroundOps.TileTextures[kind]+".png");if(tile)GUILayout.Label(tile,GUILayout.Width(80),GUILayout.Height(80));}EditorGUILayout.HelpBox("Scene 뷰에서 드래그합니다. 선은 가로/세로로 맞춰집니다. 비우기는 지형만 제거하며 건물은 유지합니다.",MessageType.Info);}
                        if(mode==3)
                        {
                            if(palette.Length==0)BuildPalette();snap=EditorGUILayout.Toggle("그리드 맞춤",snap);angle=EditorGUILayout.FloatField("배치 회전",angle);
                            string[] names=palette.Select(f=>f.name).ToArray();if(names.Length>0){paletteIndex=EditorGUILayout.Popup("건물",paletteIndex,names);var tex=AssetPreview.GetAssetPreview(palette[paletteIndex].sharedMesh);if(tex)GUILayout.Label(tex,GUILayout.Width(100),GUILayout.Height(80));}
                            paletteExpanded=EditorGUILayout.Foldout(paletteExpanded,"건물 썸네일 목록",true);
                            if(paletteExpanded){paletteSearch=EditorGUILayout.TextField("검색",paletteSearch);var candidates=palette.Select((f,i)=>new{f,i}).Where(x=>string.IsNullOrEmpty(paletteSearch)||x.f.name.IndexOf(paletteSearch,StringComparison.OrdinalIgnoreCase)>=0).ToArray();var style=new GUIStyle(GUI.skin.button){imagePosition=ImagePosition.ImageAbove,wordWrap=true,fontSize=9};for(int row=0;row<candidates.Length;row+=3){using(new EditorGUILayout.HorizontalScope()){for(int j=row;j<Math.Min(row+3,candidates.Length);j++){var item=candidates[j];var tex=AssetPreview.GetAssetPreview(item.f.sharedMesh);if(GUILayout.Button(new GUIContent(item.f.name,tex),style,GUILayout.Width((contentWidth-12f)/3f),GUILayout.Height(85)))paletteIndex=item.i;}}}}
                            EditorGUILayout.HelpBox("Scene 뷰에서 클릭해 배치합니다. 선택 모드에서 Unity 이동·회전·크기 도구와 Ctrl+D / Delete를 사용합니다.",MessageType.None);
                        }
                        EditorGUI.BeginChangeCheck();bool autoDress=EditorGUILayout.Toggle(new GUIContent("칠할 때 전체 드레싱","끄면 지형을 빠르게 편집합니다. 마무리할 때 드레싱 다시 생성을 누르세요."),document.dressWhilePainting);if(EditorGUI.EndChangeCheck()){Undo.RecordObject(document,"자동 드레싱 변경");document.dressWhilePainting=autoDress;EditorUtility.SetDirty(document);}
                        if(GUILayout.Button("드레싱 다시 생성"))Run(()=>{BackgroundOps.Rebuild(document);status="배경 전체의 호안·연석·차선 생성 완료";});
                        EditorGUILayout.Space();EditorGUILayout.LabelField("구획 복제",EditorStyles.boldLabel);copyMin=EditorGUILayout.Vector2IntField("최소",copyMin);copyMax=EditorGUILayout.Vector2IntField("최대",copyMax);copyOffset=EditorGUILayout.Vector2IntField("이동량",copyOffset);
                        if(GUILayout.Button("구획의 지형 + 건물 복제"))Run(()=>{BackgroundOps.CopyBlock(document,Vector2Int.Min(copyMin,copyMax),Vector2Int.Max(copyMin,copyMax),copyOffset);status="기존 지형을 포함해 구획의 지형과 건물을 복제했습니다.";});
                        EditorGUILayout.Space();EditorGUILayout.LabelField("선택한 건물 색상",EditorStyles.boldLabel);
                        if(!colorSet)colorSet=AssetDatabase.LoadAssetAtPath<JC.BuildingColors.BgColorSetProfile>("Assets/_ProtoType_Merge/JC/BuildingColors/Profiles/BG_All_Default.asset");
                        colorSet=(JC.BuildingColors.BgColorSetProfile)EditorGUILayout.ObjectField("기존 색상 프로필",colorSet,typeof(JC.BuildingColors.BgColorSetProfile),false,GUILayout.Width(contentWidth));
                        applyPlacementColors=EditorGUILayout.Toggle("배치 시 바리에이션",applyPlacementColors);
                        using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button("기본색 적용"))Run(()=>ProfileSelected(false));if(GUILayout.Button("바리에이션 섞기"))Run(()=>ProfileSelected(true));}tint=EditorGUILayout.ColorField("재질 색상 곱",tint);
                        if(GUILayout.Button("선택 건물에 적용"))Run(ApplyTint);
                        // JC | 2026-11-05 | 테스트와 실제 BE490은 같은 결과물을 사용합니다. 사용자가 이 버튼을 누른 때만 교체합니다.
                        if(GUILayout.Button(new GUIContent("편집 결과를 보스전에 적용","현재 원경을 새 결과물로 내보내 테스트와 실제 BE490에 함께 연결합니다.")))Run(()=>{var result=BackgroundOps.Export(document);var settings=JC.BattleTesting.JcBossBattleBackground.Load();settings.visualPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(result);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();status="보스전 연결: "+result;});
                        EditorGUILayout.Space();if(GUILayout.Button("JC 전달용 결과 생성",GUILayout.Height(32)))Run(()=>{status="결과 생성: "+BackgroundOps.Export(document);});
                        EditorGUILayout.HelpBox("결과를 새 폴더에 생성합니다. 현재 전투 배경 연결과 JC 브랜치는 자동 변경하지 않습니다.",MessageType.None);
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
            var selected=Selection.gameObjects.SelectMany(g=>g.GetComponentsInChildren<Renderer>()).Where(r=>r.transform.IsChildOf(document.Buildings)).Select(r=>r.gameObject).Distinct().ToArray();if(selected.Length==0)throw new Exception("작업본 건물을 선택해 주세요.");foreach(var go in selected){if((go.hideFlags&HideFlags.NotEditable)!=0)throw new Exception("잠긴 건물입니다.");BackgroundOps.ApplyBuildingProfile(document,go.GetComponent<Renderer>(),colorSet,random);}status="기존 건물 색상 프로필을 적용하고 결과를 저장했습니다.";
        }
        void ApplyTint()
        {
            var r=Selection.activeGameObject?Selection.activeGameObject.GetComponent<Renderer>():null;if(!r||!r.transform.IsChildOf(document.Buildings))throw new Exception("작업본의 건물을 선택해 주세요.");
            if((r.gameObject.hideFlags&HideFlags.NotEditable)!=0)throw new Exception("잠긴 건물입니다.");Undo.RecordObject(r,"건물 색상");BackgroundOps.Folder(document.assetFolder+"/Colors");var materials=r.sharedMaterials;
            for(int i=0;i<materials.Length;i++){var m=new Material(materials[i]);if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",m.GetColor("_BaseColor")*tint);string p=AssetDatabase.GenerateUniqueAssetPath(document.assetFolder+"/Colors/Building.mat");AssetDatabase.CreateAsset(m,p);materials[i]=m;}r.sharedMaterials=materials;EditorSceneManager.MarkSceneDirty(document.gameObject.scene);
        }
        void SceneGUI(SceneView view)
        {
            if(!document||EditorApplication.isPlaying||!document.gameObject.scene.isLoaded)return;var e=Event.current;
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
                if(mode==3){Run(()=>{if(palette.Length==0)BuildPalette();if(palette.Length==0)throw new Exception("건물 목록 없음");var placed=BackgroundOps.Place(document,palette[paletteIndex],snap?new Vector3(hover.x,.18f,hover.y):new Vector3(point.x,.18f,point.z),angle);if(applyPlacementColors&&BackgroundOps.Definition(placed.GetComponent<Renderer>()))BackgroundOps.ApplyBuildingProfile(document,placed.GetComponent<Renderer>(),colorSet,true);});}
                else if(mode==4){int sampled=BackgroundOps.Sample(document,hover);kind=sampled<0?4:sampled;status="해당 위치의 지형을 가져왔습니다.";Repaint();}
                else{start=hover;dragging=true;GUIUtility.hotControl=control;}e.Use();
            }
            if(e.type==EventType.MouseUp&&e.button==0&&dragging){dragging=false;GUIUtility.hotControl=0;if(mode==5){copyMin=Vector2Int.Min(start,hover);copyMax=Vector2Int.Max(start,hover);status="구획을 선택했습니다. 이동량을 지정해 복제하세요.";Repaint();}else Run(()=>{BackgroundOps.Paint(document,start,hover,kind==4?-1:kind,width,mode==2);status=$"{BackgroundOps.TileLabels[kind]} 적용: {BackgroundOps.PaintCells(document,start,hover,width,mode==2).Count}셀 (X/Z {start} → {hover})";});e.Use();}
            if(e.type==EventType.MouseDrag&&dragging)e.Use();view.Repaint();
        }
    }
}
