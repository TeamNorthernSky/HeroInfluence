using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using JC.BattleTesting.Vfx;

namespace JC.BattleTesting.Editor
{
    // 대응: 기존 HeroSkill 프리셋·VFX 카탈로그 저작 및 JC 씬 배치. ASB/공용 에셋에는 쓰지 않습니다.
    public static class JcYuliaVfxSetup
    {
        private const string Root="Assets/_ProtoType_Merge/JC/BattleTesting/Vfx";
        public static string Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||EditorApplication.isCompiling||EditorUtility.scriptCompilationFailed||scene.isDirty||scene.path!=JcBattleTestSceneBuilder.Destination)
                throw new InvalidOperationException("저장된 JC 씬의 Edit Mode가 필요합니다.");
            var session=UnityEngine.Object.FindFirstObjectByType<JcBattleTestSession>();
            var shader=Shader.Find("JC/BattleTesting/YuliaEnergy");if(shader==null)throw new InvalidOperationException("JC 에너지 셰이더 없음");
            var energy=AssetDatabase.LoadAssetAtPath<Material>(Root+"/JC_YuliaEnergy.mat");
            if(energy==null){energy=new Material(shader);energy.SetColor("_Color",Color.white);AssetDatabase.CreateAsset(energy,Root+"/JC_YuliaEnergy.mat");}
            var smoke=AssetDatabase.LoadAssetAtPath<Material>(Root+"/JC_YuliaSmoke.mat");
            if(smoke==null){smoke=new Material(shader);smoke.SetFloat("_Mode",2);smoke.SetFloat("_DstBlend",10);smoke.SetColor("_Color",Color.white);AssetDatabase.CreateAsset(smoke,Root+"/JC_YuliaSmoke.mat");}
            var frame=AssetDatabase.LoadAssetAtPath<Material>(Root+"/JC_DespairWheelFrame.mat");
            if(frame==null){frame=new Material(Shader.Find("Universal Render Pipeline/Lit"));frame.SetColor("_BaseColor",new Color(.065f,.045f,.075f));frame.SetFloat("_Metallic",.75f);frame.SetFloat("_Smoothness",.6f);AssetDatabase.CreateAsset(frame,Root+"/JC_DespairWheelFrame.mat");}
            var preset=AssetDatabase.LoadAssetAtPath<JcYuliaVfxPreset>(Root+"/JC_YuliaVfxPreset.asset");
            if(preset==null){preset=ScriptableObject.CreateInstance<JcYuliaVfxPreset>();preset.energyMaterial=energy;preset.smokeMaterial=smoke;preset.frameMaterial=frame;AssetDatabase.CreateAsset(preset,Root+"/JC_YuliaVfxPreset.asset");}
            var director=session.GetComponent<JcYuliaVfxDirector>()??session.gameObject.AddComponent<JcYuliaVfxDirector>();
            director.preset=preset;
            var visual=UnityEngine.Object.FindFirstObjectByType<BattleVisualDirector>();var so=new SerializedObject(visual);
            var registry=(EffectRegistry)so.FindProperty("_effectRegistry").objectReferenceValue;
            director.storm=registry.Get(400011);director.beam=registry.Get(400012);session.bossVfx=director;
            if(director.storm==null||director.beam==null)throw new InvalidOperationException("기존 폭풍/궤적 VFX 없음");
            EditorUtility.SetDirty(session);EditorUtility.SetDirty(director);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "JC 상태 VFX 연결 완료 / 기존 히어로·율리아 공격 카탈로그 읽기 참조";
        }
        public static string SaveCandidate(int rank=6,int skillLevel=2,int weaponLevel=3)
        {
            if(Application.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Edit Mode 필요");
            var session=UnityEngine.Object.FindFirstObjectByType<JcBattleTestSession>();
            var profile=AssetDatabase.LoadAssetAtPath<JcBattleTestProfile>(Root+"/JC_YuliaFullPhase_Playable.asset");
            if(profile==null){profile=ScriptableObject.CreateInstance<JcBattleTestProfile>();AssetDatabase.CreateAsset(profile,Root+"/JC_YuliaFullPhase_Playable.asset");}
            var settings=JsonUtility.FromJson<JcBattleTestSettings>(JsonUtility.ToJson(session.settings));
            foreach(var ally in settings.allies){ally.level=rank;ally.weaponLevel=weaponLevel;ally.attackTraining=1;ally.healthTraining=3;ally.skillLevels=new[]{skillLevel,skillLevel,skillLevel,skillLevel};ally.hpRatio=1;ally.initialIP=100;}
            settings.enemyPreset=AssetDatabase.LoadAssetAtPath<JcEnemyPreset>("Assets/_ProtoType_Merge/JC/BattleTesting/JC_YuliaFullEncounter.asset");
            settings.alliesCannotDie=false;settings.useFixedSeed=true;settings.seed=1006;
            profile.settings=settings;session.settings=JsonUtility.FromJson<JcBattleTestSettings>(JsonUtility.ToJson(settings));session.profile=profile;
            if(!session.Validate(out var error))throw new InvalidOperationException(error);
            EditorUtility.SetDirty(profile);EditorUtility.SetDirty(session);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(session.gameObject.scene);EditorSceneManager.SaveScene(session.gameObject.scene);
            return AssetDatabase.GetAssetPath(profile)+" / rank="+rank+" / skill="+skillLevel+" / weapon="+weaponLevel;
        }
    }
}
