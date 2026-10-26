using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JC.BuildingColors;
using JC.UiRecolor;

namespace JC.BuildingColors.EditorTools
{
    public static class BgColorStorage
    {
        public const string Folder="Assets/_ProtoType_Merge/JC/BuildingColors/Profiles";
        public static void Save(BgColorModifier c)
        {
            if(!c.profile||!AssetDatabase.Contains(c.profile)||c.profile.buildingId!=c.definition.buildingId)throw new InvalidOperationException("같은 건물의 프로필을 연결하세요.");
            Undo.RecordObject(c.profile,"건물 색상 프로필 저장");c.EnsureParts();c.profile.parts=BgColorModifier.Copy(c.parts);c.profile.partIds=(string[])c.definition.Ids.Clone();EditorUtility.SetDirty(c.profile);AssetDatabase.SaveAssetIfDirty(c.profile);
        }
        public static BgColorProfile Create(BgColorModifier c,string path)
        {
            path=AssetDatabase.GenerateUniqueAssetPath(path);var p=ScriptableObject.CreateInstance<BgColorProfile>();p.buildingId=c.definition.buildingId;c.EnsureParts();p.parts=BgColorModifier.Copy(c.parts);p.partIds=(string[])c.definition.Ids.Clone();AssetDatabase.CreateAsset(p,path);AssetDatabase.SaveAssetIfDirty(p);Undo.RecordObject(c,"건물 색상 프로필 연결");c.profile=p;Mark(c);return p;
        }
        static void ValidateEntries(BgColorSetEntry[] entries)
        {
            if(entries.Any(e=>e==null||string.IsNullOrEmpty(e.buildingId)||!BgColorDefinition.ValidValues(e.parts,e.partIds)||float.IsNaN(e.weight)||float.IsInfinity(e.weight))||entries.Select(e=>e.Key).Distinct().Count()!=entries.Length)
                throw new InvalidOperationException("건물 종류 + 바리에이션 ID 또는 파츠 구성이 잘못되었습니다.");
        }
        public static void SaveSet(BgColorRoot root,BgColorSetProfile profile)
        {
            if(!profile||!AssetDatabase.Contains(profile))throw new InvalidOperationException("전체 프로필을 연결하세요.");
            var children=root.Children.Where(c=>c.definition&&c.isActiveAndEnabled).ToArray();
            if(children.Any(c=>c.targetRadius>0))throw new InvalidOperationException("위치별 조절기는 바리에이션과 고정 지정으로 옮긴 뒤 저장하세요.");
            var entries=children.Select(c=>{c.EnsureParts();var previous=profile.buildings?.FirstOrDefault(e=>e!=null&&e.buildingId==c.definition.buildingId&&(e.variantId??string.Empty)==(c.variantId??string.Empty))??root.profile?.buildings?.FirstOrDefault(e=>e!=null&&e.buildingId==c.definition.buildingId&&(e.variantId??string.Empty)==(c.variantId??string.Empty));return new BgColorSetEntry{buildingId=c.definition.buildingId,variantId=c.variantId,displayName=previous?.displayName,weight=previous?.weight??1,parts=BgColorModifier.Copy(c.parts),partIds=(string[])c.definition.Ids.Clone()};}).ToArray();
            ValidateEntries(entries);
            // A scene may contain only a subset of the library. Preserve the other types and variants.
            var retained=(profile.buildings??Array.Empty<BgColorSetEntry>()).Where(e=>e!=null&&!entries.Any(n=>n.Key==e.Key));
            var merged=retained.Concat(entries).ToArray();ValidateEntries(merged);
            Undo.RecordObject(profile,"전체 건물 색상 저장");profile.buildings=merged;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }
        public static void SaveVariant(BgColorModifier c)
        {
            var root=c.GetComponentInParent<BgColorRoot>();
            if(!root||!root.profile||string.IsNullOrEmpty(c.variantId))throw new InvalidOperationException("바리에이션 전체 프로필을 연결하세요.");
            var entry=root.profile.buildings.FirstOrDefault(e=>e.buildingId==c.definition.buildingId&&e.variantId==c.variantId);
            if(entry==null)throw new InvalidOperationException("해당 바리에이션을 찾지 못했습니다.");
            Undo.RecordObject(root.profile,"바리에이션 색상 저장");c.EnsureParts();entry.parts=BgColorModifier.Copy(c.parts);entry.partIds=(string[])c.definition.Ids.Clone();EditorUtility.SetDirty(root.profile);AssetDatabase.SaveAssetIfDirty(root.profile);
        }
        public static void LoadSet(BgColorRoot root)
        {
            if(!root.profile||root.profile.buildings==null)throw new InvalidOperationException("전체 프로필이 비어 있습니다.");
            var entries=root.profile.buildings;ValidateEntries(entries);
            foreach(var c in root.Children){if(c.targetRadius>0)continue;var entry=entries.FirstOrDefault(e=>c.definition&&e.buildingId==c.definition.buildingId&&(e.variantId??string.Empty)==(c.variantId??string.Empty));if(entry==null)continue;Undo.RecordObject(c,"전체 건물 색상 불러오기");c.parts=c.definition.Remap(entry.parts,entry.partIds);c.partIds=(string[])c.definition.Ids.Clone();c.showOriginal=false;c.highlightedPart=-1;if((c.hideFlags&HideFlags.DontSave)==0&&(c.gameObject.hideFlags&HideFlags.DontSave)==0)Mark(c);}
            root.RefreshTargets();
        }
        public static void Mark(UnityEngine.Object obj){EditorUtility.SetDirty(obj);if(obj is Component c&&!Application.isPlaying){PrefabUtility.RecordPrefabInstancePropertyModifications(c);EditorSceneManager.MarkSceneDirty(c.gameObject.scene);}SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();}
        public static bool Button(string label,string tip)=>GUILayout.Button(new GUIContent(label,tip));
        public static bool Equal(BgPartColor[] a,BgPartColor[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Zip(b,(x,y)=>x.hue==y.hue&&x.lightness==y.lightness&&x.saturation==y.saturation&&x.ignoreSourceColorAndShading==y.ignoreSourceColorAndShading).All(x=>x);
    }
    [CustomEditor(typeof(BgColorModifier))]
    public sealed class BgColorModifierEditor : UnityEditor.Editor
    {
        void OnEnable(){Undo.undoRedoPerformed+=UndoChanged;}
        void OnDisable(){Undo.undoRedoPerformed-=UndoChanged;}
        void UndoChanged(){if(target is BgColorModifier c){c.ApplyNow();SceneView.RepaintAll();Repaint();}}
        public override void OnInspectorGUI()
        {
            var c=(BgColorModifier)target;
            serializedObject.Update();Field("definition","건물 파츠 정의");Field("profile","건물 색상 프로필");
            if(serializedObject.ApplyModifiedProperties()){BgColorStorage.Mark(c);c.Rebind();c.ApplyNow();}
            if(!c.definition){EditorGUILayout.HelpBox("건물 파츠 정의를 연결하세요.",MessageType.Info);return;}
            c.EnsureParts();
            if(!string.IsNullOrEmpty(c.variantId))EditorGUILayout.LabelField("바리에이션",c.variantId);
            if(c.profile&&!c.CanLoad)EditorGUILayout.HelpBox("프로필의 건물 종류 또는 파츠 수가 다릅니다. 같은 건물의 프로필을 연결하거나 새 프로필로 저장하세요.",MessageType.Warning);
            EditorGUILayout.LabelField(c.definition.buildingId,EditorStyles.boldLabel);
            EditorGUILayout.LabelField("연결된 재질 슬롯",c.TargetCount.ToString());
            if(c.TargetCount==0){var owner=c.GetComponentInParent<BgColorRoot>();bool distributed=owner&&owner.distributeVariants&&string.IsNullOrEmpty(c.variantId)&&owner.Children.Any(x=>x.definition==c.definition&&!string.IsNullOrEmpty(x.variantId));EditorGUILayout.HelpBox(distributed?"이 건물 종류는 바리에이션으로 표시 중입니다. 부모 아래의 ‘색상 바리에이션 미리보기’에서 색상을 조절하세요.":"같은 씬의 대상 범위에서 일치하는 원본 재질을 찾지 못했습니다. 부모의 대상 범위를 확인하세요.",distributed?MessageType.Info:MessageType.Warning);}
            if(!string.IsNullOrEmpty(c.Status))EditorGUILayout.HelpBox(c.Status,MessageType.Error);
            if(!string.IsNullOrEmpty(c.definition.classificationNote))EditorGUILayout.HelpBox(c.definition.classificationNote,MessageType.Info);
            EditorGUI.BeginChangeCheck();bool original=EditorGUILayout.Toggle(new GUIContent("원본 비교","켜면 이 건물의 원본 텍스처를 표시합니다. 조정값과 프로필은 그대로 유지됩니다."),c.showOriginal);
            string[] labels=new[]{"강조 없음"}.Concat(c.definition.Names).ToArray();int highlight=EditorGUILayout.Popup(new GUIContent("파츠 영역 보기","선택한 파츠의 마스크를 분홍색으로 강조합니다. 저장되는 색상값에 영향을 주지 않습니다."),c.highlightedPart+1,labels)-1;
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(c,"건물 색상 비교");c.showOriginal=original;c.highlightedPart=highlight;BgColorStorage.Mark(c);c.ApplyNow();}
            for(int i=0;i<c.parts.Length;i++)DrawPart(c,i);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("조절값은 즉시 미리보기에 반영됩니다. 아래 저장 버튼으로 색상과 토글을 프로필에 저장하세요. 씬 저장과 프로필 저장은 별개입니다.\n원본 색·명암 무시를 켠 파츠는 무늬와 텍스처 음영 없이 목표색으로 채웁니다. 씬 조명과 그림자는 유지됩니다.",MessageType.None);
            if(!string.IsNullOrEmpty(c.variantId)){
                if(BgColorStorage.Button("이 바리에이션을 전체 프로필에 저장","같은 바리에이션을 쓰는 두 탐사씬에 함께 반영됩니다. 고정 배치와 시드는 변경하지 않습니다."))BgColorStorage.SaveVariant(c);
                return;
            }
            if(c.profile)EditorGUILayout.LabelField(BgColorStorage.Equal(c.parts,c.definition.Remap(c.profile.parts,c.profile.partIds))?"프로필과 동일한 값":"● 저장하지 않은 조정값",EditorStyles.miniLabel);
            using(new EditorGUI.DisabledScope(!c.CanLoad)){
                if(BgColorStorage.Button("프로필 불러오기","연결된 같은 건물 프로필로 현재 값을 교체합니다. 저장하지 않은 조정값은 사라집니다.")){Undo.RecordObject(c,"건물 프로필 불러오기");c.LoadProfile();BgColorStorage.Mark(c);}
                if(BgColorStorage.Button("현재 값을 프로필에 저장","이 건물의 현재 파츠별 색상값만 연결된 파일에 저장합니다. 다른 건물·전체 프로필·씬은 저장하지 않습니다."))BgColorStorage.Save(c);
            }
            if(BgColorStorage.Button("현재 값으로 새 프로필 저장…","새 프로필 파일을 만들고 이 건물에 연결합니다. 기존 파일을 덮어쓰지 않습니다.")){
                string path=EditorUtility.SaveFilePanelInProject("건물 색상 프로필",c.definition.buildingId+"_Color","asset","저장 위치",BgColorStorage.Folder);if(!string.IsNullOrEmpty(path))BgColorStorage.Create(c,path);
            }
            if(BgColorStorage.Button("초기 색상으로 복원","현재 건물의 시작 색상으로 돌아갑니다. House002는 조정한 코랄·회색 바닥, 옥상 바닥은 무채색 회색입니다. 프로필 파일은 변경하지 않습니다.")){Undo.RecordObject(c,"초기 건물 색상 복원");c.ResetInitial();BgColorStorage.Mark(c);}
            if(BgColorStorage.Button("씬에서 건물 보기","같은 씬의 대상 건물을 Scene 뷰에 맞춰 보여 줍니다. 색상이나 카메라 오브젝트 설정은 변경하지 않습니다."))Frame(c);
        }
        void Field(string name,string label){var p=serializedObject.FindProperty(name);EditorGUILayout.PropertyField(p,new GUIContent(label,p.tooltip));}
        static void DrawPart(BgColorModifier c,int index)
        {
            string id=c.definition.Ids[index];string tip="이 파츠에 속한 표면의 색상·명도·채도를 조절합니다.";
            if(id=="accent")tip=c.definition.buildingId=="BGStore003"?"창 아래 하단 띠와 문 주변의 올리브색 도장면을 조절합니다. 간판·햄버거·소품은 별도 항목입니다.":"기존 파츠 밖의 올리브색 도장·소품을 조절합니다. 공유 팔레트 건물의 하단·층간 띠도 포함합니다. 파츠 영역 보기로 확인할 수 있습니다.";
            if(id=="accent2")tip="진입차단봉 금속 포인트와 측면 화단 테두리, 기존 소품 포인트를 조절합니다. 식생·화분 몸체·토양은 각 기존 항목에서 조절합니다.";
            if(id=="sign_symbol")tip="간판의 햄버거 문양을 조절합니다. 간판 바탕색에는 영향을 주지 않습니다.";
            if(id=="accent"&&c.definition.buildingId=="BGStore004")tip="건물 하단 띠와 문·창 주변의 올리브색 도장면입니다. 우체통·적재 박스·간판·진입차단봉은 제외합니다.";
            if(id=="accent"&&c.definition.buildingId=="BGRowHouse002")tip="건물 하단 테두리의 보조 도장을 조절합니다. 정면 세로 돌출부는 중앙 블록 항목에서 조절합니다.";
            if(id=="central_block")tip="정면 중앙 돌출 블록의 앞면·양쪽 측면·윗부분을 함께 조절합니다. 블록 안의 유리는 창문, 아래 현관 차양은 지붕·차양 항목에서 조절합니다.";
            if(id=="mailbox")tip="정면 우체통의 색상·명도·채도를 조절합니다. 건물 도장과 적재 박스에는 영향을 주지 않습니다.";
            if(id=="crates")tip="상점 측면에 적재된 박스의 색상·명도·채도를 조절합니다.";
            if(id=="standing_sign")tip="측면 입간판과 그 받침의 색상을 조절합니다. 상단 간판과 분리되어 있습니다.";
            if(id=="side_entrance")tip="측면 출입구의 문·문틀·발판 색상을 조절합니다. 현관 및 간판과 분리되어 있습니다.";
            if(id=="warning_paint")tip="안전봉·보호대·리프트 받침·전면 연석의 노란 경고 도장을 조절합니다. 검은 줄무늬와 보조도장은 별도로 유지합니다.";
            if(id=="sign_face"&&(c.definition.buildingId=="BGStore001"||c.definition.buildingId=="BGStore003"))tip="간판 바탕의 색상을 조절합니다. 지붕·차양과 분리되어 있습니다.";
            var p=c.parts[index];EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.LabelField(new GUIContent(c.definition.Names[index],tip),EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();p.ignoreSourceColorAndShading=EditorGUILayout.Toggle(new GUIContent("원본 색·명암 무시","켜면 이 파츠의 원본 색·무늬·텍스처 음영을 무시하고 목표색으로 채웁니다. 끄면 원본 색차와 명암을 유지합니다. 마스크 경계·알파와 씬 조명·그림자는 유지되며 개별·전체 프로필에 저장됩니다."),p.ignoreSourceColorAndShading);
            if(EditorGUI.EndChangeCheck())Set(c,index,p);
            EditorGUI.BeginChangeCheck();var sample=RecolorMath.LchToSrgb(new Vector3(p.lightness,p.saturation*.004f,p.hue));
            var picked=EditorGUILayout.ColorField(new GUIContent("색상 견본","목표 기준색입니다. 색상표로 선택하면 아래 세 값을 함께 맞춥니다. 원본 색·명암 무시가 켜져 있으면 마스크 내부를 이 색으로 채웁니다. 씬 조명에 따라 화면의 실제 색은 달라집니다."),sample,true,false,false);
            if(EditorGUI.EndChangeCheck()){var lch=RecolorMath.SrgbToLch(picked);p.hue=Mathf.Repeat(lch.z,360);p.lightness=lch.x;p.saturation=Mathf.Clamp(lch.y*250,0,100);Set(c,index,p);}
            EditorGUI.BeginChangeCheck();p.hue=EditorGUILayout.Slider(new GUIContent("색상","0~360도 색상각입니다. 원본 색·명암 무시가 꺼져 있으면 파츠 내부의 원본 색 차이를 유지합니다."),p.hue,0,360);
            p.lightness=EditorGUILayout.Slider(new GUIContent("명도","0~1 기준 밝기입니다. 0은 검정, 1은 흰색입니다. 원본 색·명암 무시가 꺼져 있으면 원본의 명암 차이를 유지합니다."),p.lightness,0,1);
            p.saturation=EditorGUILayout.Slider(new GUIContent("채도","0~100 지각 채도입니다. 0은 무채색 회색입니다. 강한 값에서는 출력 가능한 색 범위에 맞춰 보정됩니다."),p.saturation,0,100);
            if(EditorGUI.EndChangeCheck())Set(c,index,p);
            if(BgColorStorage.Button("이 파츠 초기값","이 파츠의 색상·명도·채도와 원본 색·명암 무시 토글을 시작값으로 복원합니다. 프로필 파일은 변경하지 않습니다."))Set(c,index,c.definition.Defaults[index]);
            EditorGUILayout.EndVertical();
        }
        static void Set(BgColorModifier c,int i,BgPartColor p){Undo.RecordObject(c,"건물 파츠 색상 조절");c.parts[i]=p;BgColorStorage.Mark(c);c.ApplyNow();}
        static void Frame(BgColorModifier c){var rs=c.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>()).Where(r=>r.sharedMaterials.Any(m=>c.definition.bindings.Any(b=>b.material==m))).ToArray();if(rs.Length==0||!SceneView.lastActiveSceneView)return;var bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);SceneView.lastActiveSceneView.Frame(bounds,false);}
    }
    [CustomEditor(typeof(BgColorRoot))]
    public sealed class BgColorRootEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var root=(BgColorRoot)target;serializedObject.Update();foreach(var name in new[]{"profile","targetRoot","distributeVariants","variantSeed","fixedVariants"}){var p=serializedObject.FindProperty(name);EditorGUILayout.PropertyField(p,new GUIContent(name=="profile"?"전체 색상 프로필":name=="targetRoot"?"대상 건물 범위":name=="distributeVariants"?"바리에이션 배분":name=="variantSeed"?"배분 시드":"고정 바리에이션",p.tooltip),true);}
            if(serializedObject.ApplyModifiedProperties()){BgColorStorage.Mark(root);root.RefreshTargets();}
            EditorGUILayout.LabelField("색상 조절기",root.Children.Length.ToString());
            EditorGUILayout.LabelField("공유 바리에이션 출력",root.GeneratedVariantCount.ToString());
            if(root.distributeVariants&&BgColorStorage.Button("시드를 바꿔 색상 재배치","고정 지정은 유지하고 나머지 주택 색상을 다시 배분합니다. 건물 위치는 바뀌지 않습니다.")){Undo.RecordObject(root,"주택 색상 재배치");root.variantSeed=unchecked(root.variantSeed+1);BgColorStorage.Mark(root);root.RefreshTargets();}
EditorGUILayout.HelpBox("‘색상 바리에이션 미리보기’의 하위 항목에서 색상을 조절하고 프로필에 저장하세요. 배분 비중은 전체 프로필의 각 바리에이션 항목에서 조절합니다. 고정 지정과 시드는 씬에, 색상은 공용 프로필에 저장됩니다. 바리에이션이 없는 건물은 기존 기본색을 유지합니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(!root.profile)){
                if(BgColorStorage.Button("전체 프로필 불러오기","건물 ID가 일치하는 항목들의 조정값을 전체 프로필에서 불러옵니다. 개별 프로필 파일은 변경하지 않습니다."))BgColorStorage.LoadSet(root);
                if(BgColorStorage.Button("전체 현재 값을 프로필에 저장","하위 건물들의 현재 색상값을 연결된 전체 프로필 파일에 저장합니다. 씬과 개별 프로필 파일은 저장하지 않습니다."))BgColorStorage.SaveSet(root,root.profile);
            }
            if(BgColorStorage.Button("새 전체 프로필 저장…","현재 기본색과 바리에이션 조정값으로 새 전체 프로필을 만듭니다. 기존 파일은 덮어쓰지 않습니다.")){
                string path=EditorUtility.SaveFilePanelInProject("전체 건물 색상 프로필","BG_ColorSet","asset","저장 위치",BgColorStorage.Folder);if(!string.IsNullOrEmpty(path)){var p=CreateInstance<BgColorSetProfile>();AssetDatabase.CreateAsset(p,AssetDatabase.GenerateUniqueAssetPath(path));BgColorStorage.SaveSet(root,p);Undo.RecordObject(root,"전체 프로필 연결");root.profile=p;BgColorStorage.Mark(root);}
            }
            if(BgColorStorage.Button("대상 건물 다시 찾기","대상 범위의 원본 재질을 기준으로 같은 종류의 모든 건물을 다시 연결합니다."))root.RefreshTargets();
            if(BgColorStorage.Button("전체 원본 비교","모든 하위 건물을 원본 텍스처로 표시합니다. 색상 조정값은 유지합니다."))Compare(root,true);
            if(BgColorStorage.Button("전체 조정 결과 보기","모든 하위 건물의 원본 비교와 파츠 강조를 끄고 조정 결과를 표시합니다."))Compare(root,false);
        }
        static void Compare(BgColorRoot root,bool original){foreach(var c in root.Children){Undo.RecordObject(c,"전체 건물 비교");c.showOriginal=original;c.highlightedPart=-1;BgColorStorage.Mark(c);c.ApplyNow();}}
    }
}
