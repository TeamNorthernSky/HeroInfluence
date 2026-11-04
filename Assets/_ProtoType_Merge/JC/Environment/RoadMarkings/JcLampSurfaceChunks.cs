using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.EnvironmentDetail
{
    // 원본 렌더러와 필터는 베이크·충돌·전투 캡처의 입력으로 유지합니다.
    internal sealed class JcLampSurfaceChunks : MonoBehaviour
    {
        internal const string VisualName = "JC_LampSurface";
        internal const float CellSize = 3f;
        static readonly Vector2Int Far = new Vector2Int(int.MinValue, int.MinValue);
        LODGroup visibility;
        MeshRenderer source;
        readonly List<MeshRenderer> pieces = new List<MeshRenderer>();
        readonly List<Mesh> meshes = new List<Mesh>();
        MaterialPropertyBlock block;

        internal static void Build(MeshFilter filter, List<Light> lights)
        {
            var source = filter.GetComponent<MeshRenderer>();
            if (!source || !source.enabled || source.forceRenderingOff || source.GetComponentInParent<LODGroup>() ||
                filter.GetComponent<JcLampSurfaceChunks>()) return;
            var bounds = source.bounds;
            if (Mathf.Max(bounds.size.x, bounds.size.z) <= CellSize) return;
            var nearby = lights.FindAll(l => bounds.SqrDistance(l.transform.position) <= l.range * l.range);
            if (nearby.Count == 0) return;
            var mesh = JC.BattleTesting.JcBattleSurfaceDressing.ReadableRoadMesh(filter.sharedMesh);
            if (!mesh || !mesh.isReadable) return;
            var part = filter.gameObject.AddComponent<JcLampSurfaceChunks>();
            part.source = source;
            try { part.Split(mesh, nearby); }
            catch
            {
                Destroy(part);
                throw;
            }
        }

        void Split(Mesh mesh, List<Light> lights)
        {
            var positions = mesh.vertices;
            var groups = new Dictionary<Vector2Int,List<int>[]>();
            var cellKeys = new Dictionary<Vector2Int,Vector2Int>();
            for (int sub = 0; sub < mesh.subMeshCount; ++sub)
            {
                var indices = mesh.GetTriangles(sub);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var center = transform.TransformPoint((positions[indices[i]] + positions[indices[i+1]] + positions[indices[i+2]]) / 3f);
                    var cell = new Vector2Int(Mathf.FloorToInt(center.x / CellSize), Mathf.FloorToInt(center.z / CellSize));
                    if (!cellKeys.TryGetValue(cell, out var key))
                    {
                        // 타일 가장자리·연석도 포함하도록 타일 반 폭 이상 여유를 둡니다.
                        var area = new Rect(cell.x * CellSize - 1, cell.y * CellSize - 1, CellSize + 2, CellSize + 2);
                        key = lights.Exists(light => {
                            var p = light.transform.position;
                            float dx = p.x - Mathf.Clamp(p.x, area.xMin, area.xMax);
                            float dz = p.z - Mathf.Clamp(p.z, area.yMin, area.yMax);
                            return dx*dx + dz*dz <= light.range * light.range;
                        }) ? cell : Far;
                        cellKeys.Add(cell,key);
                    }
                    if (!groups.TryGetValue(key, out var slots)) groups.Add(key, slots = new List<int>[mesh.subMeshCount]);
                    var tris = slots[sub] ?? (slots[sub] = new List<int>());
                    tris.Add(indices[i]); tris.Add(indices[i+1]); tris.Add(indices[i+2]);
                }
            }
            if (groups.Count < 2) return;
            block = new MaterialPropertyBlock();
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var colors = mesh.colors;
            var uvs = new List<Vector4>[8];
            for (int i=0;i<uvs.Length;i++) { uvs[i]=new List<Vector4>(); mesh.GetUVs(i,uvs[i]); }
            foreach (var slots in groups.Values)
            {
                var ids = new List<int>();
                var remap = new Dictionary<int,int>();
                foreach (var tris in slots)
                {
                    if (tris == null) continue;
                    for (int i=0;i<tris.Count;i++)
                    {
                        int id=tris[i];
                        if (!remap.TryGetValue(id,out int index)) { index=ids.Count; ids.Add(id); remap.Add(id,index); }
                        tris[i]=index;
                    }
                }
                var chunk = new Mesh { name=VisualName, hideFlags=HideFlags.DontSave, indexFormat=mesh.indexFormat };
                meshes.Add(chunk);
                chunk.SetVertices(ids.ConvertAll(id=>positions[id]));
                if (normals.Length>0) chunk.SetNormals(ids.ConvertAll(id=>normals[id]));
                if (tangents.Length>0) chunk.SetTangents(ids.ConvertAll(id=>tangents[id]));
                if (colors.Length>0) chunk.SetColors(ids.ConvertAll(id=>colors[id]));
                for (int i=0;i<uvs.Length;i++) if(uvs[i].Count>0) chunk.SetUVs(i,ids.ConvertAll(id=>uvs[i][id]));
                chunk.subMeshCount=slots.Length;
                for(int sub=0;sub<slots.Length;sub++) if(slots[sub]!=null) chunk.SetTriangles(slots[sub],sub,false);
                chunk.RecalculateBounds();
                var go = new GameObject(VisualName) { layer=gameObject.layer, hideFlags=HideFlags.DontSave };
                go.transform.SetParent(transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=chunk;
                var r=go.AddComponent<MeshRenderer>();
                r.shadowCastingMode=source.shadowCastingMode; r.receiveShadows=source.receiveShadows;
                r.renderingLayerMask=source.renderingLayerMask; r.lightProbeUsage=source.lightProbeUsage;
                r.reflectionProbeUsage=source.reflectionProbeUsage; r.probeAnchor=source.probeAnchor;
                r.lightmapIndex=source.lightmapIndex; r.lightmapScaleOffset=source.lightmapScaleOffset;
                pieces.Add(r);
            }
            SyncAppearance();
            visibility=gameObject.AddComponent<LODGroup>();
            visibility.SetLODs(new[]{ new LOD(.5f,new Renderer[]{source}), new LOD(0,pieces.ToArray()) });
            visibility.RecalculateBounds();
            visibility.ForceLOD(1);
        }

        internal void SyncAppearance()
        {
            if (pieces.Count==0) return;
            var mats=source.sharedMaterials;
            source.GetPropertyBlock(block);
            foreach(var r in pieces) { r.sharedMaterials=mats; r.SetPropertyBlock(block); }
            for(int i=0;i<mats.Length;i++)
            {
                source.GetPropertyBlock(block,i);
                foreach(var r in pieces) r.SetPropertyBlock(block.isEmpty?null:block,i);
            }
        }

        internal void SyncVisibility()
        {
            bool visible = source.enabled && !source.forceRenderingOff;
            foreach (var r in pieces)
                if (r.enabled != visible) r.enabled = visible;
        }

        void OnDestroy()
        {
            if(visibility) { visibility.ForceLOD(0); Destroy(visibility); }
            foreach(var r in pieces) if(r) { r.enabled=false; Destroy(r.gameObject); }
            foreach(var m in meshes) if(m) Destroy(m);
        }
    }
}
