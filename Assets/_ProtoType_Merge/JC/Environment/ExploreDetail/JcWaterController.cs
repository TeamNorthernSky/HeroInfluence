using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace JC.EnvironmentDetail
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class JcWaterController : MonoBehaviour
    {
        [SerializeField, Tooltip("명시적으로 저장하고 불러올 물 설정입니다. 플레이 중 조절은 이 에셋을 직접 변경하지 않습니다.")]
        JcWaterProfile profile;
        [SerializeField, Tooltip("현재 미리보기 값입니다. 저장 버튼을 눌러야 재로드 후에도 유지됩니다.")]
        JcWaterSettings settings = new JcWaterSettings();
        [SerializeField, Tooltip("물의 모든 애니메이션을 일시 정지합니다. 저장 프로필에는 포함되지 않습니다.")]
        bool paused;
        [SerializeField, HideInInspector] bool initialized;
        readonly Dictionary<Material,Material> materials = new Dictionary<Material,Material>();
        readonly Dictionary<Renderer,Material[]> originals = new Dictionary<Renderer,Material[]>();
        readonly Dictionary<Renderer,MaterialPropertyBlock> originalBlocks = new Dictionary<Renderer,MaterialPropertyBlock>();
        readonly List<ReflectionView> reflectionViews = new List<ReflectionView>();
        MaterialPropertyBlock reflectionBlock;
        static bool renderingReflection;
        sealed class ReflectionView
        {
            public Camera source, camera;
            public RenderTexture texture;
            public float height;
            public Matrix4x4 vp;
            public int ticks;
            public double lastUsed;
            public bool valid;
        }
        public int ReflectionViewCount => reflectionViews.Count;
        JcWaterUnderwater underwater;
        readonly Dictionary<UniversalAdditionalCameraData,CameraOverrideOption> depthOverrides=new Dictionary<UniversalAdditionalCameraData,CameraOverrideOption>();
        public JcWaterUnderwater Underwater => underwater;
        float waterTime;
        bool applyPending;
        double previousTime;
        public JcWaterSettings Settings => settings;
        public JcWaterProfile Profile => profile;
        public int MaterialCount => materials.Count;
        public int RendererCount => originals.Count;
        public bool Paused { get => paused; set => paused=value; }
        public static JcWaterController Ensure(JcExploreDetailOverlay overlay)
        {
            var parent=overlay.transform.parent!=null?overlay.transform.parent:overlay.transform;
            var water=parent.GetComponentInChildren<JcWaterController>(true);
            if(water==null)
            {
                var go=new GameObject("JC_Water"){hideFlags=HideFlags.DontSave};
                go.transform.SetParent(parent,false); water=go.AddComponent<JcWaterController>();
            }
            water.Initialize(); return water;
        }
        void Initialize()
        {
            if(profile==null) profile=Resources.Load<JcWaterProfile>("JC_Water_Default");
            if(initialized)return;
            if(profile!=null)settings=profile.settings.Copy();
            initialized=true;
        }
        void OnEnable(){ Initialize(); previousTime=Now(); Rebind(); RenderPipelineManager.beginCameraRendering+=RenderReflections; }
        // OnValidate에서는 카메라/RT를 파괴하지 않고 안전한 편집기 업데이트로 넘깁니다.
        void OnValidate(){ if(settings==null)settings=new JcWaterSettings(); applyPending=true; }
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=RenderReflections;Unbind();}
        void OnDestroy(){RenderPipelineManager.beginCameraRendering-=RenderReflections;Unbind();}
        public void Rebind()
        {
            var parent=transform.parent;
            var overlay=parent!=null?parent.GetComponentInChildren<JcExploreDetailOverlay>(true):null;
            Bind(overlay!=null?overlay.VisualRoot:null);
        }
        public void Bind(GameObject root)
        {
            Initialize();
            if(!isActiveAndEnabled || root==null){Unbind();return;}
            var nextMaterials=new Dictionary<Material,Material>();
            var nextOriginals=new Dictionary<Renderer,Material[]>();
            var nextSlots=new Dictionary<Renderer,Material[]>();
            var rollback=new Dictionary<Renderer,Material[]>();
            try
            {
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var source=originals.TryGetValue(renderer,out var original)?original:renderer.sharedMaterials;
                    var slots=(Material[])source.Clone();bool changed=false;
                    for(int i=0;i<slots.Length;i++)
                    {
                        var material=slots[i];
                        if(material==null || material.shader==null || material.shader.name!="JC/Environment/Calm Canal Water")continue;
                        if(!nextMaterials.TryGetValue(material,out var live))
                        {
                            live=new Material(material){name=material.name+" (물 실시간)",hideFlags=HideFlags.HideAndDontSave};nextMaterials.Add(material,live);
                            var constrained=JcWaterFlowConstraint.Create(root,material);
                            if(constrained!=null)live.SetTexture("_FlowMap",constrained);
                            live.SetFloat("_StreakWaterMask",constrained!=null?1:0);
                            settings.Apply(live);live.SetFloat("_UseWaterTime",1);live.SetFloat("_WaterTime",waterTime);
                        }
                        slots[i]=live;changed=true;
                    }
                    if(changed){nextOriginals.Add(renderer,source);nextSlots.Add(renderer,slots);rollback.Add(renderer,renderer.sharedMaterials);}
                }
                foreach(var pair in nextSlots)pair.Key.sharedMaterials=pair.Value;
            }
            catch
            {
                foreach(var pair in rollback)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
                foreach(var m in nextMaterials.Values)ReleaseMaterial(m);
                throw;
            }
            // 모든 신규 재질과 슬롯이 준비된 다음에만 이전 연결을 해제합니다.
            underwater?.Dispose();underwater=null;
            ClearReflections();
            RestoreBlocks();
            foreach(var pair in originals)if(pair.Key!=null && !nextOriginals.ContainsKey(pair.Key))pair.Key.sharedMaterials=pair.Value;
            foreach(var m in materials.Values)ReleaseMaterial(m);
            originals.Clear();materials.Clear();
            foreach(var pair in nextOriginals)originals.Add(pair.Key,pair.Value);
            foreach(var pair in nextMaterials)materials.Add(pair.Key,pair.Value);
            foreach(var renderer in originals.Keys)
            {
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);originalBlocks.Add(renderer,block);
            }
            underwater=new JcWaterUnderwater(transform,originals.Keys);underwater.Apply(settings);underwater.Tick(settings,0);
        }
        static void ReleaseMaterial(Material m){
            if(m==null)return;
            var flow=m.GetTexture("_FlowMap");
            if(flow!=null && flow.name=="JC constrained canal flow" && (flow.hideFlags & HideFlags.DontSave)!=0)ReleaseObject(flow);
            ReleaseObject(m);
        }
        public bool Owns(Material material)=>materials.ContainsValue(material);
        public void Unbind()
        {
            underwater?.Dispose();underwater=null;
            foreach(var pair in depthOverrides)if(pair.Key!=null&&pair.Key.requiresDepthOption==CameraOverrideOption.On)pair.Key.requiresDepthOption=pair.Value;
            depthOverrides.Clear();
            ClearReflections();RestoreBlocks();
            foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
            originals.Clear();
            foreach(var m in materials.Values)ReleaseMaterial(m);
            materials.Clear();
        }
        public void Apply()
        {
            applyPending=false;
            if(settings==null)return;
            foreach(var m in materials.Values)if(m!=null){settings.Apply(m);m.SetFloat("_UseWaterTime",1);m.SetFloat("_WaterTime",waterTime);}
            underwater?.Apply(settings);underwater?.Tick(settings,Application.isPlaying?waterTime:0);
            // 변경 직후에는 이전 해상도/마스크로 촬영한 영상이 남지 않게 합니다.
            ClearReflections();
            foreach(var renderer in originals.Keys)if(renderer!=null)SetReflection(renderer,null);
        }
        void RestoreBlocks()
        {
            foreach(var pair in originalBlocks)if(pair.Key!=null)pair.Key.SetPropertyBlock(pair.Value.isEmpty?null:pair.Value);
            originalBlocks.Clear();
        }
        static void ReleaseObject(Object value)
        {if(value!=null){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}}
        void ReleaseView(ReflectionView view)
        {
            if(view.camera!=null)view.camera.targetTexture=null;
            if(view.texture!=null){view.texture.Release();ReleaseObject(view.texture);}
            if(view.camera!=null)ReleaseObject(view.camera.gameObject);
        }
        void ClearReflections()
        {
            foreach(var view in reflectionViews)ReleaseView(view);
            reflectionViews.Clear();
        }
        void SetReflection(Renderer renderer,ReflectionView view)
        {
            if(reflectionBlock==null)reflectionBlock=new MaterialPropertyBlock();
            renderer.GetPropertyBlock(reflectionBlock);
            reflectionBlock.SetFloat("_PlanarAvailable",view!=null && view.valid?1:0);
            reflectionBlock.SetTexture("_PlanarReflection",view!=null && view.valid?(Texture)view.texture:Texture2D.blackTexture);
            reflectionBlock.SetMatrix("_PlanarVP",view!=null?view.vp:Matrix4x4.identity);
            renderer.SetPropertyBlock(reflectionBlock);
        }
        void RenderReflections(ScriptableRenderContext context,Camera source)
        {
            if(renderingReflection || source==null || !isActiveAndEnabled)return;
            if(source.cameraType!=CameraType.Game && source.cameraType!=CameraType.SceneView)return;
            if(source.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType==CameraRenderType.Overlay)return;
            var cameraData=source.GetUniversalAdditionalCameraData();
            if(!depthOverrides.ContainsKey(cameraData))depthOverrides.Add(cameraData,cameraData.requiresDepthOption);
            cameraData.requiresDepthOption=CameraOverrideOption.On;
            double now=Now();
            for(int i=reflectionViews.Count-1;i>=0;i--)
                if(reflectionViews[i].source==null || now-reflectionViews[i].lastUsed>10)
                {ReleaseView(reflectionViews[i]);reflectionViews.RemoveAt(i);}
            bool enabled=settings.planarReflection && settings.reflection>.001f;
            var planes=GeometryUtility.CalculateFrustumPlanes(source);
            var rendered=new HashSet<ReflectionView>();
            foreach(var renderer in originals.Keys)
            {
                if(renderer==null)continue;
                SetReflection(renderer,null);
                if(!enabled || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes,renderer.bounds))continue;
                // 수면은 수평 메시이며, 1cm 안의 높이를 같은 반사면으로 묶습니다.
                float height=WaterHeight(renderer);
                if(source.transform.position.y<=height+.01f)continue;
                var view=reflectionViews.Find(v=>v.source==source && Mathf.Abs(v.height-height)<.01f);
                if(view==null){view=new ReflectionView{source=source,height=height};reflectionViews.Add(view);}
                view.lastUsed=now;
                if(rendered.Add(view))
                {
                    int longest=Mathf.Clamp((int)settings.reflectionResolution,256,1024);
                    float aspect=Mathf.Max(.1f,source.aspect);
                    int width=Mathf.Max(16,Mathf.RoundToInt(longest*Mathf.Min(1,aspect)));
                    int heightPixels=Mathf.Max(16,Mathf.RoundToInt(longest/Mathf.Max(1,aspect)));
                    if(view.texture==null || view.texture.width!=width || view.texture.height!=heightPixels)
                    {
                        ReleaseView(view);view.valid=false;
                        view.texture=new RenderTexture(width,heightPixels,24,RenderTextureFormat.DefaultHDR)
                        {name="JC Water Reflection",hideFlags=HideFlags.HideAndDontSave,useMipMap=true,autoGenerateMips=true,filterMode=FilterMode.Trilinear};
                        view.texture.Create();
                        var go=new GameObject("JC Water Reflection Camera"){hideFlags=HideFlags.HideAndDontSave};
                        view.camera=go.AddComponent<Camera>();view.camera.enabled=false;
                    }
                    if(!view.valid || view.ticks++%Mathf.Clamp(settings.reflectionUpdateInterval,1,8)==0)
                        CaptureReflection(context,source,view);
                }
                SetReflection(renderer,view);
            }
        }
        float WaterHeight(Renderer renderer)
        {
            var filter=renderer.GetComponent<MeshFilter>();
            if(filter!=null && filter.sharedMesh!=null && originals.TryGetValue(renderer,out var slots))
                for(int i=0;i<slots.Length && i<filter.sharedMesh.subMeshCount;i++)
                    if(slots[i]!=null && slots[i].shader.name=="JC/Environment/Calm Canal Water")
                        return renderer.localToWorldMatrix.MultiplyPoint(filter.sharedMesh.GetSubMesh(i).bounds.center).y;
            return renderer.bounds.center.y;
        }
        void CaptureReflection(ScriptableRenderContext context,Camera source,ReflectionView view)
        {
            var camera=view.camera;camera.CopyFrom(source);camera.enabled=false;
            camera.cameraType=CameraType.Reflection;camera.targetTexture=view.texture;
            camera.rect=new Rect(0,0,1,1);camera.useOcclusionCulling=false;
            camera.cullingMask=settings.reflectionLayers.value;camera.allowMSAA=false;
            var data=camera.GetUniversalAdditionalCameraData();
            data.renderType=CameraRenderType.Base;data.renderPostProcessing=false;
            data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
            camera.clearFlags=source.clearFlags==CameraClearFlags.Skybox?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;
            // 카메라별 Skybox 오버라이드도 반영합니다. 없으면 RenderSettings.skybox를 사용합니다.
            var sky=camera.GetComponent<Skybox>();if(sky==null)sky=camera.gameObject.AddComponent<Skybox>();
            var sourceSky=source.GetComponent<Skybox>();sky.enabled=sourceSky!=null && sourceSky.enabled;
            sky.material=sky.enabled?sourceSky.material:null;
            var mirror=Matrix4x4.identity;mirror.m11=-1;mirror.m13=2*view.height;
            camera.transform.SetPositionAndRotation(mirror.MultiplyPoint(source.transform.position),Quaternion.LookRotation(mirror.MultiplyVector(source.transform.forward),mirror.MultiplyVector(source.transform.up)));
            camera.worldToCameraMatrix=source.worldToCameraMatrix*mirror;
            camera.projectionMatrix=source.projectionMatrix;
            Vector3 point=camera.worldToCameraMatrix.MultiplyPoint(new Vector3(0,view.height+.03f,0));
            Vector3 normal=camera.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
            camera.projectionMatrix=camera.CalculateObliqueMatrix(new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal)));
            view.vp=GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)*camera.worldToCameraMatrix;
            bool oldCulling=GL.invertCulling;
            float oldWaterPass=Shader.GetGlobalFloat("_JcWaterReflectionPass");
            try
            {
                renderingReflection=true;
                // 물·호안이 같은 렌더러에 들어갈 수 있으므로 수면 셰이더만 제외합니다.
                Shader.SetGlobalFloat("_JcWaterReflectionPass",1);
                GL.invertCulling=!oldCulling;
                // URP 14: 컨텍스트 내부 반사 패스에는 즉시 단일 카메라 렌더를 사용합니다.
#pragma warning disable CS0618
                UniversalRenderPipeline.RenderSingleCamera(context,camera);
#pragma warning restore CS0618
                view.valid=true;
            }
            finally
            {
                GL.invertCulling=oldCulling;renderingReflection=false;
                Shader.SetGlobalFloat("_JcWaterReflectionPass",oldWaterPass);
            }
        }
        public void LoadSaved(){ Initialize(); if(profile!=null)settings=profile.settings.Copy(); Apply(); }
        static double Now()
        {
#if UNITY_EDITOR
            if(!Application.isPlaying)return UnityEditor.EditorApplication.timeSinceStartup;
#endif
            return Time.timeAsDouble;
        }
        void Update()
        {
            if(applyPending)Apply();
            double now=Now(); float delta=(float)System.Math.Max(0,now-previousTime); previousTime=now;
            if(!paused)waterTime+=delta;
            underwater?.Tick(settings,Application.isPlaying?waterTime:0);
            foreach(var m in materials.Values)if(m!=null)m.SetFloat("_WaterTime",waterTime);
        }
    }
}
