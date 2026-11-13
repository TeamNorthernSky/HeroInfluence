using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JC.BattleTesting
{
    public static partial class JcDynamicBattleGeometry
    {
        // JC | 2026-11-13 | 튜토리얼 전투만 횡단보도 뒤의 정상 단면을 원경까지 연장합니다. 탐사 원본과 일반전은 바꾸지 않습니다.
        static void RepairTutorialRoads(GameObject root)
        {
            var output = root.GetComponent<JcDynamicBattleOutput>();
            var filters = root.GetComponentsInChildren<MeshFilter>();
            // JC | 2026-11-13 | 해당 후방 도로가 포함된 튜토리얼 배경에만 적용합니다. 다른 조우 위치의 배경 생성은 유지합니다.
            var extensions = filters.Where(f => f.name == "RoadEast").ToArray();
            if (extensions.Length == 0) return;
            var extension = extensions.Single();
            var bounds = extension.GetComponent<Renderer>().bounds;
            var reference = filters.Where(f => f.name.StartsWith("Detail_TileChunk_", StringComparison.Ordinal) && f.name.Contains("_Road_01"))
                .Where(f => f.GetComponent<Renderer>().bounds.max.x >= bounds.min.x && f.GetComponent<Renderer>().bounds.min.x <= bounds.max.x)
                .OrderBy(f => (f.GetComponent<Renderer>().bounds.ClosestPoint(bounds.center) - bounds.ClosestPoint(f.GetComponent<Renderer>().bounds.center)).sqrMagnitude).First();
            var road = reference.GetComponent<Renderer>().bounds;
            float start = road.center.z + Mathf.Min(road.size.x,road.size.z)*.1f; // 횡단보도 이후이며 타일·연석 줄눈 한가운데는 피합니다.
            const float end = 2048f; // 카메라 원거리 클립(1000) 밖까지 이어지며 일반 배경 절단 범위를 따르지 않습니다.
            var left = filters.Single(f => f.name.StartsWith("Detail_TileChunk_", StringComparison.Ordinal) && f.name.Contains("tilemap_01_RaisedCube") &&
                f.GetComponent<Renderer>().bounds.min.z < start && f.GetComponent<Renderer>().bounds.max.z > start &&
                Mathf.Abs(f.GetComponent<Renderer>().bounds.max.x - road.min.x) < .01f);
            var right = filters.Single(f => f.name == "SouthPavement");
            var curbs = filters.Single(f => f.name == "Road_Pavement_Grass_Curbs");
            float curbWidth = right.GetComponent<Renderer>().bounds.min.x - road.max.x;
            var corridor = Rect.MinMaxRect(left.GetComponent<Renderer>().bounds.min.x, start,
                right.GetComponent<Renderer>().bounds.max.x + curbWidth, end + 1f);
            // 절단 전에 정상 단면의 재질·법선·UV를 확보합니다. 도로와 인도 높이를 각각 그대로 유지합니다.
            ExtendTutorialSection(output, reference, "Road", start, end, road.min.x, road.max.x, road.max.y);
            ExtendTutorialSection(output, left, "LeftPavement", start, end, corridor.xMin, road.min.x, road.max.y);
            ExtendTutorialSection(output, right, "RightPavement", start, end, road.max.x, corridor.xMax, road.max.y);
            ExtendTutorialSection(output, curbs, "Curbs", start, end, road.max.x, corridor.xMax, road.max.y);
            // 이전 도로·잔디·흙띠·횡단 연석을 모두 제거합니다. 건물은 지형 목록에 포함되지 않아 보존됩니다.
            foreach (var f in filters)
                if (output.TerrainRenderers.Contains(f.GetComponent<Renderer>()) || f.name == "JC_DistantFieldGround" ||
                    f.name == "Band_Ground" || f.name.StartsWith("도로 풀밭", StringComparison.Ordinal))
                    RemoveInsideRoad(f, corridor);
            var dressing = root.GetComponent<JcBattleSurfaceDressing>();
            if (!dressing) throw new InvalidOperationException("튜토리얼 후방 중앙선의 기존 도색 설정이 없습니다.");
            dressing.ExtendTutorialRearLine(road.center.x, start, end);
        }

        // JC | 2026-11-13 | 정상 도로를 가로지르는 단면을 길게 압출합니다. UV 기울기를 보존하여 먼 구간에서도 텍스처가 늘어나지 않습니다.
        static void ExtendTutorialSection(JcDynamicBattleOutput output, MeshFilter source, string label,
            float start, float end, float minX, float maxX, float minY)
        {
            var mesh = source.sharedMesh;
            var positions = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var colors = mesh.colors;
            var data = new List<Vector4>(); mesh.GetUVs(1, data);
            var sections = new List<(Vertex a, Vertex b, Vector2 uvStep, Vector4 dataStep, int sub)>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var face = new Vertex[3];
                    for (int j = 0; j < 3; j++)
                    {
                        int k = triangles[i+j];
                        face[j] = new Vertex { p = source.transform.TransformPoint(positions[k]), n = source.transform.TransformDirection(normals[k]),
                            uv = uv[k], data = data.Count == positions.Length ? data[k] : Vector4.zero, color = colors.Length == positions.Length ? colors[k] : Color.white };
                    }
                    var e1 = face[1].p-face[0].p; var e2 = face[2].p-face[0].p;
                    var normal = Vector3.Cross(e1,e2).normalized;
                    if (Mathf.Abs(normal.z) > .001f || normal.y < -.5f) continue; // 횡단면·바닥 밑면은 연장하지 않습니다.
                    var cuts = new List<Vertex>();
                    for (int j = 0; j < 3; j++)
                    {
                        var a = face[j]; var b = face[(j+1)%3];
                        if ((a.p.z <= start && b.p.z > start) || (b.p.z <= start && a.p.z > start))
                            cuts.Add(Vertex.Lerp(a,b,(start-a.p.z)/(b.p.z-a.p.z)));
                    }
                    if (cuts.Count != 2) continue;
                    var ca = cuts[0]; var cb = cuts[1];
                    if (!ClipTutorialSection(ref ca, ref cb, 0, minX, true) || !ClipTutorialSection(ref ca, ref cb, 0, maxX, false) ||
                        !ClipTutorialSection(ref ca, ref cb, 1, minY, true) || (ca.p-cb.p).sqrMagnitude < .000001f) continue;
                    float d11=Vector3.Dot(e1,e1), d12=Vector3.Dot(e1,e2), d22=Vector3.Dot(e2,e2), det=d11*d22-d12*d12;
                    if (Mathf.Abs(det) < .000001f) continue;
                    float u=(e1.z*d22-e2.z*d12)/det, v=(e2.z*d11-e1.z*d12)/det;
                    sections.Add((ca,cb,(face[1].uv-face[0].uv)*u+(face[2].uv-face[0].uv)*v,
                        (face[1].data-face[0].data)*u+(face[2].data-face[0].data)*v,sub));
                }
            }
            if (sections.Count == 0) throw new InvalidOperationException("튜토리얼 후방 단면을 찾을 수 없습니다: " + source.name);
            // 원경은 단순 면 묶음으로 생성합니다. 프레임마다 복사하거나 탐사 오브젝트를 검색하지 않습니다.
            for (float z = start; z < end; z += 256f)
            {
                float far = Mathf.Min(end,z+256f);
                var p = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<Vector2>(); var d = new List<Vector4>(); var c = new List<Color>();
                var subs = Enumerable.Range(0,mesh.subMeshCount).Select(_ => new List<int>()).ToArray();
                foreach (var section in sections)
                {
                    int offset = p.Count;
                    foreach (var point in new[] { section.a, section.b, section.b, section.a })
                    {
                        float dz = (p.Count-offset < 2 ? z : far)-start;
                        p.Add(point.p+Vector3.forward*dz); n.Add(point.n); t.Add(point.uv+section.uvStep*dz); d.Add(point.data+section.dataStep*dz); c.Add(point.color);
                    }
                    bool forward = Vector3.Dot(Vector3.Cross(p[offset+1]-p[offset],p[offset+2]-p[offset]),n[offset]+n[offset+1]) > 0;
                    var order = forward ? new[] {0,1,2,0,2,3} : new[] {0,2,1,0,3,2};
                    foreach (int k in order) subs[section.sub].Add(offset+k);
                }
                var generated = new Mesh { name = "Tutorial rear " + label };
                generated.SetVertices(p); generated.SetNormals(n); generated.SetUVs(0,t);
                if(data.Count>0)generated.SetUVs(1,d); if(colors.Length>0)generated.SetColors(c);
                generated.subMeshCount=subs.Length;
                for(int sub=0;sub<subs.Length;sub++)generated.SetTriangles(subs[sub],sub);
                generated.RecalculateBounds(); generated.RecalculateTangents();
                var renderer = output.Add("JC_TutorialRear_"+label,generated,source.GetComponent<MeshRenderer>().sharedMaterials,true);
                output.RepeatTutorialSurface(renderer);
            }
        }

        // JC | 2026-11-13 | 도로 단면의 경계를 잘라 인도/연석이 이웃 영역과 중복되지 않도록 합니다.
        static bool ClipTutorialSection(ref Vertex a, ref Vertex b, int axis, float boundary, bool greater)
        {
            float da = a.p[axis]-boundary, db = b.p[axis]-boundary;
            // JC | 2026-11-13 | 회전·확대의 미세한 부동소수점 오차로 경계 위 수직 연석 면이 탈락하지 않도록 좌표를 일치시킵니다.
            if (Mathf.Abs(da)<.001f) { a.p[axis]=boundary; da=0; }
            if (Mathf.Abs(db)<.001f) { b.p[axis]=boundary; db=0; }
            bool keepA = greater ? da >= 0 : da <= 0, keepB = greater ? db >= 0 : db <= 0;
            if (!keepA && !keepB) return false;
            if (keepA != keepB) { var cut = Vertex.Lerp(a,b,da/(da-db)); if (keepA) b=cut; else a=cut; }
            return true;
        }

        // JC | 2026-11-13 | 결합 메시 전체를 숨기지 않고 도로 직사각형 내부만 삼각형 단위로 절단합니다. 법선·UV·색상은 보간 보존합니다.
        static void RemoveInsideRoad(MeshFilter filter,Rect road)
        {
            var mesh=filter.sharedMesh;if(!mesh)return;
            var renderer=filter.GetComponent<Renderer>();var bounds=renderer.bounds;
            if(bounds.max.x<road.xMin||bounds.min.x>road.xMax||bounds.max.z<road.yMin||bounds.min.z>road.yMax)return;
            var v=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;var color=mesh.colors;var uv1=new List<Vector4>();mesh.GetUVs(1,uv1);
            var points=new List<Vector3>();var normals=new List<Vector3>();var tex=new List<Vector2>();var data=new List<Vector4>();var colors=new List<Color>();
            var subs=new List<int>[mesh.subMeshCount];bool changed=false;
            for(int sub=0;sub<subs.Length;sub++)
            {
                subs[sub]=new List<int>();var triangles=mesh.GetTriangles(sub);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var polygon=new List<Vertex>();
                    for(int j=0;j<3;j++){int k=triangles[i+j];polygon.Add(new Vertex{p=filter.transform.TransformPoint(v[k]),n=n.Length==v.Length?n[k]:Vector3.up,uv=uv.Length==v.Length?uv[k]:Vector2.zero,data=uv1.Count==v.Length?uv1[k]:Vector4.zero,color=color.Length==v.Length?color[k]:Color.white});}
                    var remaining=polygon;var pieces=new List<List<Vertex>>();
                    for(int side=0;side<4;side++)
                    {
                        int axis=side<2?0:2;float boundary=side==0?road.xMin:side==1?road.xMax:side==2?road.yMin:road.yMax;bool greater=(side%2)==0;
                        var outside=Clip(remaining,axis,boundary,!greater);if(outside.Count>=3)pieces.Add(outside);
                        remaining=Clip(remaining,axis,boundary,greater);
                    }
                    if(remaining.Count>=3)changed=true;
                    foreach(var piece in pieces){int start=points.Count;foreach(var p in piece){points.Add(filter.transform.InverseTransformPoint(p.p));normals.Add(p.n);tex.Add(p.uv);data.Add(p.data);colors.Add(p.color);}for(int j=1;j<piece.Count-1;j++){subs[sub].Add(start);subs[sub].Add(start+j);subs[sub].Add(start+j+1);}}
                }
            }
            if(!changed)return;
            mesh.Clear();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(points);mesh.SetNormals(normals);mesh.SetUVs(0,tex);if(uv1.Count>0)mesh.SetUVs(1,data);if(color.Length>0)mesh.SetColors(colors);
            mesh.subMeshCount=subs.Length;for(int sub=0;sub<subs.Length;sub++)mesh.SetTriangles(subs[sub],sub);
            mesh.RecalculateBounds();mesh.RecalculateTangents();
        }
    }
}
