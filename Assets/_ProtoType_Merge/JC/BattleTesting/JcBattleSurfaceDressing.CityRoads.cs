using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    public sealed partial class JcBattleSurfaceDressing
    {
        bool centerCityRoads;
        public int CenteredCitySegments { get; private set; }
        // JC | 2026-11-05 | 기존 도로 도색 제거/연속 점선 생성기를 재사용합니다. 도시 전용 굽기에서만 중심 보정을 켭니다.
        public static JcBattleSurfaceDressing PrepareCityRoads(GameObject city)
        {
            var owner = city.AddComponent<JcBattleSurfaceDressing>();
            owner.centerCityRoads = true;
            owner.Build(Profile, 0, null, false);
            return owner;
        }
        List<RoadLine> CenterCityLines(List<RoadLine> sources, List<RoadFace> faces)
        {
            var result = new List<RoadLine>();
            foreach (var source in sources)
            foreach (var seed in source.seeds)
            {
                var side = Vector3.Cross(Vector3.up, source.direction);
                var cross = new RoadLine { origin = source.origin + source.direction * ((seed.x + seed.y) * .5f), direction = side };
                var intervals = new List<Vector2>();
                foreach (var face in faces) if (RoadInterval(face, cross, out var span)) intervals.Add(span);
                intervals.Sort((a,b) => a.x.CompareTo(b.x));
                var merged = new List<Vector2>();
                foreach (var span in intervals)
                {
                    if (merged.Count == 0 || span.x > merged[merged.Count-1].y + .02f) merged.Add(span);
                    else { var last = merged[merged.Count-1]; last.y = Mathf.Max(last.y,span.y); merged[merged.Count-1] = last; }
                }
                foreach (var span in merged)
                {
                    if (span.x > .02f || span.y < -.02f) continue;
                    var origin = source.origin + side * ((span.x + span.y) * .5f);
                    var line = result.Find(x => Vector3.Dot(x.direction,source.direction) > .9999f &&
                        Mathf.Abs(x.origin.y-origin.y) < .02f && Vector3.Cross(x.direction,origin-x.origin).magnitude < .02f);
                    if (line == null) { line = new RoadLine { origin=origin, direction=source.direction, material=source.material, baselineWidth=span.y-span.x }; result.Add(line); }
                    line.seeds.Add(seed); CenteredCitySegments++; break;
                }
            }
            // JC | 2026-11-05 | 교차로의 넓어진 단면이 평행한 가짜 축을 만들지 않도록 같은 도로 폭 안의 후보를 통합합니다.
            // 실제 도로 삼각형이 지지하는 구간 길이가 가장 긴 중앙축을 유지합니다.
            var mergedLines = new List<RoadLine>();
            var pending = new List<RoadLine>(result);
            while (pending.Count > 0)
            {
                var group = new List<RoadLine> { pending[0] }; pending.RemoveAt(0);
                for (int g = 0; g < group.Count; g++)
                    for (int i = pending.Count-1; i >= 0; i--)
                    {
                        var a=group[g]; var b=pending[i];
                        if (Vector3.Dot(a.direction,b.direction) < .9999f || Mathf.Abs(a.origin.y-b.origin.y) > .05f) continue;
                        float offset=Mathf.Abs(Vector3.Dot(Vector3.Cross(Vector3.up,a.direction),a.origin-b.origin));
                        float mergeDistance=Mathf.Min(Mathf.Min(a.baselineWidth,b.baselineWidth)*.51f,transform.lossyScale.x*1.05f);
                        if(offset > mergeDistance)continue;
                        group.Add(b);pending.RemoveAt(i);
                    }
                RoadLine best=group[0];float bestSupport=-1;
                foreach(var line in group){float support=0;foreach(var seed in line.seeds)support+=seed.y-seed.x;if(support>bestSupport){best=line;bestSupport=support;}}
                var combined=new RoadLine{origin=best.origin,direction=best.direction,material=best.material,baselineWidth=best.baselineWidth};
                foreach(var line in group)foreach(var seed in line.seeds)
                {
                    float shift=Vector3.Dot(line.origin-best.origin,best.direction);
                    combined.seeds.Add(seed+Vector2.one*shift);
                }
                mergedLines.Add(combined);
            }
            return mergedLines;
        }

        // JC | 2026-11-05 | 횡단보도 텍스처가 없는 교차로도 서로 만나는 도로 폭만큼 비워 T/십자 도색 중첩을 막습니다.
        static List<Vector2> TrimCityJunctions(RoadLine line, List<Vector2> spans, List<RoadLine> lines, JcBattleSurfaceProfile profile)
        {
            foreach (var other in lines)
            {
                if (Mathf.Abs(Vector3.Dot(line.direction,other.direction))>.1f || Mathf.Abs(line.origin.y-other.origin.y)>.05f) continue;
                var side=Vector3.Cross(Vector3.up,other.direction);
                float t=Vector3.Dot(other.origin-line.origin,side)/Vector3.Dot(line.direction,side);
                float along=Vector3.Dot(line.origin+line.direction*t-other.origin,other.direction);
                float min=float.PositiveInfinity,max=float.NegativeInfinity;
                foreach(var seed in other.seeds){min=Mathf.Min(min,seed.x);max=Mathf.Max(max,seed.y);}
                if(along<min-.02f || along>max+.02f)continue;
                float clearance=other.baselineWidth*.5f+profile.lineWidth;
                var next=new List<Vector2>();
                foreach(var span in spans)
                {
                    if(t+clearance<=span.x || t-clearance>=span.y){next.Add(span);continue;}
                    if(t-clearance>span.x)next.Add(new Vector2(span.x,t-clearance));
                    if(t+clearance<span.y)next.Add(new Vector2(t+clearance,span.y));
                }
                spans=next;
            }
            return spans;
        }
    }
}
