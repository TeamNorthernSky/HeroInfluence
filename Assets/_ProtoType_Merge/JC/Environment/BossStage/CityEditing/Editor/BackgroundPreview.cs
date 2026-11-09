// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using JC.BattleTesting;
namespace JC.BackgroundEditing
{
    public sealed partial class BackgroundWindow
    {
        Material allyMaterial,enemyMaterial;Scene previewScene;Camera previewCamera;GameObject previewStage,previewHUD;RenderTexture previewTexture;
        void DisposePreview(){if(allyMaterial)DestroyImmediate(allyMaterial);if(enemyMaterial)DestroyImmediate(enemyMaterial);if(previewScene.IsValid())EditorSceneManager.CloseScene(previewScene,true);previewScene=default;if(previewTexture){previewTexture.Release();DestroyImmediate(previewTexture);}previewTexture=null;previewCamera=null;previewStage=null;previewHUD=null;}
        void OnInspectorUpdate()
        {
            if(!document||!autoPreview||EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<nextPreview)return;nextPreview=EditorApplication.timeSinceStartup+1;
            int hash=viewMode*397+(showHUD?1:0)+document.hour.GetHashCode()+document.stageScale.GetHashCode()+document.stageOffset.GetHashCode()+document.cityAltitude.GetHashCode()+(document.overrideCityAltitude?1:0)+(document.foregroundFloor?document.foregroundFloor.GetInstanceID():0);unchecked{foreach(var f in document.GetComponentsInChildren<MeshFilter>()){hash=hash*31+f.transform.localToWorldMatrix.GetHashCode();hash=hash*31+f.sharedMesh.GetInstanceID();foreach(var m in f.GetComponent<Renderer>().sharedMaterials)hash=hash*31+(m?m.GetInstanceID():0);}}
            if(hash!=previewHash||!previewTexture){Run(()=>RefreshPreview(true));previewHash=hash;}
        }
        void RefreshPreview(bool geometry)
        {
            BackgroundOps.Guard();if(!document)return;
            if(!previewScene.IsValid())
            {
                // JC | 2026-11-05 | 미저장 작업 씬과 공존할 수 있는 전용 빈 템플릿을 열고 저장하지 않고 닫습니다.
                var active=SceneManager.GetActiveScene();previewScene=EditorSceneManager.OpenScene(BackgroundOps.Local+"/Editor/PreviewTemplate.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(active);previewTexture=new RenderTexture(1280,720,24);previewTexture.Create();var cg=new GameObject("배경 미리보기 카메라");SceneManager.MoveGameObjectToScene(cg,previewScene);previewCamera=cg.AddComponent<Camera>();previewCamera.scene=previewScene;previewCamera.targetTexture=previewTexture;previewCamera.aspect=16f/9;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.5f,.64f,.73f);
allyMaterial=new Material(Shader.Find("JC/Battle/SoftCharacter"));allyMaterial.SetColor("_BaseColor",new Color(.14f,.65f,.8f));enemyMaterial=new Material(allyMaterial);enemyMaterial.SetColor("_BaseColor",new Color(.65f,.25f,.3f));
                // JC | 2026-11-05 | 미리보기 유닛도 실내와 같은 로컬 조명만 받습니다.
                allyMaterial.SetFloat("_BossLocalOnly",1);enemyMaterial.SetFloat("_BossLocalOnly",1);
                foreach(float z in new[]{-6f,6f})foreach(float x in new[]{-3.3f,0,3.3f}){var unit=GameObject.CreatePrimitive(PrimitiveType.Capsule);SceneManager.MoveGameObjectToScene(unit,previewScene);unit.transform.position=new Vector3(x,1.3f,z);unit.transform.localScale=Vector3.one;unit.GetComponent<Renderer>().sharedMaterial=z<0?allyMaterial:enemyMaterial;}
                LoadHUD();
            }
            if(geometry||!previewStage)
            {
                if(previewStage)DestroyImmediate(previewStage);previewStage=Instantiate(document.stage);SceneManager.MoveGameObjectToScene(previewStage,previewScene);
                var old=previewStage.transform.Find("도시 원경 (베이크)");if(!old)throw new Exception("도시 원경 기준 없음");var p=old.localPosition;var q=old.localRotation;var s=old.localScale;DestroyImmediate(old.gameObject);
                var city=BackgroundOps.Child(previewStage.transform,"도시 원경 (베이크)");BackgroundOps.CopyCityVisuals(document,city.transform);city.transform.localPosition=p;city.transform.localRotation=q;city.transform.localScale=s;city.AddComponent<JcBossCityTime>().worldLighting=true;BackgroundOps.ApplyStageFraming(document,previewStage);
            }
            // JC | 2026-11-05 | 미리보기 구도도 일반 전투가 아닌 현재 보스전 저장 프로필을 읽습니다.
            var profile=JcBossBattleBackground.Load().cameraProfile;var shot=viewMode==1?profile.viewB:profile.viewA;shot.Resolve(new Vector3(0,.3f,0),out var cp,out var cr,out float cf);previewCamera.transform.SetPositionAndRotation(cp,cr);previewCamera.fieldOfView=cf;if(viewMode==2){previewCamera.fieldOfView*=1.35f;previewCamera.transform.position+=previewCamera.transform.up*2;}
            if(previewHUD)previewHUD.SetActive(showHUD);Canvas.ForceUpdateCanvases();RenderWorldPreview();Repaint();
        }
        // JC | 2026-11-05 | 고정 미리보기 조명 대신 실제 보스전과 같은 월드 시간대·실내 설정으로 렌더합니다.
        void RenderWorldPreview()
        {
            var previous=SceneManager.GetActiveScene();
            var settings=JcBossBattleBackground.Load();
            if(!settings||!settings.citySun)throw new InvalidOperationException("원경 월드 조명 설정이 없습니다.");
            var controls=new GameObject("원경 미리보기 환경");SceneManager.MoveGameObjectToScene(controls,previewScene);
            JcBossCityLighting city=null;JcBossBackgroundMaterials materials=null;JcBossEnvironment environment=null;
            try{
                SceneManager.SetActiveScene(previewScene);
                var mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(previewScene,mask);previewCamera.overrideSceneCullingMask=mask;
                city=controls.AddComponent<JcBossCityLighting>();city.Initialize(previewStage.transform,settings,document.hour);
                materials=controls.AddComponent<JcBossBackgroundMaterials>();materials.Initialize(previewStage,settings.glassProfile,previewCamera,settings.surfaceProfile);
                environment=controls.AddComponent<JcBossEnvironment>();environment.Initialize(settings.environmentProfile,previewStage.transform);
                materials.Environment=environment;environment.SurfaceController=materials;materials.ApplySurfaces();
                previewCamera.Render();
            }finally{
                if(environment){environment.SendMessage("OnDisable");environment.SendMessage("OnDestroy");}
                if(materials)materials.SendMessage("OnDestroy");
                if(city)city.SendMessage("OnDestroy");
                DestroyImmediate(controls);
                var clock=previewScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<JC.Environment.JcDayNightController>(true)).FirstOrDefault();
                if(clock)DestroyImmediate(clock.gameObject);
                SceneManager.SetActiveScene(previous);
            }
        }
        void LoadHUD()
        {
            const string path="Assets/_ProtoType_Merge/Scenes/TmpBattleScene.unity";var battle=SceneManager.GetSceneByPath(path);bool opened=!battle.IsValid()||!battle.isLoaded;
            try{if(opened)battle=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);var canvas=battle.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).First(c=>c.name=="BattleSceneCanvas");previewHUD=Instantiate(canvas.gameObject);SceneManager.MoveGameObjectToScene(previewHUD,previewScene);previewHUD.name="BattleSceneCanvas";
                foreach(var b in previewHUD.GetComponentsInChildren<MonoBehaviour>(true)){var ns=b.GetType().Namespace??"";if(!ns.StartsWith("UnityEngine.UI")&&!ns.StartsWith("TMPro"))b.enabled=false;}
                foreach(Transform t in previewHUD.transform)t.gameObject.SetActive(t.name=="BottomPanel"||t.name=="TopRightButtons"||t.name=="TurnOrderPanel");Resources.Load<JcBattleHudLayout>("JC_BattleHud_Layout")?.Apply(previewScene);
                foreach(var c in previewHUD.GetComponentsInChildren<Canvas>(true)){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=previewCamera;c.planeDistance=1;}var scaler=previewHUD.GetComponent<CanvasScaler>();if(scaler){scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;}
            }finally{if(opened&&battle.IsValid()&&battle.isLoaded)EditorSceneManager.CloseScene(battle,true);}
        }
    }
}


