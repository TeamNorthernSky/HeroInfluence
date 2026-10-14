#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JC.EnvironmentDetail
{
    public sealed partial class JcExploreDetailOverlay
    {
        // 렌더링용 방향장입니다. 이동 판정과 원본 타일에는 영향을 주지 않습니다.
        void BuildWaterFlowMap(Dictionary<Vector2Int,Cell> cells, Material material)
        {
            material.SetFloat("_FollowCanal",0);
            if(!profile.waterFollowCanal)return;
            var water=cells.Where(p=>p.Value.kind==5).ToDictionary(p=>p.Key,p=>p.Value);
            if(water.Count==0)return;
            var preferred=profile.waterFlowDirection.sqrMagnitude>.0001f?profile.waterFlowDirection.normalized:Vector2.right;
            var directions=new Dictionary<Vector2Int,Vector2>();
            var remaining=new HashSet<Vector2Int>(water.Keys);
            while(remaining.Count>0)
            {
                var component=new List<Vector2Int>();var queue=new Queue<Vector2Int>();
                var seed=remaining.OrderBy(k=>k.x).ThenBy(k=>k.y).First();queue.Enqueue(seed);remaining.Remove(seed);
                while(queue.Count>0)
                {
                    var key=queue.Dequeue();component.Add(key);var c=water[key];
                    for(int d=0;d<4;d++){var next=Neighbor(c,d);if(water.TryGetValue(next,out var n)&&Mathf.Abs(n.center.y-c.center.y)<.01f&&remaining.Remove(next))queue.Enqueue(next);}
                }
                // 선호 방향으로 가장 먼 셀을 하류로 삼고, 수로 안의 거리로 방향을 정합니다.
                var outlet=component.OrderByDescending(k=>Vector2.Dot(new Vector2(water[k].center.x,water[k].center.z),preferred)).ThenBy(k=>k.x).ThenBy(k=>k.y).First();
                var members=new HashSet<Vector2Int>(component);var distance=new Dictionary<Vector2Int,int>{{outlet,0}};queue.Enqueue(outlet);
                while(queue.Count>0)
                {
                    var key=queue.Dequeue();for(int d=0;d<4;d++){var next=Neighbor(water[key],d);if(members.Contains(next)&&!distance.ContainsKey(next)){distance[next]=distance[key]+1;queue.Enqueue(next);}}
                }
                foreach(var key in component)
                {
                    var direction=Vector2.zero;
                    for(int d=0;d<4;d++){var next=Neighbor(water[key],d);if(distance.TryGetValue(next,out var value))direction+=(Vector2)dirs[d]*(distance[key]-value);}
                    directions[key]=direction.sqrMagnitude>.0001f?direction.normalized:preferred;
                }
                // 연결된 같은 수역 안에서만 방향을 부드럽게 만듭니다.
                for(int pass=0;pass<8;pass++)
                {
                    var smoothed=new Dictionary<Vector2Int,Vector2>();
                    foreach(var key in component)
                    {
                        var sum=directions[key]*2;int count=2;
                        for(int d=0;d<4;d++){var next=Neighbor(water[key],d);if(members.Contains(next)){sum+=directions[next];count++;}}
                        var value=sum/count;
                        if(!members.Contains(Neighbor(water[key],0))||!members.Contains(Neighbor(water[key],1)))value.x=0;
                        if(!members.Contains(Neighbor(water[key],2))||!members.Contains(Neighbor(water[key],3)))value.y=0;
                        smoothed[key]=value;
                    }
                    foreach(var pair in smoothed)directions[pair.Key]=pair.Value;
                }
            }
            float step=water.Values.Min(c=>c.size);
            float minX=water.Values.Min(c=>c.center.x)-step,minZ=water.Values.Min(c=>c.center.z)-step;
            int width=Mathf.CeilToInt((water.Values.Max(c=>c.center.x)-minX)/step)+2;
            int height=Mathf.CeilToInt((water.Values.Max(c=>c.center.z)-minZ)/step)+2;
            if(width>1024||height>1024){Debug.LogWarning("JC 수로 방향 지도 범위 초과: 공통 방향으로 표시합니다.",this);return;}
            var pixels=new Color[width*height];var filled=new bool[pixels.Length];var flood=new Queue<int>();
            foreach(var pair in directions)
            {
                var c=water[pair.Key];int x=Mathf.RoundToInt((c.center.x-minX)/step),z=Mathf.RoundToInt((c.center.z-minZ)/step),index=z*width+x;
                pixels[index]=new Color(pair.Value.x*.5f+.5f,pair.Value.y*.5f+.5f,0,1);filled[index]=true;flood.Enqueue(index);
            }
            // 물가 바깥도 가장 가까운 방향으로 채워 bilinear 경계에서 0으로 꺾이지 않게 합니다.
            while(flood.Count>0)
            {
                int index=flood.Dequeue(),x=index%width,z=index/width;
                foreach(var d in dirs){int nx=x+d.x,nz=z+d.y;if(nx<0||nz<0||nx>=width||nz>=height)continue;int next=nz*width+nx;if(filled[next])continue;filled[next]=true;pixels[next]=pixels[index];flood.Enqueue(next);}
            }
            var texture=new Texture2D(width,height,TextureFormat.RGHalf,false,true){name="JC shared canal flow",hideFlags=HideFlags.DontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            texture.SetPixels(pixels);texture.Apply(false,false);owned.Add(texture);
            material.SetTexture("_FlowMap",texture);
            material.SetVector("_FlowMapBounds",new Vector4(minX-step*.5f,minZ-step*.5f,1/(width*step),1/(height*step)));
            material.SetFloat("_FollowCanal",1);
        }
    }
}

#endif
