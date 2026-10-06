using System.Collections.Generic;
using UnityEngine;

/// <summary>둥근 사각 윤곽을 따라 윗면·밑면·양쪽 벽을 생성합니다. 움직임에는 재생성하지 않습니다.</summary>
public static class BattleCellBorderMesh
{
    public static void Build(Mesh mesh, Vector2 size, float radius, float width, float depth, int segments, bool fill)
    {
        size = new Vector2(Mathf.Max(.01f,size.x),Mathf.Max(.01f,size.y));
        float half = Mathf.Min(size.x,size.y)*.5f;
        width = Mathf.Clamp(width,.001f,half*.95f);
        radius = Mathf.Clamp(radius,0,half);
        var outside = Outline(size, radius, segments);
        var inside = Outline(size-Vector2.one*width*2, Mathf.Max(0,radius-width), segments);
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int i=0;i<outside.Count;i++)
        {
            int j=(i+1)%outside.Count;
            Vector3 a=outside[i],b=outside[j],c=inside[i],d=inside[j];
            Vector3 up=Vector3.up*Mathf.Max(.001f,depth);
            if(fill) Triangle(Vector3.zero,a,b,Vector3.up);
            else {
                Quad(a+up,b+up,d+up,c+up,Vector3.up);
                Quad(a,c,d,b,Vector3.down);
                Quad(a,b,b+up,a+up, new Vector3((a+b).x,0,(a+b).z));
                Quad(c+up,d+up,d,c, new Vector3(-(c+d).x,0,-(c+d).z));
            }
        }
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(1,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 expected){Triangle(a,b,c,expected);Triangle(a,c,d,expected);}
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 expected)
        {
            Vector3 normal=Vector3.Cross(b-a,c-a);
            if(normal.sqrMagnitude<1e-14f)return;
            if(Vector3.Dot(normal,expected)<0){var swap=b;b=c;c=swap;normal=-normal;}
            normal.Normalize();Add(a,normal);Add(b,normal);Add(c,normal);
        }
        void Add(Vector3 p,Vector3 normal){triangles.Add(vertices.Count);vertices.Add(p);normals.Add(normal);uv.Add(new Vector2(p.x/size.x+.5f,p.z/size.y+.5f));}
    }
    private static List<Vector3> Outline(Vector2 size,float radius,int segments)
    {
        var points=new List<Vector3>();segments=Mathf.Clamp(segments,2,32);
        var half=size*.5f;
        for(int corner=0;corner<4;corner++)
        {
            float angle=corner*Mathf.PI*.5f;
            Vector2 center=new Vector2(corner==0||corner==3?half.x-radius:-half.x+radius,corner<2?half.y-radius:-half.y+radius);
            for(int i=0;i<=segments;i++){float a=angle+i*Mathf.PI*.5f/segments;points.Add(new Vector3(center.x+Mathf.Cos(a)*radius,0,center.y+Mathf.Sin(a)*radius));}
        }
        return points;
    }
}
