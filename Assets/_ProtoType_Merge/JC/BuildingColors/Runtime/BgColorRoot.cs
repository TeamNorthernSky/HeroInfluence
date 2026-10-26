using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace JC.BuildingColors
{
    [Serializable]
    public sealed class BgFixedColorVariant
    {
        [Tooltip("고정할 건물 종류 ID입니다.")] public string buildingId;
        [Tooltip("DH 배치 기준점 또는 표시용 건물의 월드 위치입니다.")] public Vector3 anchor;
        [Tooltip("같은 종류의 색상 바리에이션 ID입니다.")] public string variantId;
    }

    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class BgColorRoot : MonoBehaviour
    {
        [Tooltip("기본 색상과 건물 종류별 바리에이션을 함께 저장하는 프로필입니다.")] public BgColorSetProfile profile;
        [Tooltip("대상 부모입니다. 비우면 같은 씬 전체에서 일치하는 건물을 찾습니다.")] public Transform targetRoot;
        [Tooltip("프로필의 바리에이션을 건물별로 배분합니다. 건물 위치와 레벨 데이터는 변경하지 않습니다.")] public bool distributeVariants;
        [Tooltip("같은 씬·배치 기준점·건물 종류·시드이면 항상 같은 색상을 선택합니다.")] public int variantSeed=260926;
        [Tooltip("지정된 건물은 무작위 배분보다 우선합니다. 튜토리얼의 기존 8채 보존에 사용합니다.")] public BgFixedColorVariant[] fixedVariants=Array.Empty<BgFixedColorVariant>();
        public BgColorModifier[] Children=>GetComponentsInChildren<BgColorModifier>(true);
        Renderer[] cachedRenderers;
        double nextScan,nextProfileCheck;
        Transform cachedScope;
        bool cachedPlaying,refreshRequested;
        int appliedProfileHash=int.MinValue;
        GameObject generatedRoot;
        DecorativeObjectRegistry registry;
        readonly Dictionary<string,List<BgColorSetEntry>> variants=new Dictionary<string,List<BgColorSetEntry>>();
        readonly HashSet<string> supportedTypes=new HashSet<string>();
        public int GeneratedVariantCount=>generatedRoot?generatedRoot.GetComponentsInChildren<BgColorModifier>(true).Length:0;

        void OnEnable(){InvalidateTargets();appliedProfileHash=int.MinValue;refreshRequested=true;}
        void OnValidate(){appliedProfileHash=int.MinValue;refreshRequested=true;}
        void OnDisable(){Unsubscribe();RemoveGenerated();InvalidateTargets();}
        void OnDestroy(){Unsubscribe();RemoveGenerated();}
        void Update()
        {
            if(Time.realtimeSinceStartupAsDouble>=nextProfileCheck){
                BindRegistry();int hash=ProfileHash();
                if(hash!=appliedProfileHash){RebuildVariants();appliedProfileHash=hash;refreshRequested=true;}
                nextProfileCheck=Time.realtimeSinceStartupAsDouble+1;
            }
            if(refreshRequested){refreshRequested=false;RefreshBindings();}
        }
        void BindRegistry()
        {
            if(registry)return;
            var scene=gameObject.scene;if(!scene.IsValid()||!scene.isLoaded)return;
            foreach(var g in scene.GetRootGameObjects()){registry=g.GetComponentInChildren<DecorativeObjectRegistry>(true);if(registry)break;}
            if(!registry)return;
            registry.DecorativeObjectRegistered+=Changed;
            registry.DecorativeObjectUnregistered+=Changed;
        }
        void Unsubscribe(){if(registry){registry.DecorativeObjectRegistered-=Changed;registry.DecorativeObjectUnregistered-=Changed;}registry=null;}
        void Changed(DecorativeObjectPlacement placement){InvalidateTargets();refreshRequested=true;}
        void InvalidateTargets(){cachedRenderers=null;cachedScope=null;nextScan=0;}
        bool CacheInvalid(){
            if(cachedRenderers==null||cachedScope!=targetRoot||cachedPlaying!=Application.IsPlaying(gameObject))return true;
            foreach(var renderer in cachedRenderers)if(!renderer)return true;
            return false;
        }
        public Renderer[] TargetRenderers {
            get {
                if(CacheInvalid()||Time.realtimeSinceStartupAsDouble>=nextScan){
                    if(targetRoot)cachedRenderers=targetRoot.GetComponentsInChildren<Renderer>(true);
                    else{var list=new List<Renderer>();var scene=gameObject.scene;if(scene.IsValid()&&scene.isLoaded)foreach(var g in scene.GetRootGameObjects())list.AddRange(g.GetComponentsInChildren<Renderer>(true));cachedRenderers=list.ToArray();}
                    nextScan=Time.realtimeSinceStartupAsDouble+2;cachedScope=targetRoot;cachedPlaying=Application.IsPlaying(gameObject);
                }
                return cachedRenderers;
            }
        }
        public static Vector3 BuildingAnchor(Renderer renderer)
        {
            var placement=renderer.GetComponentInParent<DecorativeObjectPlacement>();
            return placement?placement.transform.TransformPoint(placement.AnchorLocalOffset):renderer.transform.position;
        }
        // FNV-1a is stable across processes, unlike string.GetHashCode and Unity instance IDs.
        public static uint AssignmentHash(string scene,string buildingId,Vector3 anchor,int seed)
        {
            unchecked {
                uint hash=2166136261;
                foreach(char c in scene+"/"+buildingId){hash=(hash^c)*16777619;}
                foreach(int v in new[]{seed,Mathf.RoundToInt(anchor.x*1000),Mathf.RoundToInt(anchor.z*1000)})hash=(hash^(uint)v)*16777619;
                hash^=hash>>16;hash*=0x7feb352d;hash^=hash>>15;hash*=0x846ca68b;hash^=hash>>16;
                return hash;
            }
        }
        public string SelectVariant(string buildingId,Vector3 anchor)
        {
            if(!distributeVariants||!supportedTypes.Contains(buildingId)||!variants.TryGetValue(buildingId,out var choices))return string.Empty;
            foreach(var item in fixedVariants??Array.Empty<BgFixedColorVariant>()){
                if(item==null||item.buildingId!=buildingId)continue;
                if(Mathf.Abs(item.anchor.x-anchor.x)>.01f||Mathf.Abs(item.anchor.z-anchor.z)>.01f)continue;
                if(choices.Any(e=>e.variantId==item.variantId))return item.variantId;
            }
            double total=choices.Sum(e=>Math.Max(0,e.weight));if(total<=0)return string.Empty;
            double pick=(AssignmentHash(gameObject.scene.name,buildingId,anchor,variantSeed)/4294967296.0)*total;
            foreach(var choice in choices){pick-=Math.Max(0,choice.weight);if(pick<0)return choice.variantId;}
            return choices.Last(e=>e.weight>0).variantId;
        }
        public bool Accepts(BgColorModifier modifier,Renderer renderer)
        {
            string selected=SelectVariant(modifier.definition.buildingId,BuildingAnchor(renderer));
            return selected==(modifier.variantId??string.Empty);
        }
        public void RefreshTargets()
        {
            if(!isActiveAndEnabled)return;
            RebuildVariants();appliedProfileHash=ProfileHash();refreshRequested=false;RefreshBindings();
        }
        void RefreshBindings()
        {
            InvalidateTargets();var children=Children.Where(c=>c.isActiveAndEnabled).ToArray();
            // Release every old owner first so changing a seed cannot restore a stale colour over a new owner.
            foreach(var c in children)c.ResetTargets();
            foreach(var c in children){c.Rebind();c.ApplyNow();}
        }
        int ProfileHash()
        {
            unchecked{
                int h=(profile?profile.GetInstanceID():0)*31+(distributeVariants?1:0);
                foreach(var e in profile?profile.buildings??Array.Empty<BgColorSetEntry>():Array.Empty<BgColorSetEntry>()){
                    if(e==null||string.IsNullOrEmpty(e.variantId))continue;
                    h=h*31+e.Key.GetHashCode();h=h*31+e.weight.GetHashCode();
                    foreach(var id in e.partIds??Array.Empty<string>())h=h*31+(id??string.Empty).GetHashCode();
                    foreach(var p in e.parts??Array.Empty<BgPartColor>()){h=h*31+p.hue.GetHashCode();h=h*31+p.lightness.GetHashCode();h=h*31+p.saturation.GetHashCode();h=h*31+(p.ignoreSourceColorAndShading?1:0);}
                }
                return h;
            }
        }
        void RemoveGenerated()
        {
            variants.Clear();supportedTypes.Clear();
            if(!generatedRoot)return;
            generatedRoot.SetActive(false);generatedRoot.transform.SetParent(null);
            if(Application.IsPlaying(gameObject))Destroy(generatedRoot);else DestroyImmediate(generatedRoot);
            generatedRoot=null;
        }
        void RebuildVariants()
        {
            foreach(var c in Children)c.ResetTargets();
            RemoveGenerated();
            if(!distributeVariants||!profile||profile.buildings==null)return;
            var entries=profile.buildings.Where(e=>e!=null&&!string.IsNullOrEmpty(e.variantId)).ToArray();
            if(entries.Any(e=>string.IsNullOrEmpty(e.buildingId)||!BgColorDefinition.ValidValues(e.parts,e.partIds)||float.IsNaN(e.weight)||float.IsInfinity(e.weight))||entries.Select(e=>e.Key).Distinct().Count()!=entries.Length){Debug.LogError("건물 바리에이션 ID·파츠·비중을 확인하세요.",this);return;}
            var definitions=Children.Where(c=>c.definition&&string.IsNullOrEmpty(c.variantId)&&c.targetRadius<=0).Select(c=>c.definition).Distinct().ToArray();
            if(entries.Length==0)return;
            generatedRoot=new GameObject("색상 바리에이션 미리보기"){hideFlags=HideFlags.DontSave};generatedRoot.transform.SetParent(transform,false);
            foreach(var group in entries.GroupBy(e=>e.buildingId)){
                var definition=definitions.FirstOrDefault(d=>d.buildingId==group.Key);if(!definition)continue;
                variants[group.Key]=group.OrderBy(e=>e.variantId,StringComparer.Ordinal).ToList();supportedTypes.Add(group.Key);
                foreach(var e in variants[group.Key]){
                    var go=new GameObject(string.IsNullOrEmpty(e.displayName)?e.Key:e.displayName){hideFlags=HideFlags.DontSave};go.transform.SetParent(generatedRoot.transform,false);
                    var modifier=go.AddComponent<BgColorModifier>();modifier.definition=definition;modifier.variantId=e.variantId;modifier.parts=definition.Remap(e.parts,e.partIds);modifier.partIds=(string[])definition.Ids.Clone();
                }
            }
        }
    }
}
