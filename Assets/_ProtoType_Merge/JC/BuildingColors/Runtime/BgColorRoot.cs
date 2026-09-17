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
        public Renderer[] TargetRenderers {
            get {
                if(cachedRenderers==null||Time.realtimeSinceStartupAsDouble>=nextScan){
                    if(targetRoot)cachedRenderers=targetRoot.GetComponentsInChildren<Renderer>(true);
                    else{var list=new List<Renderer>();foreach(var g in gameObject.scene.GetRootGameObjects())list.AddRange(g.GetComponentsInChildren<Renderer>(true));cachedRenderers=list.ToArray();}
                    nextScan=Time.realtimeSinceStartupAsDouble+2;
                }
                return cachedRenderers;
            }
        }
        public void RefreshTargets(){cachedRenderers=null;foreach(var c in Children){c.InvalidatePreview();c.Rebind();c.ApplyNow();}}
    }
}
