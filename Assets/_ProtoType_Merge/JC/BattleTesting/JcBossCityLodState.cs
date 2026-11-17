using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-17 | 에디트 미리보기 중 도시를 복제해도 원본 활성 상태와 임시 자식 참조가 함께 복제됩니다.
    public sealed class JcBossCityLodState : MonoBehaviour
    {
        [Serializable] public struct Source { public Renderer renderer; public bool enabled; }
        public List<Source> sources=new();
        public List<GameObject> generated=new();
        public void Restore()
        {
            foreach(var source in sources)if(source.renderer)source.renderer.enabled=source.enabled;
            foreach(var go in generated)if(go){go.SetActive(false);Release(go);}
            sources.Clear();generated.Clear();Release(this);
        }
        static void Release(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
