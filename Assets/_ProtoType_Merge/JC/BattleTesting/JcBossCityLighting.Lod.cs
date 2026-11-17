using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.BattleTesting
{
    public sealed partial class JcBossCityLighting
    {
        // JC | 2026-11-17 | 원본 배치/편집 메시를 유지하고 보스 표시 수명 동안만 구역별 LOD를 구성합니다.
        readonly List<GameObject> lodObjects = new();
        readonly Dictionary<Renderer,bool> lodSources = new();
        // JC | 2026-11-17 | 생성된 그룹과 메시 교체를 추적하여 조절 즉시 반영하고 종료 시 복원합니다.
        readonly List<LODGroup> cityLodGroups = new();
        readonly Dictionary<MeshFilter,Mesh> previewMeshSources = new();
        public JcBossBattleBackground OptimizationSource => optimization;
        public JcBossCityLodSettings LodDraft { get; private set; }
        // JC | 2026-11-17 | 같은 프로필의 에디트/플레이 조절기가 동일 초안을 미리봅니다.
        public void SetLodDraft(JcBossCityLodSettings settings) { LodDraft=settings.Copy();ApplyLodDraft(); }
        public void ApplyLodDraft()
        {
            if(LodDraft==null)return;
            LodDraft.Validate();
            foreach(var group in cityLodGroups)if(group) {
                var levels=group.GetLODs();
                levels[0].screenRelativeTransitionHeight=LodDraft.highScreenHeight;
                levels[1].screenRelativeTransitionHeight=LodDraft.mediumScreenHeight;
                group.SetLODs(levels);group.RecalculateBounds();
            }
        }
        public void SetLodMeshPreview(Dictionary<Mesh,Mesh> replacements)
        {
            foreach(var pair in previewMeshSources)if(pair.Key)pair.Key.sharedMesh=pair.Value;
            previewMeshSources.Clear();
            if(replacements!=null)foreach(var go in lodObjects)if(go && go.TryGetComponent<MeshFilter>(out var filter) &&
                filter.sharedMesh && replacements.TryGetValue(filter.sharedMesh,out var preview)) {
                previewMeshSources.Add(filter,filter.sharedMesh);filter.sharedMesh=preview;
            }
            ApplyLodDraft();
        }
        sealed class LodBlock { public readonly List<Renderer>[] levels = {new(),new(),new()}; }
        void BuildCityLods(Transform city)
        {
            var library=optimization.cityLodLibrary;
            if(!library) return;
            var lookup=new Dictionary<Mesh,JcBossCityLodLibrary.Entry>();
            foreach(var entry in library.entries) if(entry.source) lookup[entry.source]=entry;
            var state=city.gameObject.AddComponent<JcBossCityLodState>();state.hideFlags=HideFlags.HideAndDontSave;
            var blocks=new Dictionary<Vector2Int,LodBlock>();
            foreach(var filter in city.GetComponentsInChildren<MeshFilter>(true)) {
                var source=filter.GetComponent<MeshRenderer>();
                if(!source || !source.enabled || !source.gameObject.activeInHierarchy || source.GetComponentInParent<LODGroup>() || !filter.sharedMesh || !lookup.TryGetValue(filter.sharedMesh,out var entry)) continue;
                lodSources.Add(source,source.enabled);state.sources.Add(new JcBossCityLodState.Source{renderer=source,enabled=source.enabled});
                bool split=entry.parts.Length>1;
                if(split) source.enabled=false;
                foreach(var part in entry.parts) {
                    var center=city.InverseTransformPoint(filter.transform.TransformPoint(part.high.bounds.center));
                    var key=new Vector2Int(Mathf.FloorToInt(center.x/12),Mathf.FloorToInt(center.z/12));
                    if(!blocks.TryGetValue(key,out var block)) blocks.Add(key,block=new LodBlock());
                    var meshes=new[]{part.high,part.medium,part.low};
                    for(int level=0;level<3;level++) {
                        if(level==0 && !split) { block.levels[level].Add(source); continue; }
                        var go=new GameObject("JC 원경 LOD "+level){hideFlags=HideFlags.HideAndDontSave,layer=source.gameObject.layer};
                        go.transform.SetParent(filter.transform,false);lodObjects.Add(go);state.generated.Add(go);
                        go.AddComponent<MeshFilter>().sharedMesh=meshes[level];
                        var renderer=go.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials=source.sharedMaterials;
                        renderer.shadowCastingMode=source.shadowCastingMode;renderer.receiveShadows=source.receiveShadows;
                        renderer.lightProbeUsage=source.lightProbeUsage;renderer.reflectionProbeUsage=source.reflectionProbeUsage;
                        renderer.renderingLayerMask=source.renderingLayerMask;
                        block.levels[level].Add(renderer);
                    }
                }
            }
            foreach(var block in blocks.Values) {
                var go=new GameObject("JC 원경 구역 LOD"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(city,false);lodObjects.Add(go);state.generated.Add(go);
                var group=go.AddComponent<LODGroup>();
                // JC | 2026-11-17 | 구역이 화면에서 작아질 때 단계별 단순화하며 최원경도 윤곽은 유지합니다.
                group.SetLODs(new[]{new LOD(LodDraft.highScreenHeight,block.levels[0].ToArray()),new LOD(LodDraft.mediumScreenHeight,block.levels[1].ToArray()),new LOD(0,block.levels[2].ToArray())});
                cityLodGroups.Add(group);
                group.RecalculateBounds();
            }
        }
        void ReleaseCityLods()
        {
            // JC | 2026-11-17 | 미저장 메시 초안을 원본 에셋 참조로 복원한 뒤 임시 그룹을 제거합니다.
            SetLodMeshPreview(null);cityLodGroups.Clear();
            // JC | 2026-11-17 | 시작 후 복제한 도시도 현재 무대에서 찾아 임시 LOD와 원본 상태를 함께 복원합니다.
            foreach(var city in JcBossCityTime.Roots(stageRoot))if(city)
                foreach(var state in city.GetComponents<JcBossCityLodState>())if(state)state.Restore();
            foreach(var go in lodObjects) if(go) { go.SetActive(false);Release(go); }
            lodObjects.Clear();
            foreach(var pair in lodSources) if(pair.Key) pair.Key.enabled=pair.Value;
            lodSources.Clear();
        }
    }
}
