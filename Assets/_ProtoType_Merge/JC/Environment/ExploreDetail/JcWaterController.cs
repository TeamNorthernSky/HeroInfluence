using System.Collections.Generic;
using UnityEngine;
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
        float waterTime;
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
        void OnEnable(){ Initialize(); previousTime=Now(); Rebind(); }
        void OnValidate(){ if(settings==null)settings=new JcWaterSettings(); Apply(); }
        void OnDisable()=>Unbind();
        void OnDestroy()=>Unbind();
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
            foreach(var pair in originals)if(pair.Key!=null && !nextOriginals.ContainsKey(pair.Key))pair.Key.sharedMaterials=pair.Value;
            foreach(var m in materials.Values)ReleaseMaterial(m);
            originals.Clear();materials.Clear();
            foreach(var pair in nextOriginals)originals.Add(pair.Key,pair.Value);
            foreach(var pair in nextMaterials)materials.Add(pair.Key,pair.Value);
        }
        static void ReleaseMaterial(Material m){if(m==null)return;if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}
        public bool Owns(Material material)=>materials.ContainsValue(material);
        public void Unbind()
        {
            foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
            originals.Clear();
            foreach(var m in materials.Values)ReleaseMaterial(m);
            materials.Clear();
        }
        public void Apply()
        {
            if(settings==null)return;
            foreach(var m in materials.Values)if(m!=null){settings.Apply(m);m.SetFloat("_UseWaterTime",1);m.SetFloat("_WaterTime",waterTime);}
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
            double now=Now(); float delta=(float)System.Math.Max(0,now-previousTime); previousTime=now;
            if(!paused)waterTime+=delta;
            foreach(var m in materials.Values)if(m!=null)m.SetFloat("_WaterTime",waterTime);
        }
    }
}
