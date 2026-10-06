using UnityEngine;
using UnityEditor;
using JC.VFX;
using System.Linq;
using UnityEngine.Timeline;
namespace JC.VFX.EditorTools
{
    [CustomEditor(typeof(FlareBombTuningRig))]
    public sealed class FlareBombTuningRigEditor : Editor
    {
        FlareVolumePreset draft;
        FlareImpactPreset impactDraft;
        FlareVolumePreset draftSource;
        GameObject orb, impact;
        FlareVolumeVisual visual;
        bool dark, playing;
        float time;
        double previous;
        FlareBombTuningRig Rig => (FlareBombTuningRig)target;
        FlareVolumePreset Source => dark?Rig.dark:Rig.normal;
        SkillPresentationData Skill => AssetDatabase.LoadAssetAtPath<SkillPresentationData>(
            "Assets/ASB_Work/Skills/SkillPresentation_"+(dark?"2011":"2010")+".asset");
        float TimelineHoldSeconds()
        {
            var timeline=Skill?Skill.ResolveTimeline("10002"):null;
            if(!timeline||!timeline.markerTrack)return Source.summonSeconds;
            double cast=-1,fire=-1;
            foreach(var marker in timeline.markerTrack.GetMarkers())
            {
                var so=new SerializedObject((Object)marker);var cue=so.FindProperty("_cueName");
                if(cue==null)continue;
                if(cue.stringValue=="cast")cast=marker.time;
                if(cue.stringValue=="fire")fire=marker.time;
            }
            if(cast<0||fire<=cast)return Source.summonSeconds;
            var points=timeline.markerTrack.GetMarkers().Select(m=>m.time).Where(t=>t>cast&&t<fire).Concat(new[]{cast,fire}).Distinct().OrderBy(t=>t).ToArray();
            double seconds=0;
            for(int i=1;i<points.Length;i++)seconds+=(points[i]-points[i-1])/PresentationTimelineSpeed.SpeedAt(timeline,(points[i]+points[i-1])*.5);
            return (float)seconds;
        }
        void OnEnable(){EditorApplication.update+=UpdatePreview;Reload();}
        void OnDisable(){EditorApplication.update-=UpdatePreview;Clear();DestroyDraft();}
        void DestroyDraft()
        {
            if(draftSource)
            {
                foreach(var f in FindObjectsOfType<FlareVolumeVisual>())if(f.preset==draft)f.preset=draftSource;
                foreach(var f in FindObjectsOfType<FlareVolumeBurst>())if(f.preset==draft)f.preset=draftSource;
                foreach(var b in FindObjectsOfType<JcLuminaPartPresetBinder>())if(b.preset==impactDraft){b.preset=draftSource.impact;b.ApplyNow();}
            }
            if(draft)DestroyImmediate(draft);if(impactDraft)DestroyImmediate(impactDraft);
        }
        void Reload()
        {
            Clear();DestroyDraft();if(!Source)return;
            draftSource=Source;
            // HideAndDontSave에는 NotEditable이 포함되어 PropertyField가 비활성화됩니다.
            // 초안의 비저장·숨김 속성만 유지하고 인스펙터 편집은 허용합니다.
            draft=Instantiate(Source);draft.hideFlags=HideFlags.HideInHierarchy|HideFlags.DontSave;
            if(Source.impact){impactDraft=Instantiate(Source.impact);impactDraft.hideFlags=HideFlags.HideInHierarchy|HideFlags.DontSave;draft.impact=impactDraft;}
        }
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("이 오브젝트의 프리뷰는 전투를 실행하지 않습니다. 조절값은 임시 초안이며 ‘공용 프리셋 저장’을 누르면 모든 전투 씬에 적용됩니다. 선택 해제 전 저장해 주세요.",MessageType.Info);
            bool next=GUILayout.Toolbar(dark?1:0,new[]{"일반 불꽃","5단계 흑염"})==1;
            if(next!=dark){dark=next;Reload();}
            if(!draft){DrawDefaultInspector();return;}
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button("▶ 반복 프리뷰")){Clear();playing=true;previous=EditorApplication.timeSinceStartup;Create();}
                if(GUILayout.Button("■ 프리뷰 종료"))Clear();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("공용 프리셋 저장"))
            {
                Undo.RecordObject(Source,"플레어봄 공용 사양 저장");
                var originalImpact=Source.impact;string sourceName=Source.name;EditorUtility.CopySerialized(draft,Source);Source.impact=originalImpact;Source.name=sourceName;
                if(originalImpact&&impactDraft){Undo.RecordObject(originalImpact,"플레어봄 폭발 저장");string impactName=originalImpact.name;EditorUtility.CopySerialized(impactDraft,originalImpact);originalImpact.name=impactName;originalImpact.hideFlags=HideFlags.None;EditorUtility.SetDirty(originalImpact);}
                Source.hideFlags=HideFlags.None;EditorUtility.SetDirty(Source);AssetDatabase.SaveAssets();
            }
            if(GUILayout.Button("저장값 다시 읽기"))Reload();
            EditorGUILayout.EndHorizontal();
            var settings=new SerializedObject(draft);settings.Update();
            EditorGUILayout.LabelField("회전 — 정점과 비행은 별도 설정",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(settings.FindProperty("apexSpinDegreesPerSecond"),new GUIContent("정점 구체 회전 (도/초)","발사 전 머리 위에서 회전하는 속도입니다."));
            EditorGUILayout.PropertyField(settings.FindProperty("rollDegreesPerSecond"),new GUIContent("비행 나선 회전 (도/초)","0이면 비행 중 나선 회전이 없습니다. 정점 회전과 독립적입니다."));
            settings.ApplyModifiedProperties();
            using(new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("생성→발사 (타임라인 초)",TimelineHoldSeconds());
                var data=Skill?Skill.GetProjectileVisual():null;
                if(data!=null){EditorGUILayout.FloatField("이동 속도 (스킬 데이터)",data.Speed);EditorGUILayout.FloatField("가속 배율 (스킬 데이터)",data.AccelerationMultiplier);}
            }
            DrawFields(draft,new[]{"m_Script","impact","startAtApex","summonSeconds","riseHeight","startScale","apexHoldSeconds","apexSpinDegreesPerSecond","rollDegreesPerSecond"});
            if(impactDraft)
            {
                EditorGUILayout.Space();EditorGUILayout.LabelField("폭발 — 크기·섬광·방사 불꽃·충격 링",EditorStyles.boldLabel);
                var so=new SerializedObject(impactDraft);so.Update();
                foreach(string n in new[]{"groundWidth","groundHeight","spikeWidth2","spikeHeight","ringSize","hotColor","goldColor","burstEmission","growFrac","fadeStart","spikeCount","spikeLen","spikeWidth","lenJitter","widthJitter","subCount","flashSize","flashIntensity","flashFade","ringColor","ringEmission","ringWidth","burstDuration","ringDuration","ringGroundOffset"})
                    EditorGUILayout.PropertyField(so.FindProperty(n),true);
                so.ApplyModifiedProperties();
            }
        }
        static void DrawFields(Object obj,string[] exclude)
        {var so=new SerializedObject(obj);so.Update();DrawPropertiesExcluding(so,exclude);so.ApplyModifiedProperties();}
        void Create()
        {
            if(EditorApplication.isPlaying)return;
            orb=Instantiate(dark?Rig.darkOrb:Rig.normalOrb);impact=Instantiate(dark?Rig.darkImpact:Rig.normalImpact);
            foreach(var root in new[]{orb,impact})
            {
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.HideAndDontSave;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,Rig.gameObject.scene);
            }
            visual=orb.GetComponent<FlareVolumeVisual>();visual.preset=draft;visual.Initialize();
            impact.GetComponent<JcLuminaPartPresetBinder>().preset=impactDraft;
            var burst=impact.GetComponent<FlareVolumeBurst>();if(burst)burst.preset=draft;
            time=0;
        }
        void Clear(){playing=false;if(orb)DestroyImmediate(orb);if(impact)DestroyImmediate(impact);time=0;}
        void UpdatePreview()
        {
            // 사용자가 플레이 중 조절할 때도 원본 에셋 대신 임시 초안을 보여줍니다.
            if(EditorApplication.isPlaying && draft)
            {
                foreach(var f in FindObjectsOfType<FlareVolumeVisual>())if(f.preset==draftSource)f.preset=draft;
                foreach(var f in FindObjectsOfType<FlareVolumeBurst>())if(f.preset==draftSource)f.preset=draft;
                foreach(var b in FindObjectsOfType<JcLuminaPartPresetBinder>())if(b.preset==draftSource.impact||b.preset==impactDraft){b.preset=impactDraft;b.ApplyNow();}
            }
            if(!playing||!draft)return;
            if(EditorApplication.isPlaying){Clear();return;}
            float dt=Mathf.Min(.04f,(float)(EditorApplication.timeSinceStartup-previous));previous=EditorApplication.timeSinceStartup;
            if(!orb){Clear();return;}
            var data=Skill?Skill.GetProjectileVisual():null;
            if(data==null){Clear();return;}
            float summon=TimelineHoldSeconds();
            Vector3 previewStart=Rig.transform.position,previewEnd=previewStart+Rig.transform.right*Rig.previewDistance;
            Vector3 timingEnd=data.PreserveRootFlightTime?previewEnd-data.TargetOffset:previewEnd;
            float baseFlight=Vector3.Distance(previewStart,timingEnd)/Mathf.Max(.01f,data.Speed);
            float flight=baseFlight*ASB.Work.Battle.Sequence.ProjectileAcceleration.DurationScale(data.Acceleration,data.AccelerationMultiplier);
            float before=time;time+=dt;
            Vector3 start=Rig.transform.position,end=start+Rig.transform.right*Rig.previewDistance;
            if(time<summon)
            {
                float u=draft.startAtApex?1:1-Mathf.Pow(1-Mathf.Clamp01(time/draft.RiseSeconds),3);orb.transform.position=start-Vector3.up*draft.riseHeight*(1-u);
                orb.transform.localScale=Vector3.one*Mathf.Lerp(draft.startScale,1,u);impact.SetActive(false);
                visual.SummonPose(time/Mathf.Max(.01f,draft.summonSeconds));
            }
            else if(time<summon+flight)
            {
                orb.transform.localScale=Vector3.one;if(before<summon)visual.Begin(1);
                float u=(time-summon)/Mathf.Max(.001f,flight);
                var trajectory=ASB.Work.Battle.Sequence.ProjectileTrajectoryFactory.Create(data.Trajectory);
                trajectory.Init(start,end,data);trajectory.Step(time-summon,null,out var position);
                Vector3 last=orb.transform.position;orb.transform.position=position;visual.Pose(orb.transform.position-last,u);
            }
            else
            {
                if(before<summon+flight)visual.End();impact.SetActive(true);impact.transform.position=end;
                impact.GetComponent<JcLuminaPartPresetBinder>().ApplyNow();
                float t=time-summon-flight;
                impact.GetComponent<FlareVolumeBurst>()?.Sample(t/Mathf.Max(.01f,impactDraft.burstDuration));
                foreach(var r in impact.GetComponentsInChildren<Renderer>(true))
                {
                    bool ring=r.name.Contains("Ring");var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);
                    b.SetFloat("_Progress",Mathf.Clamp01(t/Mathf.Max(.01f,ring?impactDraft.ringDuration:impactDraft.burstDuration)));r.SetPropertyBlock(b);
                    if(ring)r.transform.position=end-Vector3.up*impactDraft.ringGroundOffset;
                }
                if(t>Mathf.Max(impactDraft.burstDuration,impactDraft.ringDuration)+.6f){Clear();Create();playing=true;}
            }
            visual.Tick(dt);SceneView.RepaintAll();
        }
    }
}
