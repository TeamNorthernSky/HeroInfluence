using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using JC.VFX.EditorTools;

namespace JC.VFX
{
    /// <summary>프리셋 적용/캡처는 실제 연결된 부품을 우선 사용합니다. 옛 프리뷰 전용 경로는 폴백입니다.</summary>
    public static class JcPresetPartTargets
    {
        public static bool References(Component component,Object preset)
        {
            if(component==null)return false;
            var it=new SerializedObject(component).GetIterator();
            while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue==preset)return true;
            return false;
        }
        public static GameObject[] Roots(Object preset)
        {
            return HeroEffectUsage.CurrentPrefabs()
                .OrderByDescending(g=>AssetDatabase.GetAssetPath(g).StartsWith("Assets/RenderFX/HeroSkill/"))
                .Where(g=>g.GetComponentsInChildren<MonoBehaviour>(true).Any(c=>References(c,preset))).ToArray();
        }
        public static string[] Paths(Object preset,string[] fallback)
        {
            var roots=Roots(preset);
            return roots.Length>0?roots.Select(AssetDatabase.GetAssetPath).Distinct().ToArray():fallback;
        }
        public static string Path(Object preset,string fallback)
        {
            fallback=HeroEffectPaths.Resolve(fallback);
            var source=HeroEffectPaths.Load<GameObject>(fallback);
            if(source==null){var current=Roots(preset).FirstOrDefault();return current!=null?AssetDatabase.GetAssetPath(current):fallback;}
            var types=source.GetComponents<MonoBehaviour>().Where(c=>c!=null).Select(c=>c.GetType()).ToArray();
            var root=Roots(preset).FirstOrDefault(g=>types.Any(t=>g.GetComponent(t)!=null));
            return root!=null?AssetDatabase.GetAssetPath(root):fallback;
        }
        public static T Resolve<T>(Object preset,string path)where T:Object
        {
            if(typeof(T)==typeof(GameObject))return HeroEffectPaths.Load<T>(Path(preset,path));
            var original=HeroEffectPaths.Load<T>(path);
            if(!(original is Material))return original;
            foreach(var root in Roots(preset))foreach(var c in root.GetComponentsInChildren<Component>(true))
            {
                if(c==null)continue;var it=new SerializedObject(c).GetIterator();
                while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue is Material mat
                    && (mat==original||mat.name.EndsWith("_"+original.name)))return mat as T;
            }
            return original;
        }
    }
}
