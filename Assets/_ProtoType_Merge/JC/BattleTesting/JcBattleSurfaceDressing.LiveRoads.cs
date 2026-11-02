using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    public sealed partial class JcBattleSurfaceDressing
    {
        sealed class LineRecord { public RoadLine line; public List<Vector2> spans; }
        readonly List<LineRecord> lineRecords = new List<LineRecord>();
        readonly List<GameObject> lineNodes = new List<GameObject>();
        readonly List<UnityEngine.Object> lineOwned = new List<UnityEngine.Object>();
        readonly Dictionary<Texture, Rect> paintBounds = new Dictionary<Texture, Rect>();
        bool redrawingLines, explorationLines;
        Vector3 lineValues = Vector3.one;
        Color lineColor = JC.EnvironmentDetail.JcRoadMarkingProfile.DefaultColor;
        public int LineTargetCount => lineRecords.Count;

        internal static Mesh ReadableRoadMesh(Mesh mesh)
        {
            if (mesh == null || mesh.isReadable) return mesh;
            // 배포 빌드에서 CPU 데이터가 제거된 원본도 기존 전투 외형 캐시를 재사용합니다.
            foreach (var path in new[] { "JC_Encounters/DynamicSource", "JC_Encounters/TutorialDynamicSource" })
            {
                var source = Resources.Load<JcDynamicBattleSource>(path);
                if (source == null || source.parts == null) continue;
                foreach (var part in source.parts)
                    if (part.originalMesh == mesh && part.mesh != null && part.mesh.isReadable) return part.mesh;
            }
            return mesh;
        }

        public void InitializeExplorationLines(MeshFilter[] filters)
        {
            explorationLines = true;
            BuildConnectedRoadLines(filters, Profile);
            CaptureAuthoredLines(filters, Profile);
            ClearRoadLines(); // 기본 배율에서는 원본 외형을 그대로 표시합니다.
        }

        public void SetRoadValues(Vector3 values) => SetRoadValues(values, JC.EnvironmentDetail.JcRoadMarkingProfile.DefaultColor);

        public void SetRoadValues(Vector3 values, Color color)
        {
            lineColor = color;
            lineValues = values;
            ClearRoadLines();
            if (explorationLines && values == Vector3.one && color == JC.EnvironmentDetail.JcRoadMarkingProfile.DefaultColor) return;
            var settings = Instantiate(Profile);
            try
            {
                if (!explorationLines) { settings.lineWidth = values.x; settings.dashLength = values.y; settings.dashGap = values.z; }
                redrawingLines = true;
                foreach (var record in lineRecords) EmitRoadLine(record.line, record.spans, settings);
            }
            finally { redrawingLines = false; ReleaseRoadObject(settings); }
        }

        void ClearRoadLines()
        {
            foreach (var node in lineNodes) if (node != null) { node.SetActive(false); ReleaseRoadObject(node); }
            foreach (var item in lineOwned) if (item != null) ReleaseRoadObject(item);
            lineNodes.Clear(); lineOwned.Clear();
        }

        static void ReleaseRoadObject(UnityEngine.Object item)
        { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }

        static void ResolveBaseline(RoadLine line, out float length, out float gap)
        {
            line.paint.Sort((a, b) => a.x.CompareTo(b.x));
            length = line.paint.Count > 0 ? line.paint[0].y - line.paint[0].x : 0;
            gap = float.PositiveInfinity;
            for (int i = 1; i < line.paint.Count; i++)
            {
                float candidate = line.paint[i].x - line.paint[i - 1].y;
                if (candidate > .01f) gap = Mathf.Min(gap, candidate);
            }
            if (float.IsInfinity(gap)) gap = Mathf.Max(.1f, length * 3);
        }

        void CapturePaint(RoadLine line, Material material, Vector2 uv, Vector3 vertex, Vector3 du, Vector3 dv, Vector2 normal)
        {
            var texture = material.GetTexture("_BaseMap");
            if (texture == null) return;
            if (!paintBounds.TryGetValue(texture, out var rect))
            {
                rect = MeasurePaint(texture);
                paintBounds.Add(texture, rect);
            }
            if (rect.width <= 0 || rect.height <= 0) return;
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            var side = Vector3.Cross(Vector3.up, line.direction);
            float sideMin = float.PositiveInfinity, sideMax = float.NegativeInfinity;
            foreach (float x in new[] { rect.xMin, rect.xMax }) foreach (float y in new[] { rect.yMin, rect.yMax })
            {
                var point = vertex + du * (x - uv.x) + dv * (y - uv.y);
                float t = Vector3.Dot(point - line.origin, line.direction);
                min = Mathf.Min(min, t); max = Mathf.Max(max, t);
                float w = Vector3.Dot(point, side); sideMin = Mathf.Min(sideMin, w); sideMax = Mathf.Max(sideMax, w);
            }
            float width = sideMax - sideMin;
            // UV 경계형 도색은 옆 타일과 합쳐지는 반쪽 폭입니다.
            if ((Mathf.Abs(normal.x) > .99f && (rect.xMin < .01f || rect.xMax > .99f)) ||
                (Mathf.Abs(normal.y) > .99f && (rect.yMin < .01f || rect.yMax > .99f))) width *= 2;
            line.baselineWidth = Mathf.Max(line.baselineWidth, width);
            if (!line.paint.Exists(p => Mathf.Abs(p.x - min) < .01f && Mathf.Abs(p.y - max) < .01f))
                line.paint.Add(new Vector2(min, max));
        }

        // 읽기 불가 임포트 텍스처도 원본 설정을 변경하지 않고 최초 1회 분석합니다.
        static Rect MeasurePaint(Texture source)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32);
            var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
            try
            {
                Graphics.Blit(source, target); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
                var pixels = image.GetPixels32(); int minX = 256, minY = 256, maxX = -1, maxY = -1;
                for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                {
                    var c = pixels[y * 256 + x];
                    if (Math.Min(c.r, c.g) - c.b < 15) continue;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
                return maxX < 0 ? default : Rect.MinMaxRect(minX / 256f, minY / 256f, (maxX + 1) / 256f, (maxY + 1) / 256f);
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); ReleaseRoadObject(image); }
        }
    }
}
