using System;
using System.Linq;
using UnityEngine;
namespace JC.EnvironmentDetail
{
    public sealed class JcExploreDetailBakeCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string scenePath;
            public JcExploreDetailBake current;
            public string previousAssetPath;
            public string buildInputWarning;
        }
        [Tooltip("씬별 확정본과 직전 확정본입니다. 편집기 베이크 도구가 갱신합니다.")]
        public Entry[] scenes = Array.Empty<Entry>();
        public JcExploreDetailBake Find(string scenePath) => scenes.FirstOrDefault(x => x.scenePath == scenePath)?.current;
        public static JcExploreDetailBakeCatalog Load() => Resources.Load<JcExploreDetailBakeCatalog>("JC_ExploreDetail_Bakes");
    }
}
