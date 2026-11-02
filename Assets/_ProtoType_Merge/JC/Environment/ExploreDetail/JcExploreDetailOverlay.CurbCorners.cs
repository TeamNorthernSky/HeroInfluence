#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
namespace JC.EnvironmentDetail
{
    public sealed partial class JcExploreDetailOverlay
    {
        // 도로 한 셀의 두 야지 경계가 만나는 바깥 모서리에서 비어 있던 폭×폭 영역을 채웁니다.
        void AddGrassCurbCorners(Dictionary<Vector2Int,Cell> cells,List<CombineInstance> instances)
        {
            if(!profile.roadsideCurb||!profile.curbMesh||!profile.curbMaterial)return;
            foreach(var c in cells.Values)
            {
                if(c.kind!=1)continue;
                for(int x=0;x<2;x++)for(int z=2;z<4;z++)
                {
                    if(!cells.TryGetValue(Neighbor(c,x),out var a)||!cells.TryGetValue(Neighbor(c,z),out var b))continue;
                    if(!CornerGrass(a)||!CornerGrass(b))continue;
                    if(CornerOpening(a,x^1,z==2?0:1)||CornerOpening(b,z^1,x==0?0:1))continue;
                    float ha=Mathf.Abs(a.center.y-c.center.y)<=profile.flatBoundaryTolerance?profile.flatGrassCurbHeight:profile.roadsideHeight;
                    float hb=Mathf.Abs(b.center.y-c.center.y)<=profile.flatBoundaryTolerance?profile.flatGrassCurbHeight:profile.roadsideHeight;
                    float height=Mathf.Min(ha,hb),width=profile.curbWidth;
                    var center=c.center+new Vector3(dirs[x].x,0,dirs[z].y)*(c.size*.5f+width*.5f);center.y=c.center.y+height*.5f;
                    var mesh=BeveledCurb(width,height,width,false,false);owned.Add(mesh);
                    instances.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(center,Quaternion.identity,Vector3.one)});
                }
            }
        }
        bool CornerGrass(Cell cell)=>cell.kind==3||(cell.kind==4&&profile.naturalRoadEdges&&HasPathRule(cell));
        bool CornerOpening(Cell cell,int direction,int endpoint)
        {
            if(!profile.naturalRoadEdges)return false;
            var opening=PathOpening(cell,direction);
            return opening.x>=0&&endpoint>=opening.x-.00001f&&endpoint<=opening.y+.00001f;
        }
    }
}
#endif
