using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace JC.VFX.EditorTools
{
    [CustomEditor(typeof(HeroSkillWorkbench))]
    public sealed class HeroSkillWorkbenchEditor : Editor
    {
        private readonly Dictionary<int,bool> folds=new Dictionary<int,bool>();
        private readonly Dictionary<int,Editor> materialEditors=new Dictionary<int,Editor>();
        private JcSkillPartPreviewWindow player;
        private HeroSkillWorkbench Bench => (HeroSkillWorkbench)target;
        private void OnDisable()
        {
            if(player!=null){player.StopPart();DestroyImmediate(player);}
            foreach(var e in materialEditors.Values)if(e!=null)DestroyImmediate(e);
        }
        private static GUIContent Label(string text,string tip)=>new GUIContent(text,tip);
        public override void OnInspectorGUI()
        {
            var bench=Bench;var skill=bench.Skill;
            if(skill==null){DrawDefaultInspector();EditorGUILayout.HelpBox("스킬 대장 연결을 확인하세요.",MessageType.Error);return;}
            EditorGUILayout.LabelField(bench.key+" · "+skill.label,EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("공용 설정: 이 값을 사용하는 프리뷰와 전투에 함께 반영됩니다. 변경값은 다음 재생부터 적용되며 일부 효과는 즉시 갱신됩니다. 저장 버튼은 아래에 펼친 해당 에셋만 저장합니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(true))EditorGUILayout.ObjectField(Label("실제 연출 데이터","실제 전투와 프리뷰가 함께 사용하는 연결입니다."),skill.presentation,typeof(SkillPresentationData),false);
            using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(Label("전체 스킬 선택","이 키의 스킬을 선택합니다. Game 뷰에서 대상을 클릭하면 실제 시전합니다.")))bench.rig?.SelectKey(bench.key);
                if(GUILayout.Button(Label("시전 초기화","스킬 시전을 중단하고 위치·HP·부활 사용권을 복구합니다."))){player?.StopPart();bench.rig?.ResetPreview();}
            }
            DrawProjectile(skill.presentation);
            EditorGUILayout.Space();
            foreach(var part in skill.parts)
            {
                if(part.prefab==null)continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(PartName(part.cue),EditorStyles.boldLabel);
                EditorGUILayout.LabelField(part.role,EditorStyles.wordWrappedMiniLabel);
                using(new EditorGUI.DisabledScope(true))EditorGUILayout.ObjectField(Label("부품","현재 실제 연결된 프리팹입니다. 교체 후보는 위 역할 설명에 표시됩니다."),part.prefab,typeof(GameObject),false);
                using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button(Label("부품 재생","선택 부품만 검사합니다. 피해·회복 판정은 실행하지 않습니다."))){EnsurePlayer();player.PlayPart(part,bench.rig);}
                    if(part.signal&&GUILayout.Button(Label("발사 신호","검사 중인 차징 부품에서 비행을 시작합니다.")))player?.SignalPart();
                    if(GUILayout.Button(Label("부품 중단","이 작업대가 검사 중인 부품을 정리합니다.")))player?.StopPart();
                }
                var presets=FindPresets(part.prefab,skill.skillIndex%10!=0).ToArray();
                foreach(var preset in presets)DrawAsset(preset,PartName(part.cue)+" · "+preset.name);
                // 프리셋이 없는 임시 재료도 실제 컴포넌트와 재질에서 조절할 수 있습니다.
                int detailKey=part.prefab.GetInstanceID();
                bool show=folds.TryGetValue(detailKey,out var opened)&&opened;
                show=EditorGUILayout.Foldout(show,Label("부품 자체 설정 · 고급","프리셋에 없는 임시 부품과 조립 설정을 직접 조절합니다. 프리셋이 제어하는 값은 프리셋이 우선합니다."),true);folds[detailKey]=show;
                if(show)
                {
                    foreach(var c in part.prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if(c==null||c is JcLuminaPartPresetBinder||c.GetType().Name.EndsWith("Binder")||c.GetType().Name.EndsWith("Adapter"))continue;
                        DrawAsset(c,c.GetType().Name+" · "+c.name);
                    }
                    if(presets.Length==0)foreach(var mat in part.prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())DrawMaterial(mat);
                }
                EditorGUILayout.EndVertical();
            }
            foreach(var assembly in FindAssemblies(skill))DrawAsset(assembly.GetComponent<JcVfxPartSequence>(),"부품 조립 시간 · "+assembly.name);
        }
        private void EnsurePlayer(){if(player==null){player=CreateInstance<JcSkillPartPreviewWindow>();player.hideFlags=HideFlags.HideAndDontSave;}}
        private void DrawProjectile(SkillPresentationData data)
        {
            if(data==null||data.GetProjectileVisual()==null)return;
            int id=data.GetInstanceID();bool open=folds.TryGetValue(id,out var f)&&f;
            open=EditorGUILayout.Foldout(open,Label("실제 시전 · 투사체 이동","전체 스킬 시전에서 사용하는 이동값입니다. 부품 단독 재생의 시간·궤적과 구분됩니다."),true);folds[id]=open;
            if(!open)return;
            EditorGUILayout.HelpBox("전체 시전의 비행 속도·궤적은 여기서 조절합니다. 도착 시점에 피해가 적용되므로 속도를 바꾸면 피해 시점도 달라집니다. 다음 시전부터 적용됩니다.",MessageType.Info);
            var so=new SerializedObject(data);so.Update();
            var names=new[]{"Speed","Trajectory","ArcHeight","TrackTarget","AlignToVelocity","ImpactVisualLifetime"};
            var labels=new[]{"비행 속도","이동 궤적","궤적 높이","대상 추적","이동 방향으로 회전","도착 후 잔상 시간"};
            var tips=new[]{"초당 이동 거리입니다. 실제 도착·피해 시점에 영향을 줍니다.","직선·포물선·상공 낙하 중 실제 이동 방식을 선택합니다.","포물선 높이 또는 상공 낙하 높이(m)입니다.","확정된 대상의 현재 위치를 따라갑니다.","이동 방향을 바라보도록 투사체를 회전합니다.","도착 후 잔상을 유지하는 1배속 기준 초입니다."};
            for(int i=0;i<names.Length;i++)EditorGUILayout.PropertyField(so.FindProperty("ProjectileVisual."+names[i]),Label(labels[i],tips[i]),true);
            so.ApplyModifiedProperties();DrawSaveRestore(data);
        }
        private void DrawAsset(Object asset,string title)
        {
            if(asset==null)return;
            int id=asset.GetInstanceID();bool open=folds.TryGetValue(id,out var f)&&f;
            open=EditorGUILayout.Foldout(open,Label(title,"펼치면 실제 에셋의 설정을 이 Inspector에서 직접 조절합니다."),true);folds[id]=open;
            if(!open)return;
            using(new EditorGUI.IndentLevelScope())
            {
                using(new EditorGUI.DisabledScope(true))EditorGUILayout.ObjectField(Label("설정 에셋","다른 부품도 이 에셋을 참조하면 변경값을 공유합니다."),asset,asset.GetType(),false);
                DrawSettings(asset);
                DrawSaveRestore(asset);
            }
        }
        private void DrawMaterial(Material mat)
        {
            int id=mat.GetInstanceID();bool open=folds.TryGetValue(id,out var f)&&f;
            open=EditorGUILayout.Foldout(open,Label("재질 · "+mat.name,"프리셋이 없는 부품의 색·발광 등 셰이더 값을 조절합니다. 같은 재질 참조는 값을 공유합니다."),true);folds[id]=open;
            if(!open)return;
            if(!materialEditors.TryGetValue(id,out var editor)||editor==null){editor=CreateEditor(mat);materialEditors[id]=editor;}
            editor.OnInspectorGUI();DrawSaveRestore(mat);
        }
        private void DrawSaveRestore(Object asset)
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(Label("이 설정 저장","이 설정 에셋의 현재 값을 디스크에 확정합니다. 부품 컴포넌트이면 해당 프리팹을 저장합니다.")))Save(asset);
                if(GUILayout.Button(Label("저장값 복원","이 에셋의 미저장 조절을 버리고 마지막 디스크 저장값을 다시 읽습니다.")))Restore(asset);
            }
        }
        public static void Save(Object asset)
        {
            string path=AssetDatabase.GetAssetPath(asset);if(string.IsNullOrEmpty(path))return;
            if(path.EndsWith(".prefab")){var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);PrefabUtility.SavePrefabAsset(root);}
            else{EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);}
        }
        public static void Restore(Object asset)
        {
            string path=AssetDatabase.GetAssetPath(asset);if(string.IsNullOrEmpty(path))return;
            // 재임포트만으로는 메모리의 미저장 SO 값이 유지될 수 있으므로 디스크 사본에서 읽습니다.
            string temp=System.IO.Path.GetDirectoryName(path).Replace('\\','/')+"/__JCWorkbenchRestore_"+Guid.NewGuid().ToString("N")+System.IO.Path.GetExtension(path);
            // CopyAsset은 프리팹의 메모리 캐시를 복제할 수 있으므로 파일 바이트를 직접 읽습니다.
            System.IO.File.Copy(path,temp,false);
            try
            {
                AssetDatabase.ImportAsset(temp,ImportAssetOptions.ForceSynchronousImport);
                Object saved=AssetDatabase.LoadMainAssetAtPath(temp);
                if(asset is Component)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string unused,out long localId);
                    saved=((GameObject)saved).GetComponentsInChildren<Component>(true).FirstOrDefault(c=>c!=null&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c,out string guid,out long id)&&id==localId);
                }
                if(saved==null)throw new InvalidOperationException("저장된 부품을 찾지 못했습니다: "+path);
                Undo.RecordObject(asset,"스킬 설정 저장값 복원");
                var from=new SerializedObject(saved);var to=new SerializedObject(asset);var it=from.GetIterator();bool enter=true;
                while(it.NextVisible(enter)){enter=false;if(it.propertyPath!="m_Script")to.CopyFromSerializedProperty(it);}
                // 프리팹 내부 참조는 사본의 객체가 아니라 원본의 동일 fileID를 가리켜야 합니다.
                var original=AssetDatabase.LoadMainAssetAtPath(path);var candidates=original is GameObject root?root.GetComponentsInChildren<Component>(true).Cast<Object>().Concat(root.GetComponentsInChildren<Transform>(true).Select(t=>(Object)t.gameObject)).ToArray():new[]{original};
                var refs=to.GetIterator();
                while(refs.Next(true))
                    if(refs.propertyType==SerializedPropertyType.ObjectReference&&refs.objectReferenceValue!=null&&AssetDatabase.GetAssetPath(refs.objectReferenceValue)==temp&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(refs.objectReferenceValue,out string g,out long id))
                        refs.objectReferenceValue=candidates.FirstOrDefault(c=>c!=null&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c,out string cg,out long ci)&&ci==id);
                to.ApplyModifiedProperties();LivePush(asset);
            }
            finally { AssetDatabase.DeleteAsset(temp); }
        }
        private static void DrawSettings(Object asset)
        {
            var so=new SerializedObject(asset);so.Update();
            if(asset is JcSimpleVisualPart&&so.FindProperty("externallyDriven")?.boolValue==true)
                EditorGUILayout.HelpBox("이 부품의 시간·오프셋·포물선은 단독 재생용입니다. 전체 시전의 비행은 위의 ‘실제 시전 · 투사체 이동’ 설정을 사용합니다.",MessageType.Info);
            var follow=so.FindProperty("followBasic");var basic=so.FindProperty("basicRef");
            bool locked=follow!=null&&follow.boolValue&&basic!=null&&basic.objectReferenceValue!=null;
            var lockedFields=new HashSet<string>();
            if(locked)
            {
                EditorGUILayout.HelpBox("위치·형태는 Basic 따름 중입니다. 해당 항목은 Basic 프리셋에서 조절하거나 따름을 해제하세요.",MessageType.Info);
                var type=asset.GetType().Assembly.GetType("JC.VFX."+asset.GetType().Name+"Editor");
                if(type==null)type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("JC.VFX."+asset.GetType().Name+"Editor")).FirstOrDefault(t=>t!=null);
                var field=type?.GetField("TransformProps",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
                if(field?.GetValue(null) is string[] names)foreach(var n in names)lockedFields.Add(n);
            }
            EditorGUI.BeginChangeCheck();var it=so.GetIterator();bool enter=true;
            while(it.NextVisible(enter))
            {
                enter=false;if(it.propertyPath=="m_Script"||it.propertyPath=="previewProgress"||it.propertyPath=="previewSeed")continue;
                if(asset is JusticeTrailPreset justice)
                {
                    var t=justice.targets;
                    if(t.trailPrefab==null&&(it.propertyPath=="trail"||it.propertyPath=="spark"))continue;
                    if(t.impactPrefab==null&&it.propertyPath=="impact")continue;
                    if(t.arcStrokePrefab==null&&it.propertyPath=="arcStroke")continue;
                    if(t.arcPointPrefab==null&&it.propertyPath=="arcPoint")continue;
                }
                if(asset is FlareImpactPreset && new[]{"orbScale","summonOffset","chargeDuration","chargeGrowTime","speed","arcHeight","flightScale","targetHeight","trailTime","trailWidth","emberRate","emberSize","emberNoise","lingerTime"}.Contains(it.propertyPath))continue;
                bool structural=it.propertyPath=="targets"||it.propertyPath=="targetPrefab"||it.propertyPath=="targetParts"||it.propertyPath=="partMode"||it.propertyPath=="segment";
                string tip=string.IsNullOrEmpty(it.tooltip)?"이 설정"+"의 값을 조절합니다. 동일 에셋을 사용하는 부품에 함께 반영됩니다.":it.tooltip;
                using(new EditorGUI.DisabledScope(structural||lockedFields.Contains(it.propertyPath)))EditorGUILayout.PropertyField(it,new GUIContent(it.displayName,tip),true);
            }
            so.ApplyModifiedProperties();if(EditorGUI.EndChangeCheck()){LivePush(asset);SceneView.RepaintAll();}
        }
        public static void LivePush(Object preset)
        {
            if(!EditorApplication.isPlaying)return;
            foreach(var c in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(c is JcLuminaPartPresetBinder lumina&&lumina.preset==preset)lumina.ApplyNow();
                else if(c is KAimShotVfx shot&&preset is KAimShotPreset p&&shot.UsesPreset(p))shot.PullFromPreset();
                else if(c.GetType().Namespace=="JC.VFX"&&c.GetType().Name.EndsWith("Binder"))
                {
                    var active=c.GetType().GetProperty("ActivePreset");
                    if(active?.GetValue(c) as Object==preset)c.GetType().GetMethod("ApplyNow",Type.EmptyTypes)?.Invoke(c,null);
                }
            }
        }
        public static IEnumerable<ScriptableObject> FindPresets(GameObject root,bool alternate)
        {
            var found=new HashSet<ScriptableObject>();
            foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(c==null)continue;var so=new SerializedObject(c);var it=so.GetIterator();
                bool two=so.FindProperty("presetAlt")?.objectReferenceValue!=null;
                while(it.Next(true))
                {
                    if(two&&it.propertyPath==(alternate?"preset":"presetAlt"))continue;
                    if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue is ScriptableObject p)found.Add(p);
                }
            }
            return found.OrderBy(p=>p.name);
        }
        public static IEnumerable<GameObject> FindAssemblies(JcSkillPartsManifest.Skill skill)
        {
            var registry=AssetDatabase.LoadAssetAtPath<EffectRegistry>("Assets/RenderFX/_Seam/Data/Merged/JC_Merged_EffectRegistry.asset");
            if(registry==null||skill.presentation==null)return Array.Empty<GameObject>();
            return skill.presentation.Attack.Beats.SelectMany(b=>b.Cues).SelectMany(c=>c.EffectIds).Select(id=>registry.Get(id)).Where(g=>g!=null&&g.GetComponent<JcVfxPartSequence>()!=null).Distinct();
        }
        private static string PartName(string cue)
        {
            var key=cue.Substring(cue.LastIndexOf('_')+1);
            switch(key){case "charge":return "차징";case "flight":return "비행";case "impact":return "착탄";case "sweep":return "이동 궤적";case "muzzle":return "총구";case "main":return "최초 번개";case "chain":return "연쇄 번개";case "shock":return "감전";case "land":return "회복 착지";case "merge":return "합류";case "spawn":return "등장";case "warp":return "워프";case "beam":return "빔·착탄";case "revive":return "부활";case "barrage":return "전체공격";case "meteor":return "혜성 · 교체 후보";default:return cue;}
        }
    }
}
