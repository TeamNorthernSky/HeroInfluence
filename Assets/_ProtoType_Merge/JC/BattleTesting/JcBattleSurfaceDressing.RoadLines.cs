using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.BattleTesting
{
    public sealed partial class JcBattleSurfaceDressing
    {
        struct RoadFace
        {
            public Vector3 a, b, c;
        }

        sealed class RoadLine
        {
            public Vector3 origin, direction;
            public Material material;
            public float baselineWidth;
            public readonly List<Vector2> paint = new List<Vector2>();
            public readonly List<Vector2> seeds = new List<Vector2>();
        }

        // 튜토리얼의 미리 그려진 점선도 전투 복사본에서만 같은 간격 정책을 적용합니다.
        public static void ApplyAuthoredLineSpacing(GameObject root)
        {
            var profile = Profile;
            if (profile == null || profile.roadShader == null || profile.plainAsphalt == null ||
                root.GetComponent<JcBattleSurfaceDressing>() != null) return;
            var owner = root.AddComponent<JcBattleSurfaceDressing>();
            owner.CaptureAuthoredLines(root.GetComponentsInChildren<MeshFilter>(), profile);
        }

        void CaptureAuthoredLines(MeshFilter[] filters, JcBattleSurfaceProfile profile)
        {
            foreach (var filter in filters)
            {
                if (filter.name != "CenterLane_ThinDense_VisualOnly" && filter.name != "NorthBackStreet_CenterLane") continue;
                var mesh = ReadableRoadMesh(filter.sharedMesh);
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || !mesh.isReadable || renderer == null) continue;
                // 전투 지형 복사는 삼각형마다 정점을 분리할 수 있어 인덱스 형태에 의존하지 않습니다.
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                if (triangles.Length < 6) continue;
                var a = filter.transform.TransformPoint(vertices[triangles[0]]);
                var b = filter.transform.TransformPoint(vertices[triangles[1]]);
                var c = filter.transform.TransformPoint(vertices[triangles[2]]);
                var edges = new[] { b - a, c - b, a - c };
                Array.Sort(edges, (x, y) => x.sqrMagnitude.CompareTo(y.sqrMagnitude));
                var direction = edges[1].normalized;
                if ((Mathf.Abs(direction.x) > Mathf.Abs(direction.z) ? direction.x : direction.z) < 0) direction = -direction;
                var side = Vector3.Cross(Vector3.up, direction);
                float sideMin = float.PositiveInfinity, sideMax = float.NegativeInfinity;
                foreach (var v in vertices)
                {
                    float t = Vector3.Dot(filter.transform.TransformPoint(v), side);
                    sideMin = Mathf.Min(sideMin, t); sideMax = Mathf.Max(sideMax, t);
                }
                var line = new RoadLine { origin = side * ((sideMin + sideMax) * .5f) + Vector3.up * a.y,
                    direction = direction, material = renderer.sharedMaterial };
                var pieces = new List<Vector2>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    float min = float.PositiveInfinity, max = float.NegativeInfinity;
                    for (int j = 0; j < 3; j++)
                    {
                        float t = Vector3.Dot(filter.transform.TransformPoint(vertices[triangles[i + j]]) - line.origin, direction);
                        min = Mathf.Min(min, t); max = Mathf.Max(max, t);
                    }
                    if (!pieces.Exists(p => Mathf.Abs(p.x - min) < .001f && Mathf.Abs(p.y - max) < .001f)) pieces.Add(new Vector2(min, max));
                }
                pieces.Sort((x, y) => x.x.CompareTo(y.x));
                float oldGap = float.PositiveInfinity;
                for (int i = 1; i < pieces.Count; i++)
                    if (pieces[i].x - pieces[i - 1].y > .001f) oldGap = Mathf.Min(oldGap, pieces[i].x - pieces[i - 1].y);
                if (float.IsInfinity(oldGap)) continue;
                var spans = new List<Vector2>();
                foreach (var piece in pieces)
                {
                    if (spans.Count == 0 || piece.x - spans[spans.Count - 1].y > oldGap + .02f) spans.Add(piece);
                    else { var last = spans[spans.Count - 1]; last.y = piece.y; spans[spans.Count - 1] = last; }
                }
                // 원본은 배치 구간만 제공하고, 확대 배율과 무관하게 본게임 전투 규격을 사용합니다.
                line.baselineWidth = sideMax - sideMin;
                line.paint.AddRange(pieces);
                EmitRoadLine(line, spans, profile);
                if (!explorationLines) renderer.enabled = false;
            }
        }

        // 도색이 있는 타일의 UV로 중심축을 구하되, 점선의 길이는 월드 거리로 통일합니다.
        // 무표시 타일만 있는 주차장에는 축을 만들지 않으며 횡단보도/비도로에서 구간이 끊깁니다.
        void BuildConnectedRoadLines(MeshFilter[] filters, JcBattleSurfaceProfile profile)
        {
            if (profile.roadShader == null || profile.plainAsphalt == null) return;
            var faces = new List<RoadFace>();
            var lines = new List<RoadLine>();
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                var mesh = ReadableRoadMesh(filter.sharedMesh);
                if (renderer == null || !renderer.enabled || mesh == null || !mesh.isReadable) continue;
                var vertices = mesh.vertices;
                var uv = mesh.uv;
                var materials = renderer.sharedMaterials;
                for (int sub = 0; sub < Math.Min(materials.Length, mesh.subMeshCount); sub++)
                {
                    string texture = TextureName(materials[sub]);
                    bool marked = texture.StartsWith("Road_Centerline_", StringComparison.Ordinal);
                    if (!marked && texture != "Road_01") continue;
                    var triangles = mesh.GetTriangles(sub);
                    Vector2 normal = default;
                    float offset = 0;
                    Vector3 direction = default;
                    bool canSeed = marked && uv.Length == vertices.Length &&
                        LineFrame(filter, sub, texture, out normal, out offset, out direction, out _);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int ai = triangles[i], bi = triangles[i + 1], ci = triangles[i + 2];
                        var a = filter.transform.TransformPoint(vertices[ai]);
                        var b = filter.transform.TransformPoint(vertices[bi]);
                        var c = filter.transform.TransformPoint(vertices[ci]);
                        if (Vector3.Cross(b - a, c - a).normalized.y < .98f) continue;
                        var face = new RoadFace { a = a, b = b, c = c };
                        faces.Add(face);
                        if (!canSeed) continue;
                        var u = uv[bi] - uv[ai];
                        var v = uv[ci] - uv[ai];
                        float det = u.x * v.y - u.y * v.x;
                        if (Mathf.Abs(det) < .000001f) continue;
                        var du = ((b - a) * v.y - (c - a) * u.y) / det;
                        var dv = ((c - a) * u.x - (b - a) * v.x) / det;
                        // 큐브 옆면이 첫 삼각형인 메시도 실제 도로 윗면의 축을 사용합니다.
                        direction = (du * -normal.y + dv * normal.x).normalized;
                        if ((Mathf.Abs(direction.x) > Mathf.Abs(direction.z) ? direction.x : direction.z) < 0)
                            direction = -direction;
                        var delta = normal * (offset - Vector2.Dot(uv[ai], normal));
                        var origin = a + du * delta.x + dv * delta.y;
                        var candidate = new RoadLine { origin = origin, direction = direction, material = materials[sub] };
                        if (!RoadInterval(face, candidate, out var seed)) continue;
                        var line = lines.Find(x => Vector3.Dot(x.direction, direction) > .9999f &&
                            Mathf.Abs(x.origin.y - origin.y) < .02f &&
                            Vector3.Cross(x.direction, origin - x.origin).magnitude < .02f);
                        if (line == null) { line = candidate; lines.Add(line); }
                        if (explorationLines) CapturePaint(line, materials[sub], uv[ai], a, du, dv, normal);
                        if (RoadInterval(face, line, out seed)) line.seeds.Add(seed);
                    }
                }
            }

            foreach (var line in lines)
            {
                var intervals = new List<Vector2>();
                foreach (var face in faces)
                    if (RoadInterval(face, line, out var interval)) intervals.Add(interval);
                intervals.Sort((a, b) => a.x.CompareTo(b.x));
                var connected = new List<Vector2>();
                foreach (var interval in intervals)
                {
                    if (connected.Count == 0 || interval.x > connected[connected.Count - 1].y + .02f)
                        connected.Add(interval);
                    else
                    {
                        var last = connected[connected.Count - 1];
                        last.y = Mathf.Max(last.y, interval.y);
                        connected[connected.Count - 1] = last;
                    }
                }
                // 같은 직선 위라도 횡단보도 너머의 무표시 도로는 별도 구간입니다.
                connected.RemoveAll(span => !line.seeds.Exists(seed => seed.y > span.x + .001f && seed.x < span.y - .001f));
                if (connected.Count == 0) continue;
                EmitRoadLine(line, connected, profile);
            }
        }

        static bool RoadInterval(RoadFace face, RoadLine line, out Vector2 interval)
        {
            interval = default;
            if (Mathf.Abs(face.a.y - line.origin.y) > .05f || Mathf.Abs(face.b.y - line.origin.y) > .05f ||
                Mathf.Abs(face.c.y - line.origin.y) > .05f) return false;
            var perpendicular = Vector3.Cross(Vector3.up, line.direction);
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            RoadEdge(face.a, face.b, line, perpendicular, ref min, ref max);
            RoadEdge(face.b, face.c, line, perpendicular, ref min, ref max);
            RoadEdge(face.c, face.a, line, perpendicular, ref min, ref max);
            if (max - min <= .001f) return false;
            interval = new Vector2(min, max);
            return true;
        }

        static void RoadEdge(Vector3 a, Vector3 b, RoadLine line, Vector3 perpendicular, ref float min, ref float max)
        {
            float da = Vector3.Dot(a - line.origin, perpendicular), db = Vector3.Dot(b - line.origin, perpendicular);
            if (Mathf.Abs(da) < .001f)
            {
                float t = Vector3.Dot(a - line.origin, line.direction);
                min = Mathf.Min(min, t); max = Mathf.Max(max, t);
            }
            if (da * db < 0)
            {
                var point = Vector3.Lerp(a, b, da / (da - db));
                float t = Vector3.Dot(point - line.origin, line.direction);
                min = Mathf.Min(min, t); max = Mathf.Max(max, t);
            }
        }

        void EmitRoadLine(RoadLine line, List<Vector2> spans, JcBattleSurfaceProfile profile)
        {
            if (!redrawingLines) lineRecords.Add(new LineRecord { line = line, spans = new List<Vector2>(spans) });
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            float length = profile.dashLength;
            float width = profile.lineWidth;
            float gap = profile.dashGap;
            if (explorationLines)
            {
                ResolveBaseline(line, out float baseLength, out float baseGap);
                width = line.baselineWidth * lineValues.x;
                length = baseLength * lineValues.y;
                gap = baseGap * lineValues.z;
            }
            if (width <= 0 || length <= 0 || gap <= 0) return;
            var side = Vector3.Cross(Vector3.up, line.direction) * (width * .5f);
            float period = Mathf.Max(.2f, length + gap);
            foreach (var span in spans)
            {
                // 점선 공백에는 깊이/노멀 패스에도 면을 남기지 않습니다.
                // 횡단보도/도로 구간의 양 끝에서 차선 간격만큼 비웁니다.
                // 시작점에 위상을 맞추므로 첫 마디가 경계에 붙거나 조각으로 남지 않습니다.
                float first = span.x + gap;
                float limit = span.y - gap;
                if (explorationLines)
                {
                    var paints = line.paint.FindAll(p => p.y > span.x && p.x < span.y);
                    if (paints.Count == 0) continue;
                    paints.Sort((a,b) => a.x.CompareTo(b.x));
                    first = Mathf.Max(span.x, paints[0].x);
                    limit = span.y;
                }
                for (float begin = first; begin + length <= limit + .001f; begin += period)
                {
                    float end = begin + length;
                    var a = line.origin + line.direction * begin + Vector3.up * .008f;
                    var b = line.origin + line.direction * end + Vector3.up * .008f;
                    int start = vertices.Count;
                    vertices.Add(transform.InverseTransformPoint(a - side));
                    vertices.Add(transform.InverseTransformPoint(b - side));
                    vertices.Add(transform.InverseTransformPoint(b + side));
                    vertices.Add(transform.InverseTransformPoint(a + side));
                    uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1));
                    uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }
            if (vertices.Count == 0) return;
            var mesh = new Mesh { name = "Connected road markings", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); lineOwned.Add(mesh);
            var material = new Material(line.material) { name = "Connected road markings", hideFlags = HideFlags.DontSave };
            material.shader = profile.roadShader;
            material.SetTexture("_BaseMap", profile.plainAsphalt); material.SetTexture("_PlainMap", profile.plainAsphalt);
            material.SetTextureScale("_BaseMap", Vector2.one); material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_LineColor", lineColor);
            material.SetFloat("_MarkMode", 4); material.SetVector("_LineNormal", new Vector4(1, 0, .5f, 0));
            material.SetVector("_LineWorld", line.direction); material.SetFloat("_TileMeters", profile.lineWidth * 2);
            material.SetFloat("_LineWidth", profile.lineWidth); material.SetFloat("_DashLength", profile.dashLength);
            material.SetFloat("_DashGap", profile.dashGap); lineOwned.Add(material);
            var node = new GameObject("JC_ConnectedRoadLine") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            lineNodes.Add(node);
            node.transform.SetParent(transform, false);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
