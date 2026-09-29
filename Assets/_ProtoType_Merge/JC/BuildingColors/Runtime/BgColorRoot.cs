using UnityEngine;
using System.Collections.Generic;
namespace JC.BuildingColors
{
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class BgColorRoot : MonoBehaviour
    {
        [Tooltip("전체 건물의 색상값을 한 묶음으로 저장하고 불러올 프로필입니다. 개별 프로필과 독립적입니다.")] public BgColorSetProfile profile;
        [Tooltip("적용할 건물들의 공통 부모입니다. 비우면 같은 씬 전체에서 원본 재질이 일치하는 건물을 찾습니다. 다른 씬에는 적용하지 않습니다.")] public Transform targetRoot;
        public BgColorModifier[] Children=>GetComponentsInChildren<BgColorModifier>(true);
        Renderer[] cachedRenderers;
        double nextScan;
        Transform cachedScope;
        bool cachedPlaying;
        void OnEnable() => InvalidateTargets();
        void OnDisable() => InvalidateTargets();
        void OnDestroy() => InvalidateTargets();
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
                    nextScan=Time.realtimeSinceStartupAsDouble+2;
                    cachedScope=targetRoot;cachedPlaying=Application.IsPlaying(gameObject);
                }
                return cachedRenderers;
            }
        }
        public void RefreshTargets(){InvalidateTargets();foreach(var c in Children){c.InvalidatePreview();c.Rebind();c.ApplyNow();}}
    }
}
