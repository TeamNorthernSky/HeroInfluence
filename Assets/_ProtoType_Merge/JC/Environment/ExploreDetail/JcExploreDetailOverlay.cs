using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
namespace JC.EnvironmentDetail
{
    // DH 생성 결과만 읽습니다. 원본 MeshFilter, 재질, Collider, GridManager는 수정하지 않습니다.
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed partial class JcExploreDetailOverlay : MonoBehaviour
    {
        [Tooltip("생성용 초안입니다. 수치는 편집기 재생성·베이크 확정으로 반영합니다. 컴포넌트를 끄면 즉시 원본으로 돌아갑니다.")]
        public JcExploreDetailProfile profile;
        [SerializeField,Tooltip("현재 적용한 도로/보도 청크와 경계 수입니다.")]
        private string status;
        public string Status=>status;
        readonly Dictionary<Renderer,bool> originals=new Dictionary<Renderer,bool>();
        readonly Dictionary<MeshFilter,MeshFilter> visualCopies=new Dictionary<MeshFilter,MeshFilter>();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        GameObject output;
        JcWaterController waterController;
        void BindWater(){waterController=JcWaterController.Ensure(this);waterController.Bind(output);}
        struct Cell { public Vector3 center; public float size; public int kind; public Texture2D texture; public Vector2 uvMin,uvMax; }
        static readonly Vector2Int[] dirs={Vector2Int.left,Vector2Int.right,Vector2Int.down,Vector2Int.up};
        static Vector2Int Key(Vector3 p)=>new Vector2Int(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.z*1000));
        static Vector2Int Neighbor(Cell c,int d)=>Key(c.center+new Vector3(dirs[d].x,0,dirs[d].y)*c.size);
        static void Dispose(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        public void Restore()
        { ReleaseVisual(true); }
        void ReleaseVisual(bool releaseWater)
        {
            if(releaseWater && waterController!=null)waterController.Unbind();
            foreach(var pair in originals)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;
            originals.Clear();visualCopies.Clear();if(output!=null){output.SetActive(false);Dispose(output);}output=null;
            foreach(var o in owned)Dispose(o);owned.Clear();
            appliedBake=null;appliedPrefab=null;
        }
        #if UNITY_EDITOR
        MeshFilter[] Sources() => JcExploreDetailBake.Sources(gameObject.scene);
        int Kind(MeshFilter f)
        {
            var m=f.GetComponent<MeshRenderer>().sharedMaterial;var tex=m!=null&&m.HasProperty("_BaseMap")?m.GetTexture("_BaseMap"):null;
            if(tex==null)return 0;
            if(tex.name==profile.sidewalkTexture&&f.name.EndsWith("_RaisedCube"))return 2;
            if(tex.name==profile.grassTexture)return 3;
            if(tex.name==profile.waterTexture)return 5;
            if(tex.name.StartsWith("ParkRoad",StringComparison.Ordinal))return 4;
            return !string.IsNullOrEmpty(profile.roadTexturePrefix)&&tex.name.StartsWith(profile.roadTexturePrefix,StringComparison.Ordinal)?1:0;
        }
        static int Stride(MeshFilter f)=>f.name.EndsWith("_RaisedCube")||f.name.EndsWith("_FlatCube")?20:4;
        void Build(MeshFilter[] sources)
        {
            output=new GameObject("JC_ExploreDetail_Visuals");output.hideFlags=HideFlags.DontSave;output.transform.SetParent(transform,false);output.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);output.transform.localScale=new Vector3(1/transform.lossyScale.x,1/transform.lossyScale.y,1/transform.lossyScale.z);
            var cells=new Dictionary<Vector2Int,Cell>();
            foreach(var f in sources){int kind=Kind(f);if(kind==0)continue;var vs=f.sharedMesh.vertices;int stride=Stride(f);if(vs.Length%stride!=0)continue;
                var uv=f.sharedMesh.uv;var texture=f.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap") as Texture2D;
                for(int i=0;i<vs.Length;i+=stride){Vector3 a=f.transform.TransformPoint(vs[i]),c=f.transform.TransformPoint(vs[i+2]);var cell=new Cell{center=(a+c)*.5f,size=Mathf.Abs(c.x-a.x),kind=kind,texture=texture,uvMin=uv[i],uvMax=uv[i+2]};if(cell.size>.001f)cells[Key(cell.center)]=cell;}}
            var mats=new Dictionary<Material,Material>();int roadCount=0,pavingCount=0;
            Material curb=profile.curbMaterial!=null?new Material(profile.curbMaterial){hideFlags=HideFlags.DontSave}:null;
            if(curb!=null){curb.SetColor("_BaseColor",profile.curbTint);owned.Add(curb);}
            Material joint=curb!=null?new Material(curb){hideFlags=HideFlags.DontSave}:null;
            if(joint!=null){joint.SetColor("_BaseColor",profile.jointTint);joint.SetTexture("_BaseMap",null);owned.Add(joint);}
            var plainRoad=sources.FirstOrDefault(f=>Kind(f)==1&&f.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap").name=="Road_01");
            Material roadFill=plainRoad!=null?plainRoad.GetComponent<MeshRenderer>().sharedMaterial:curb;
            foreach(var f in sources){int kind=Kind(f);var source=f.GetComponent<MeshRenderer>();if(!source.enabled||source.forceRenderingOff)continue;
                if(kind==1&&(profile.roadSurface||(profile.whiteCrosswalks&&JC.BattleTesting.JcBattleSurfaceDressing.IsCrosswalk(source.sharedMaterial)))){var original=source.sharedMaterial;if(!mats.TryGetValue(original,out var mat)){mat=new Material(original){name=original.name+"_JC road detail",hideFlags=HideFlags.DontSave};owned.Add(mat);mats.Add(original,mat);
                    if(profile.roadSurface&&profile.roadNormal!=null&&mat.HasProperty("_BumpMap")){mat.SetTexture("_BumpMap",profile.roadNormal);mat.SetFloat("_BumpScale",profile.roadNormalStrength);mat.EnableKeyword("_NORMALMAP");}
                    if(profile.roadSurface&&profile.roadGrain!=null&&mat.HasProperty("_DetailAlbedoMap")){mat.SetTexture("_DetailAlbedoMap",profile.roadGrain);mat.SetTextureScale("_DetailAlbedoMap",Vector2.one*5);mat.SetFloat("_DetailAlbedoMapScale",profile.roadGrainStrength);mat.EnableKeyword("_DETAIL_SCALED");mat.DisableKeyword("_DETAIL_MULX2");}
                    if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.08f);
                    if(profile.whiteCrosswalks&&JC.BattleTesting.JcBattleSurfaceDressing.IsCrosswalk(mat))JC.BattleTesting.JcBattleSurfaceDressing.WhiteCrosswalk(mat);}
                    var roadMesh=Instantiate(f.sharedMesh);roadMesh.name=f.name+"_RoadDetail";roadMesh.hideFlags=HideFlags.DontSave;roadMesh.RecalculateTangents();owned.Add(roadMesh);
                    CloneVisual(f,roadMesh,mat);roadCount++;
                }else if(kind==2&&(profile.roundSidewalk||profile.sidewalkCurb)){var mesh=RoundedPaving(f,cells);owned.Add(mesh);CloneVisual(f,mesh,new[]{source.sharedMaterial,curb!=null?curb:source.sharedMaterial,roadFill,joint!=null?joint:source.sharedMaterial});pavingCount++;}}
            var instances=new List<CombineInstance>();int pavingEdges=profile.sidewalkCurb?cells.Values.Where(c=>c.kind==2).Sum(c=>dirs.Select((d,i)=>Neighbor(c,i)).Count(k=>cells.TryGetValue(k,out var next)&&(next.kind==1||(next.kind==3&&c.center.y-next.center.y>profile.flatBoundaryTolerance)))):0,grassEdges=0;
            if(profile.curbMesh!=null&&profile.curbMaterial!=null)foreach(var c in cells.Values){if(c.kind!=1)continue;for(int d=0;d<4;d++){if(!cells.TryGetValue(Neighbor(c,d),out var next))continue;bool grass=(next.kind==3||(next.kind==4&&profile.naturalRoadEdges))&&profile.roadsideCurb;if(!grass)continue;grassEdges++;
                float height=Mathf.Abs(next.center.y-c.center.y)<=profile.flatBoundaryTolerance?profile.flatGrassCurbHeight:profile.roadsideHeight;
                var n=new Vector3(dirs[d].x,0,dirs[d].y);var pos=c.center+n*(c.size*.5f+profile.curbWidth*.5f);pos.y=c.center.y+height*.5f;
                var opening=profile.naturalRoadEdges?PathOpening(next,d^1):new Vector2(-1,-1);
                // 미분석 흙길은 입구를 막는 연석을 추측 배치하지 않습니다.
                if(next.kind==4&&!HasPathRule(next))continue;
                foreach(var range in SolidRanges(opening)){
                    bool meetsStart=opening.x>=0&&Mathf.Abs(range.x-opening.y)<.00001f;
                    bool meetsEnd=opening.x>=0&&Mathf.Abs(range.y-opening.x)<.00001f;
                    float markerHalf=opening.x>=0?Mathf.Min(profile.entranceMarkerWidth,(opening.y-opening.x)*c.size*.2f)*.5f:0;
                    float gapStart=meetsStart?markerHalf+.0002f:profile.jointWidth*.5f;
                    float gapEnd=meetsEnd?markerHalf+.0002f:profile.jointWidth*.5f;
                    float length=(range.y-range.x)*c.size-gapStart-gapEnd;if(length<=.002f)continue;
                    var tangent=d<2?Vector3.forward:Vector3.right;
                    var at=pos+tangent*((range.x+range.y-1)*.5f*c.size+(gapStart-gapEnd)*.5f);
                    // 블록 길이축은 회전 후에도 경계의 양의 접선 방향입니다.
                    var block=BeveledCurb(profile.curbWidth,height,length,!meetsStart,!meetsEnd);owned.Add(block);
                    instances.Add(new CombineInstance{mesh=block,transform=Matrix4x4.TRS(at,Quaternion.Euler(0,d<2?0:90,0),Vector3.one)});
                }
            }}
            AddGrassCurbCorners(cells,instances);
            if(instances.Count>0){var mesh=new Mesh{name="JC continuous boundaries",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.DontSave};mesh.CombineMeshes(instances.ToArray());owned.Add(mesh);var go=new GameObject("연속 연석: 보도·야지 경계");go.hideFlags=HideFlags.DontSave;go.transform.SetParent(output.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=curb;}
            string edgeStatus=BuildNaturalEdges(cells);
            string waterStatus=BuildWaterfront(sources,cells);
            status=$"ON: 도로 {roadCount}, 보도 {pavingCount}, 보도 연석 {pavingEdges}, 야지 연석 {grassEdges}; {edgeStatus}; {waterStatus}";
        }
        void CloneVisual(MeshFilter source,Mesh mesh,Material material)=>CloneVisual(source,mesh,new[]{material});
        void CloneVisual(MeshFilter source,Mesh mesh,Material[] materials)
        {
            var go=new GameObject("Detail_"+source.name);go.hideFlags=HideFlags.DontSave;go.transform.SetParent(output.transform,false);
            go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);go.transform.localScale=source.transform.lossyScale;
            go.layer=source.gameObject.layer;var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;visualCopies[source]=filter;
            var sr=source.GetComponent<MeshRenderer>();var rr=go.AddComponent<MeshRenderer>();rr.sharedMaterials=materials;rr.shadowCastingMode=sr.shadowCastingMode;rr.receiveShadows=sr.receiveShadows;
            var block=new MaterialPropertyBlock();sr.GetPropertyBlock(block);rr.SetPropertyBlock(block);originals[sr]=sr.forceRenderingOff;sr.forceRenderingOff=true;
        }
        // 같은 외곽선에서 보도·연석·측면을 나눠 만들어 면 중첩을 제거합니다.
        Mesh RoundedPaving(MeshFilter f,Dictionary<Vector2Int,Cell> cells)
        {
            var source=f.sharedMesh;var vs=source.vertices;var uv=source.uv;
            var points=new List<Vector3>();var uvs=new List<Vector2>();var tris=new[]{new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            Action<int,Vector3,Vector3,Vector3,Vector2,Vector2,Vector2> tri=(sub,a,b,c,ua,ub,uc)=>{
                if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-14f)return;
                int k=points.Count;points.AddRange(new[]{a,b,c});uvs.AddRange(new[]{ua,ub,uc});tris[sub].AddRange(new[]{k,k+1,k+2});};
            if(vs.Length%20!=0)throw new InvalidOperationException("지원하지 않는 DH 보도 메시 구조: "+f.name);
            for(int i=0;i<vs.Length;i+=20){var lo=vs[i];var hi=vs[i+2];var cell=cells[Key((f.transform.TransformPoint(lo)+f.transform.TransformPoint(hi))*.5f)];
                var road=new bool[4];var border=new bool[4];float ground=.01f;for(int d=0;d<4;d++)if(cells.TryGetValue(Neighbor(cell,d),out var other)&&other.kind==1){road[d]=true;ground=f.transform.InverseTransformPoint(other.center).y;}
                for(int d=0;d<4;d++)border[d]=road[d]||(cells.TryGetValue(Neighbor(cell,d),out var adjacent)&&adjacent.kind==3&&cell.center.y-adjacent.center.y>profile.flatBoundaryTolerance);
                float w=Mathf.Min(profile.curbWidth,(hi.x-lo.x)*.2f);float radius=Mathf.Clamp(profile.cornerRadius,w+.01f,(hi.x-lo.x)*.45f);
                float left=lo.x+(border[0]&&profile.sidewalkCurb?w:0),right=hi.x-(border[1]&&profile.sidewalkCurb?w:0),back=lo.z+(border[2]&&profile.sidewalkCurb?w:0),front=hi.z-(border[3]&&profile.sidewalkCurb?w:0);
                var outer=new List<Vector3>();var inner=new List<Vector3>();bool curved=false;var concave=new bool[4];
                // BR, TR, TL, BL. Corresponding samples keep the curb ring disjoint from paving.
                int[] dx={1,1,0,0},dz={2,3,3,2};
                for(int corner=0;corner<4;corner++){
                    // 양옆은 같은 높이의 보도이고 대각선이 도로 또는 낮은 풀밭인 안쪽 교차점을 채웁니다.
                    var diagonal=Key(cell.center+new Vector3(dirs[dx[corner]].x,0,dirs[dz[corner]].y)*cell.size);
                    concave[corner]=profile.sidewalkCurb
                        &&cells.TryGetValue(Neighbor(cell,dx[corner]),out var sideX)&&sideX.kind==2
                        &&cells.TryGetValue(Neighbor(cell,dz[corner]),out var sideZ)&&sideZ.kind==2
                        &&Mathf.Abs(sideX.center.y-cell.center.y)<.001f&&Mathf.Abs(sideZ.center.y-cell.center.y)<.001f
                        &&cells.TryGetValue(diagonal,out var across)
                        &&(across.kind==1||(across.kind==3&&cell.center.y-across.center.y>profile.flatBoundaryTolerance));
                    bool rounded=profile.roundSidewalk&&road[dx[corner]]&&road[dz[corner]];curved|=rounded;
                    float r=rounded?radius:0,ir=rounded?Mathf.Max(0,r-(profile.sidewalkCurb?w:0)):0;
                    float cx=(corner<2?hi.x-r:lo.x+r),cz=(corner==1||corner==2?hi.z-r:lo.z+r);
                    float ix=(corner<2?right-ir:left+ir),iz=(corner==1||corner==2?front-ir:back+ir);
                    for(int j=0;j<=16;j++){float angle=(-90+corner*90+j*90f/16)*Mathf.Deg2Rad;
                        outer.Add(new Vector3(cx+Mathf.Cos(angle)*r,lo.y,cz+Mathf.Sin(angle)*r));
                        if(concave[corner])
                        {
                            var x=new Vector3(corner<2?-w:w,0,0);var z=new Vector3(0,0,corner==1||corner==2?-w:w);
                            var origin=new Vector3(cx,lo.y,cz);
                            // 보도 상판에서 블록 자리만 제외하므로 같은 높이의 면이 겹치지 않습니다.
                            inner.Add(origin+(j==0?(corner%2==0?x:z):j==16?(corner%2==0?z:x):x+z));
                        }
                        else inner.Add(new Vector3(ix+Mathf.Cos(angle)*ir,lo.y,iz+Mathf.Sin(angle)*ir));}
                }
                Func<Vector3,Vector2> texture=p=>Vector2.Lerp(Vector2.Lerp(uv[i],uv[i+1],(p.x-lo.x)/(hi.x-lo.x)),Vector2.Lerp(uv[i+3],uv[i+2],(p.x-lo.x)/(hi.x-lo.x)),(p.z-lo.z)/(hi.z-lo.z));
                var center=new Vector3((left+right)*.5f,lo.y,(back+front)*.5f);
                for(int j=0;j<outer.Count;j++){int n=(j+1)%outer.Count;var a=outer[j];var b=outer[n];var ia=inner[j];var ib=inner[n];
                    tri(0,center,ib,ia,texture(center),texture(ib),texture(ia));
                    bool strip=(a-ia).sqrMagnitude+(b-ib).sqrMagnitude>1e-8f;
                    float length=Vector3.Distance(a,b);int divisions=length>.2f?Mathf.Max(1,Mathf.CeilToInt(length/profile.curbBlockLength)):1;
                    for(int segment=0;segment<divisions;segment++){
                        float t0=segment/(float)divisions,t1=(segment+1f)/divisions;
                        bool infill=concave[j/17]&&j%17<16;
                        bool mark=strip&&!infill&&(length>.2f||j%17==0);
                        float end=mark?Mathf.Min(t1,t0+profile.jointWidth/Mathf.Max(.0001f,length)):t0;
                        var cuts=mark?new[]{t0,end,t1}:new[]{t0,t1};
                        for(int piece=0;piece<cuts.Length-1;piece++){
                            var oa=Vector3.Lerp(a,b,cuts[piece]);var ob=Vector3.Lerp(a,b,cuts[piece+1]);
                            var xa=Vector3.Lerp(ia,ib,cuts[piece]);var xb=Vector3.Lerp(ia,ib,cuts[piece+1]);
                            float wa=Vector3.Distance(oa,xa),wb=Vector3.Distance(ob,xb);
                            var sa=Vector3.Lerp(xa,oa,Mathf.Min(.4f,profile.jointWidth/Mathf.Max(.0001f,wa)));
                            var sb=Vector3.Lerp(xb,ob,Mathf.Min(.4f,profile.jointWidth/Mathf.Max(.0001f,wb)));
                            int sub=mark&&piece==0?3:1;
                            float bevel=strip&&!infill?Mathf.Min(profile.curbBevel,Mathf.Min(wa,wb)*.25f,Mathf.Max(0,lo.y-ground)*.4f):0;
                            var ta=Vector3.MoveTowards(oa,xa,bevel);var tb=Vector3.MoveTowards(ob,xb,bevel);
                            var ea=oa-Vector3.up*bevel;var eb=ob-Vector3.up*bevel;
                            tri(sub,ta,sb,tb,texture(ta),texture(sb),texture(tb));tri(sub,ta,sa,sb,texture(ta),texture(sa),texture(sb));
                            tri(sub,ta,tb,eb,texture(ta),texture(tb),texture(ob));tri(sub,ta,eb,ea,texture(ta),texture(ob),texture(oa));
                            tri(3,sa,xb,sb,texture(sa),texture(xb),texture(sb));tri(3,sa,xa,xb,texture(sa),texture(xa),texture(xb));
                            var downA=new Vector3(oa.x,vs[i+4].y,oa.z);var downB=new Vector3(ob.x,vs[i+4].y,ob.z);
                            int side=strip?sub:0;
                            tri(side,ea,eb,downB,texture(oa),texture(ob),texture(ob));tri(side,ea,downB,downA,texture(oa),texture(ob),texture(oa));
                        }
                    }
                }
                if(curved){ // Replace the removed corner with asphalt at road height, not a hole.
                    var a=new Vector3(lo.x,ground,lo.z);var b=new Vector3(hi.x,ground,lo.z);var c=new Vector3(hi.x,ground,hi.z);var d=new Vector3(lo.x,ground,hi.z);
                    tri(2,a,c,b,Vector2.zero,Vector2.one,Vector2.right);tri(2,a,d,c,Vector2.zero,Vector2.up,Vector2.one);
                }
            }
            var m=new Mesh{name=f.name+"_RoundedPlanCorners",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.DontSave};m.SetVertices(points);m.SetUVs(0,uvs);m.subMeshCount=4;for(int i=0;i<4;i++)m.SetTriangles(tris[i],i);m.RecalculateNormals();SmoothCurveNormals(m);m.RecalculateTangents();m.RecalculateBounds();return m;
        }
        #endif
    }
}


