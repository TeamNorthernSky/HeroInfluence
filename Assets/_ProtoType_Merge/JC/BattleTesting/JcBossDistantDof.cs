using System.Collections.Generic;
using JC.Env;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting
{
    // JC | 2026-11-17 | 보스 원경만 Gaussian으로 흐립니다. 기존 DOF 적용기·프로필을 재사용하고
    // 실내/참가자의 가장 먼 카메라 깊이보다 뒤로 시작점을 밀어 스킬 카메라에서도 선명도를 보존합니다.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class JcBossDistantDof : MonoBehaviour
    {
        [SerializeField, Tooltip("본게임 보스와 보스 테스트가 공유하는 Gaussian DOF 저장 프로필입니다.")]
        JcDofProfile profile;
        public JcDofProfile Source { get => profile; set { profile = value; ReloadDraft(); } }
        public JcDofProfile Draft { get; private set; }
        public float ProtectedDepth { get; private set; }
        public float ActualStart => driver ? driver.CurrentGaussianStart : 0;
        public float ActualEnd => driver ? driver.CurrentGaussianEnd : 0;
        public string Status { get; private set; } = "보스 카메라 대기";
        Transform stage;
        Renderer[] interior;
        readonly HashSet<Renderer> characters = new HashSet<Renderer>();
        BattleFlowManager flow;
        Volume volume;
        JcDofController driver;
        Camera targetCamera;
        double nextFlowLookup;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            ReloadDraft();
        }
        public void ReloadDraft()
        {
            if (Draft) Release(Draft);
            Draft = profile ? Instantiate(profile) : null;
            if (Draft) { Draft.hideFlags = HideFlags.DontSave; Draft.name = profile.name + " (조절 중)"; }
        }
        void LateUpdate()
        {
            if (SceneManager.GetActiveScene() != gameObject.scene || !gameObject.scene.isLoaded) { if(volume) volume.weight=0; return; }
            if (!targetCamera || targetCamera.gameObject.scene != gameObject.scene)
                foreach(var root in gameObject.scene.GetRootGameObjects())
                    foreach(var camera in root.GetComponentsInChildren<Camera>())
                        if(camera.CompareTag("MainCamera")) { targetCamera=camera; break; }
            BindParticipants();
            if(targetCamera) ApplyForCamera(targetCamera);
        }
        void BindParticipants()
        {
            if(flow || !Application.isPlaying || Time.realtimeSinceStartupAsDouble < nextFlowLookup) return;
            nextFlowLookup = Time.realtimeSinceStartupAsDouble + .5;
            foreach(var root in gameObject.scene.GetRootGameObjects())
            {
                flow=root.GetComponentInChildren<BattleFlowManager>();
                if(flow) break;
            }
            if(!flow) return;
            // JC | 2026-11-17 | 매 프레임 전장 전체를 찾지 않고 기존 참가자 등록 이벤트를 읽기 전용으로 구독합니다.
            foreach(var actor in flow.Participants) RegisterActor(actor);
            flow.OnParticipantRegistered += RegisterActor;
        }
        void RegisterActor(BattleCharactor actor)
        {
            if(!actor || actor.gameObject.scene != gameObject.scene) return;
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))
                if(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) characters.Add(renderer);
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            // JC | 2026-11-17 | URP Volume 갱신 직전의 최종 카메라 자세로 계산합니다. SceneView/다른 씬에는 적용하지 않습니다.
            bool active = camera == targetCamera && camera.cameraType == CameraType.Game &&
                SceneManager.GetActiveScene() == gameObject.scene && isActiveAndEnabled;
            if(volume) volume.weight = active ? 1 : 0;
            if(active) ApplyForCamera(camera);
        }
        public bool ApplyForCamera(Camera camera)
        {
            if(!isActiveAndEnabled || !Draft || !camera || camera.gameObject.scene != gameObject.scene ||
                SceneManager.GetActiveScene() != gameObject.scene) return false;
            stage=transform.parent;
            if(!stage) return false;
            if(interior == null)
            {
                var list=new List<Renderer>();
                foreach(var r in stage.GetComponentsInChildren<Renderer>(true))
                    if(!r.GetComponentInParent<JcBossCityTime>() && (r is MeshRenderer || r is SkinnedMeshRenderer)) list.Add(r);
                interior=list.ToArray();
            }
            if(!driver)
            {
                // JC | 2026-11-17 | 원본 Volume·파이프라인은 그대로 두고 이 보스 인스턴스만 임시 Volume을 소유합니다.
                var go=new GameObject("Gaussian DOF · 실행 사본") { hideFlags=HideFlags.DontSave };
                go.transform.SetParent(transform,false);
                volume=go.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=100; volume.weight=1;
                driver=go.AddComponent<JcDofController>(); driver.hideFlags=HideFlags.HideInInspector;
                driver.TargetVolume=volume;
            }
            targetCamera=camera;
            float depth=0;
            foreach(var r in interior) if(r && r.enabled && r.gameObject.activeInHierarchy) depth=Mathf.Max(depth,FarthestDepth(r.bounds,camera));
            foreach(var r in characters) if(r && r.enabled && r.gameObject.activeInHierarchy) depth=Mathf.Max(depth,FarthestDepth(r.bounds,camera));
            ProtectedDepth=depth;
            var settings=Draft.settings.Sanitized();
            float width=Mathf.Max(.01f,settings.gaussianEnd-settings.gaussianStart);
            settings.mode=DepthOfFieldMode.Gaussian; settings.autoFocus=false;
            settings.gaussianStart=Mathf.Max(settings.gaussianStart,depth+3);
            settings.gaussianEnd=settings.gaussianStart+width;
            driver.TargetCamera=camera; driver.Settings=settings; volume.weight=1;
            var data=camera.GetUniversalAdditionalCameraData();
            Status=data.renderPostProcessing ? "적용 중 · 실내/캐릭터 뒤부터 Gaussian 원경 블러" : "카메라 Post Processing이 꺼져 있습니다.";
            return data.renderPostProcessing;
        }
        // JC | 2026-11-17 | 월드 AABB의 카메라 전방 투영 최댓값입니다. 카메라 회전·책상 이동·캐릭터 스킬 이동을 반영합니다.
        public static float FarthestDepth(Bounds bounds, Camera camera)
        {
            var f=camera.transform.forward; var e=bounds.extents;
            return Vector3.Dot(bounds.center-camera.transform.position,f)+Mathf.Abs(f.x)*e.x+Mathf.Abs(f.y)*e.y+Mathf.Abs(f.z)*e.z;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            if(flow) flow.OnParticipantRegistered -= RegisterActor;
            flow=null; characters.Clear(); interior=null;
            if(driver) Release(driver.gameObject);
            driver=null; volume=null;
            if(Draft) Release(Draft); Draft=null;
        }
        static void Release(Object value) { if(Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
