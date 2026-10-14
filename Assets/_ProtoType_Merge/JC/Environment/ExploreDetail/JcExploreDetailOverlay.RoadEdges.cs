#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.EnvironmentDetail
{
    public sealed partial class JcExploreDetailOverlay
    {
        public struct BoundaryEntry
        {
            public Vector3 position;
            public string tile, category;
            public Vector2 opening;
            public int direction;
        }
        readonly List<BoundaryEntry> boundaries=new List<BoundaryEntry>();
        public IReadOnlyList<BoundaryEntry> Boundaries=>boundaries;
        static void SmoothCurveNormals(Mesh mesh)
        {
            // 같은 위치의 비슷한 방향 면만 연결합니다. 윗면/모따기/수직면의 경계는 유지합니다.
            var vertices=mesh.vertices;var normals=mesh.normals;var result=(Vector3[])normals.Clone();
            var groups=new Dictionary<Vector3Int,List<int>>();
            for(int i=0;i<vertices.Length;i++)
            {
                var key=Vector3Int.RoundToInt(vertices[i]*100000);
                if(!groups.TryGetValue(key,out var list)){list=new List<int>();groups.Add(key,list);}list.Add(i);
            }
            foreach(var group in groups.Values)foreach(int i in group)
            {
                var directions=new List<Vector3>();
                foreach(int j in group)if(Vector3.Dot(normals[i],normals[j])>.9f&&!directions.Any(n=>Vector3.Dot(n,normals[j])>.99999f))directions.Add(normals[j]);
                if(directions.Count>0){var sum=Vector3.zero;foreach(var n in directions)sum+=n;result[i]=sum.normalized;}
            }
            mesh.normals=result;
        }
        // 진입로 쪽 두 모서리만 둥글게 만들고 일반 연석과의 접합면은 평평하게 유지합니다.
        Mesh RoundedEntranceMarker(float width,float height,float length,bool positiveEnd)
        {
            // 블록 두께 전체를 반지름으로 사용하여 반대편 접합 모서리부터 호를 시작합니다.
            float radius=Mathf.Min(width*.49f,length);
            float bevel=Mathf.Min(profile.curbBevel,height*.4f,radius*.5f);
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            System.Func<float,float,List<Vector3>> ring=(inset,y)=>
            {
                float w=width*.5f,l=length*.5f,r=radius-inset;
                var points=new List<Vector3>();
                if(radius<length)points.Add(new Vector3(-w+inset,y,-l));
                for(int corner=0;corner<2;corner++)for(int j=0;j<=8;j++)
                {
                    float angle=(180-corner*90-j*90f/8)*Mathf.Deg2Rad;
                    points.Add(new Vector3((corner==0?-w+radius:w-radius)+Mathf.Cos(angle)*r,y,l-radius+Mathf.Sin(angle)*r));
                }
                if(radius<length)points.Add(new Vector3(w-inset,y,-l));
                if(!positiveEnd){for(int i=0;i<points.Count;i++){var v=points[i];v.z=-v.z;points[i]=v;}points.Reverse();}
                return points;
            };
            System.Action<Vector3,Vector3,Vector3> tri=(a,b,c)=>{int i=vertices.Count;vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z)});triangles.AddRange(new[]{i,i+1,i+2});};
            var bottom=ring(0,-height*.5f);var shoulder=ring(0,height*.5f-bevel);var top=ring(bevel,height*.5f);
            for(int i=0;i<top.Count;i++)
            {
                int j=(i+1)%top.Count;
                tri(new Vector3(0,height*.5f,0),top[i],top[j]);
                tri(bottom[i],bottom[j],shoulder[j]);tri(bottom[i],shoulder[j],shoulder[i]);
                if(bevel>0){tri(shoulder[i],shoulder[j],top[j]);tri(shoulder[i],top[j],top[i]);}
                tri(Vector3.down*height*.5f,bottom[j],bottom[i]);
            }
            var mesh=new Mesh{name="JC entrance rounded corners",hideFlags=HideFlags.DontSave};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();SmoothCurveNormals(mesh);mesh.RecalculateBounds();return mesh;
        }
        Mesh BeveledCurb(float width,float height,float length,bool bevelStart=true,bool bevelEnd=true)
        {
            float b=Mathf.Min(profile.curbBevel,height*.4f,width*.2f,length*.2f);
            var vertices=new List<Vector3>();var uvs=new List<Vector2>();var indices=new List<int>();
            System.Action<Vector3,Vector3,Vector3,Vector3> quad=(a,c,d,e)=>
            {
                int i=vertices.Count;vertices.AddRange(new[]{a,c,d,e});
                uvs.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                indices.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            };
            var bottom=new Vector3[4];var shoulder=new Vector3[4];var top=new Vector3[4];
            int[] xs={-1,-1,1,1},zs={-1,1,1,-1};
            for(int i=0;i<4;i++)
            {
                bottom[i]=new Vector3(xs[i]*width*.5f,-height*.5f,zs[i]*length*.5f);
                shoulder[i]=new Vector3(bottom[i].x,height*.5f-b,bottom[i].z);
                float endBevel=(zs[i]<0?bevelStart:bevelEnd)?b:0;
                top[i]=new Vector3(xs[i]*(width*.5f-b),height*.5f,zs[i]*(length*.5f-endBevel));
            }
            quad(top[0],top[1],top[2],top[3]);
            for(int i=0;i<4;i++){int j=(i+1)%4;quad(bottom[i],bottom[j],shoulder[j],shoulder[i]);if(b>0)quad(shoulder[i],shoulder[j],top[j],top[i]);}
            var mesh=new Mesh{name="JC curb single bevel",hideFlags=HideFlags.DontSave};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        bool HasPathRule(Cell c)=>profile.pathEdgeRules!=null&&profile.pathEdgeRules.Any(r=>r!=null&&r.texture==c.texture);
        Vector2 PathOpening(Cell c,int face)
        {
            if(c.kind!=4)return new Vector2(-1,-1);
            var rule=profile.pathEdgeRules?.FirstOrDefault(r=>r!=null&&r.texture==c.texture);
            if(rule==null)return new Vector2(-1,-1);
            var edge=rule.Edge(face);if(edge.x<0)return edge;
            float lo=face<2?c.uvMin.y:c.uvMin.x,hi=face<2?c.uvMax.y:c.uvMax.x;
            if(edge.y<=lo||edge.x>=hi||hi-lo<.00001f)return new Vector2(-1,-1);
            return new Vector2(Mathf.Clamp01((edge.x-lo)/(hi-lo)),Mathf.Clamp01((edge.y-lo)/(hi-lo)));
        }
        static IEnumerable<Vector2> SolidRanges(Vector2 opening)
        {
            if(opening.x<0){yield return new Vector2(0,1);yield break;}
            if(opening.x>0)yield return new Vector2(0,opening.x);
            if(opening.y<1)yield return new Vector2(opening.y,1);
        }
        sealed class EdgeMesh
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<Color> colors=new List<Color>();
            readonly List<int> triangles=new List<int>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color ca,Color cb,Color cc,Color cd,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud)
            {
                int i=vertices.Count;vertices.AddRange(new[]{a,b,c,d});colors.AddRange(new[]{ca,cb,cc,cd});uv.AddRange(new[]{ua,ub,uc,ud});triangles.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});
            }
            public Mesh Build(string name)
            {
                if(vertices.Count==0)return null;
                var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.DontSave};m.SetVertices(vertices);m.SetUVs(0,uv);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateBounds();return m;
            }
        }
        void EdgeVisual(EdgeMesh data,Material mat,string name)
        {
            var mesh=data.Build(name);if(mesh==null)return;owned.Add(mesh);
            var go=new GameObject(name){hideFlags=HideFlags.DontSave};go.transform.SetParent(output.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var rr=go.AddComponent<MeshRenderer>();rr.sharedMaterial=mat;rr.shadowCastingMode=ShadowCastingMode.Off;
        }
        static Color Alpha(Color c,float a){c.a=a;return c;}
        static float Grain(Vector3 p,int salt)=>Mathf.Repeat(Mathf.Sin(p.x*127.1f+p.z*311.7f+salt*74.7f)*43758.5453f,1);
        string BuildNaturalEdges(Dictionary<Vector2Int,Cell> cells)
        {
            boundaries.Clear();if(!profile.naturalRoadEdges)return "자연 경계 OFF";
            var shader=profile.roadEdgeShader;if(shader==null)return "경계 셰이더 미연결";
            var mat=new Material(shader){hideFlags=HideFlags.DontSave};owned.Add(mat);
                        var soil=new EdgeMesh();var plants=new EdgeMesh();var entrancePaving=new EdgeMesh();
            var markerParts=new List<CombineInstance>();
            var roadMaterial=new Material(mat){hideFlags=HideFlags.DontSave};owned.Add(roadMaterial);
            var plain=Sources().Select(f=>f.GetComponent<MeshRenderer>().sharedMaterial).FirstOrDefault(m=>m.HasProperty("_BaseMap")&&m.GetTexture("_BaseMap")!=null&&m.GetTexture("_BaseMap").name=="Road_01");
            if(plain!=null){roadMaterial.SetTexture("_BaseMap",plain.GetTexture("_BaseMap"));roadMaterial.SetColor("_BaseColor",plain.GetColor("_BaseColor"));}
            roadMaterial.SetFloat("_RoadSurface",1);roadMaterial.SetFloat("_Smoothness",profile.roadSurface?.08f:(plain!=null?plain.GetFloat("_Smoothness"):.08f));
            if(profile.roadSurface&&profile.roadNormal!=null){roadMaterial.SetTexture("_BumpMap",profile.roadNormal);roadMaterial.SetFloat("_BumpScale",profile.roadNormalStrength);}
            int entrances=0,unknown=0,skippedHeight=0;
            foreach(var road in cells.Values.Where(c=>c.kind==1).OrderBy(c=>c.center.x).ThenBy(c=>c.center.z))for(int d=0;d<4;d++)
            {
                if(!cells.TryGetValue(Neighbor(road,d),out var green)||(green.kind!=3&&green.kind!=4))continue;
                var n=new Vector3(dirs[d].x,0,dirs[d].y);var tangent=d<2?Vector3.forward:Vector3.right;
                var edge=road.center+n*road.size*.5f;
                bool known=green.kind==3||HasPathRule(green);bool flat=Mathf.Abs(road.center.y-green.center.y)<=profile.flatBoundaryTolerance;
                var opening=known?PathOpening(green,d^1):new Vector2(-1,-1);
                string category=!known?"미분석 흙길":!flat?"높이차 제외":opening.x>=0?"흙길 입구":"풀밭 경계";
                boundaries.Add(new BoundaryEntry{position=edge,tile=green.texture.name,category=category,opening=opening,direction=d});
                if(!known){unknown++;continue;}if(!flat){skippedHeight++;continue;}
                float curb=profile.roadsideCurb?profile.curbWidth:0;
                foreach(var span in SolidRanges(opening))
                {
                    int segments=Mathf.Max(1,Mathf.CeilToInt((span.y-span.x)*12));
                    for(int j=0;j<segments;j++)
                    {
                        float t0=Mathf.Lerp(span.x,span.y,j/(float)segments),t1=Mathf.Lerp(span.x,span.y,(j+1f)/segments);
                        var a=edge+tangent*((t0-.5f)*road.size)+n*curb;var b=edge+tangent*((t1-.5f)*road.size)+n*curb;a.y=b.y=green.center.y+.006f;
                        float w0=profile.soilEdgeWidth*(.65f+.35f*Grain(a,1)),w1=profile.soilEdgeWidth*(.65f+.35f*Grain(b,1));
                        soil.Quad(a,b,b+n*w1,a+n*w0,Alpha(profile.soilEdgeTint,.42f),Alpha(profile.soilEdgeTint,.42f),Alpha(profile.soilEdgeTint,0),Alpha(profile.soilEdgeTint,0),Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
                        if(Grain(a,4)<.74f||t0<span.x+.025f||t1>span.y-.025f)continue;
                        var root=(a+b)*.5f+n*(w0*.75f);float h=.025f+Grain(a,5)*.025f;
                        var col=profile.edgeGrassTint;
                        for(int blade=0;blade<2;blade++){var lean=tangent*(blade==0?-.01f:.012f);plants.Quad(root-tangent*.01f,root+tangent*.01f,root+Vector3.up*h+lean,root+Vector3.up*h+lean,Alpha(col,.65f),Alpha(col,.65f),Alpha(col,.35f),Alpha(col,.35f),Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);}
                    }
                }
                if(opening.x<0)continue;
                entrances++;
                var a0=edge+tangent*((opening.x-.5f)*road.size);var b0=edge+tangent*((opening.y-.5f)*road.size);
                a0.y=b0.y=green.center.y+.007f;
                float solidDepth=profile.curbWidth*Mathf.Clamp(profile.entranceRoadFraction,.1f,.8f);
                float fadeDepth=Mathf.Min(profile.pathApronLength,profile.curbWidth-solidDepth);
                var a1=a0+n*solidDepth;var b1=b0+n*solidDepth;
                System.Func<Vector3,Vector2> roadUV=p=>new Vector2(p.x+.5f,p.z+.5f)/road.size;
                entrancePaving.Quad(a0,b0,b1,a1,Color.white,Color.white,Color.white,Color.white,roadUV(a0),roadUV(b0),roadUV(b1),roadUV(a1));
                // 겹침 방지용 높이 차이 아래에 원본 흙색이 가는 선으로 드러나지 않도록 닫습니다.
                var lowA=a0;var lowB=b0;lowA.y=lowB.y=road.center.y;
                entrancePaving.Quad(lowA,lowB,b0,a0,Color.white,Color.white,Color.white,Color.white,roadUV(lowA),roadUV(lowB),roadUV(b0),roadUV(a0));
                for(int j=0;j<12;j++)
                {
                    float u=j/12f,v=(j+1)/12f;
                    var a=Vector3.Lerp(a1,b1,u);var b=Vector3.Lerp(a1,b1,v);
                    var c=b+n*fadeDepth;var e=a+n*fadeDepth;
                    entrancePaving.Quad(a,b,c,e,Color.white,Color.white,Alpha(Color.white,0),Alpha(Color.white,0),roadUV(a),roadUV(b),roadUV(c),roadUV(e));
                }
                float markerWidth=Mathf.Min(profile.entranceMarkerWidth,(opening.y-opening.x)*road.size*.2f);
                float markerHeight=profile.flatGrassCurbHeight;
                foreach(float sign in new[]{-1f,1f})
                {
                    var markerMesh=RoundedEntranceMarker(profile.curbWidth,markerHeight,markerWidth,sign<0);owned.Add(markerMesh);
                    // 블록 중심을 풀밭-흙길 경계선에 맞춥니다.
                    var at=(sign<0?a0:b0)+n*profile.curbWidth*.5f;
                    at.y=road.center.y+markerHeight*.5f;
                    markerParts.Add(new CombineInstance{mesh=markerMesh,transform=Matrix4x4.TRS(at,Quaternion.Euler(0,d<2?0:90,0),Vector3.one)});
                }
                var mark=new GameObject($"흙길 입구 {entrances:00} ({edge.x:F1}, {edge.z:F1})"){hideFlags=HideFlags.DontSave};mark.transform.SetParent(output.transform,false);mark.transform.position=(a0+b0)*.5f;
            }
            EdgeVisual(soil,mat,"도로 풀밭 · 얇은 흙띠");EdgeVisual(plants,mat,"도로 풀밭 · 드문 풀");
                        EdgeVisual(entrancePaving,roadMaterial,"흙길 입구 · 포장 및 야지 전이");
            if(markerParts.Count>0&&profile.curbMaterial!=null)
            {
                var mesh=new Mesh{name="JC entrance marker blocks",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.DontSave};mesh.CombineMeshes(markerParts.ToArray());owned.Add(mesh);
                var markerMat=new Material(profile.curbMaterial){hideFlags=HideFlags.DontSave};markerMat.SetColor("_BaseColor",profile.curbTint);owned.Add(markerMat);
                var go=new GameObject("흙길 입구 · 양끝 표시 블록"){hideFlags=HideFlags.DontSave};go.transform.SetParent(output.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=markerMat;
            }
            return $"자연 경계 {boundaries.Count}, 흙길 입구 {entrances}, 미분석 {unknown}, 높이차 제외 {skippedHeight}";
        }
    }
}




#endif
