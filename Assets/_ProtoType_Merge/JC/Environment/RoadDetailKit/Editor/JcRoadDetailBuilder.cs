using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using JC.BattleTesting;
using Object=UnityEngine.Object;
namespace JC.EnvironmentDetail.Editor
{
    public static class JcRoadDetailBuilder
    {
        public const string Folder="Assets/_ProtoType_Merge/JC/Environment/RoadDetailKit";
        static bool busy;
        static JcRoadDetailSettings settings;
        static Material asphalt,concrete,pavingMaterial,paint,centerPaint,edgePaint,soil,grass,stone,metal;
        [MenuItem("JC/환경/도로 디테일 부품 생성 및 Main_1-2 적용")]
        public static void Build() => BuildWithSettings(null);
        public static void BuildWithSettings(JcRoadDetailSettings selected)
        {
            if(busy)return;
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("편집 모드에서 생성해 주세요.");
            busy=true;
            try
            {
                foreach(var d in new[]{"Meshes","Materials","Textures","Prefabs","Examples"})Directory.CreateDirectory(Folder+"/"+d);
                AssetDatabase.Refresh();
                settings=selected!=null?selected:AssetDatabase.LoadAssetAtPath<JcRoadDetailSettings>(Folder+"/RoadDetail_Default.asset");
                if(settings==null){settings=ScriptableObject.CreateInstance<JcRoadDetailSettings>();settings.treePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RenderFX/Environment/Models/Trees/Prefabs/Tree_02.prefab");AssetDatabase.CreateAsset(settings,Folder+"/RoadDetail_Default.asset");}
                if(settings.roadWidth<1||settings.segmentLength<1||settings.sidewalkWidth<1||settings.sidewalkHeight<=.08f)throw new InvalidOperationException("도로 폭·길이·보도 폭은 1 이상, 보도 높이는 0.08보다 커야 합니다.");
                CreateMaterials();
                CreateParts();
                CreateExample();
                Debug.Log("[JC 도로 디테일] 독립 부품과 Main_1-2 예시 생성 완료. 탐사 원본과 전투씬 미저장.");
            }
            finally{busy=false;}
        }
        static T Save<T>(T obj,string path) where T:Object
        {
            obj.name=Path.GetFileNameWithoutExtension(path);obj.hideFlags=HideFlags.None;
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null){AssetDatabase.CreateAsset(obj,path);return obj;}
            EditorUtility.CopySerialized(obj,old);if(old is Mesh uploaded){uploaded.RecalculateBounds();uploaded.UploadMeshData(false);}Object.DestroyImmediate(obj);EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);return old;
        }
        static float Grain(int x,int y,int salt=0){unchecked{uint h=(uint)(x*374761393+y*668265263+salt*1442695041);h=(h^(h>>13))*1274126177;return (h^(h>>16))/(float)uint.MaxValue;}}
        static Texture2D Texture(string name,Func<int,int,Color> pixel,bool linear=false,bool normal=false)
        {
            const int n=256;var t=new Texture2D(n,n,TextureFormat.RGBA32,true,linear);var colors=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)colors[y*n+x]=pixel(x,y);
            t.SetPixels(colors);t.Apply();string p=Folder+"/Textures/"+name+".png";File.WriteAllBytes(p,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(p);
            var importer=(TextureImporter)AssetImporter.GetAtPath(p);importer.sRGBTexture=!linear;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Compressed;
            if(normal)importer.textureType=TextureImporterType.NormalMap;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        static Material Surface(string name,Texture2D color,Texture2D height,Texture2D normal,float smooth=.1f)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetTexture("_BaseMap",color);m.SetColor("_BaseColor",Color.white);m.SetFloat("_Smoothness",smooth);
            m.SetTexture("_BumpMap",normal);m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.35f);
            m.SetTexture("_ParallaxMap",height);m.SetFloat("_Parallax",settings.parallax);m.EnableKeyword("_PARALLAXMAP");
            return Save(m,Folder+"/Materials/"+name+".mat");
        }
        static void CreateMaterials()
        {
            var h=Texture("Grain_Height",(x,y)=>Color.white*(.38f+.22f*Grain(x,y)),true);
            var n=Texture("Grain_Normal",(x,y)=>new Color(.5f+(Grain(x+1,y)-Grain(x-1,y))*.17f,.5f+(Grain(x,y+1)-Grain(x,y-1))*.17f,1,1),true,true);
            var a=Texture("Asphalt_Color",(x,y)=>{float v=.27f+(Grain(x,y)-.5f)*.08f;return new Color(v*.93f,v*.97f,v,1);});
            asphalt=Surface("Asphalt",a,h,n,.08f);
            var c=Texture("Concrete_Color",(x,y)=>{float v=.79f+(Mathf.PerlinNoise(x*.055f,y*.055f)-.5f)*.035f+(Grain(x,y)-.5f)*.012f;return new Color(v,v,v,1);});
            concrete=Surface("Concrete",c,h,n,.24f);concrete.SetFloat("_BumpScale",.05f);concrete.SetFloat("_Parallax",0);EditorUtility.SetDirty(concrete);AssetDatabase.SaveAssetIfDirty(concrete);
            // 원본 보도 재질과 텍스처를 그대로 복제합니다.
            var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/_ProtoType_Merge/JC/BattleTesting/Resources/JC_Encounters/Main_1-2_Generated/Material_47eb374e1135_0.mat");
            if(source==null)throw new InvalidOperationException("기존 보도 색상 참조 재질이 없습니다.");
            pavingMaterial=Save(new Material(source),Folder+"/Materials/Paving_OriginalColor.mat");
            centerPaint=Surface("CenterLine",Texture("CenterLine_Color",(x,y)=>new Color(.88f,.64f,.13f)*( .95f+.05f*Grain(x,y))),h,n,.15f);
            edgePaint=Surface("EdgeLine",Texture("EdgeLine_Color",(x,y)=>new Color(.82f,.81f,.76f)*( .95f+.05f*Grain(x,y))),h,n,.15f);
            var p=Texture("WornPaint_Color",(x,y)=>{float noise=Grain(x,y,7);float wear=(noise<settings.wear||((x<5||x>250)&&noise<.65f))?1:0;return Color.Lerp(settings.paintColor*(.91f+.09f*noise),new Color(.27f,.28f,.3f),wear);});
            paint=Surface("WornPaint",p,h,n,.2f);
            var earth=Texture("Earth_Color",(x,y)=>Color.Lerp(new Color(.29f,.25f,.18f),new Color(.44f,.39f,.27f),Grain(x,y,2)));
            soil=Surface("Earth",earth,h,n);
            var turf=Texture("Grass_Color",(x,y)=>{float g=Mathf.PerlinNoise(x/27f,y/27f);return Color.Lerp(new Color(.27f,.34f,.16f),new Color(.45f,.49f,.25f),g)*(.9f+.15f*Grain(x,y));});
            grass=Surface("Grass",turf,h,n);
            stone=Surface("Stone",c,h,n);
            metal=new Material(Shader.Find("Universal Render Pipeline/Lit"));metal.SetColor("_BaseColor",new Color(.19f,.21f,.2f));metal.SetFloat("_Metallic",.55f);metal.SetFloat("_Smoothness",.25f);metal=Save(metal,Folder+"/Materials/DrainMetal.mat");
        }
        static Mesh Box(string name,Vector3 size,float radius)
        {
            var vs=new List<Vector3>();var uv=new List<Vector2>();var ns=new List<Vector3>();var ts=new List<int>();
            int steps=4;Vector3 half=size*.5f;radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.95f);var inner=half-Vector3.one*radius;
            foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                var u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;var v=Vector3.Cross(normal,u);int start=vs.Count;
                for(int j=0;j<=steps;j++)for(int i=0;i<=steps;i++)
                {
                    var q=normal+u*(2f*i/steps-1)+v*(2f*j/steps-1);var p=Vector3.Scale(q,half);var core=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));var n=(p-core).normalized;
                    vs.Add(core+n*radius);ns.Add(n);uv.Add(new Vector2(i/(float)steps,j/(float)steps));
                }
                for(int j=0;j<steps;j++)for(int i=0;i<steps;i++){int a=start+j*(steps+1)+i;ts.AddRange(new[]{a,a+1,a+steps+2,a,a+steps+2,a+steps+1});}
            }
            var m=new Mesh();m.SetVertices(vs);m.SetNormals(ns);m.SetUVs(0,uv);m.SetTriangles(ts,0);m.RecalculateBounds();m.RecalculateTangents();return Save(m,Folder+"/Meshes/"+name+".asset");
        }
        static Mesh ContinuousCurb()
        {
            float h=settings.sidewalkHeight*.5f,w=.14f,b=Mathf.Min(settings.rounding,.012f),z=.498f;
            var section=new[]{new Vector2(-w,-h),new Vector2(w,-h),new Vector2(w,h-b),new Vector2(w-b,h),new Vector2(-w+b,h),new Vector2(-w,h-b)};
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            for(int i=0;i<section.Length;i++)
            {
                var a=section[i];var d=section[(i+1)%section.Length];int start=vertices.Count;
                vertices.AddRange(new[]{new Vector3(a.x,a.y,-z),new Vector3(a.x,a.y,z),new Vector3(d.x,d.y,z),new Vector3(d.x,d.y,-z)});
                uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            // 끝면은 둥글게 깎지 않습니다. 1m 간격 배치 시 4mm 이음새만 남습니다.
            foreach(float end in new[]{-z,z}){int start=vertices.Count;foreach(var q in section){vertices.Add(new Vector3(q.x,q.y,end));uv.Add(q);}for(int i=1;i<section.Length-1;i++)if(end>0)indices.AddRange(new[]{start,start+i,start+i+1});else indices.AddRange(new[]{start,start+i+1,start+i});}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();return Save(mesh,Folder+"/Meshes/ContinuousCurbBlock.asset");
        }
        static Mesh FlatSidewalk()
        {
            // 원본은 3m마다 UV 0~1을 사용합니다. Clamp 텍스처도 안전하도록
            // 타일 경계에서 정점을 분리하고 원본 좌표 범위 안에서 반복합니다.
            float w=settings.sidewalkWidth,l=settings.segmentLength,h=settings.sidewalkHeight;
            var vs=new List<Vector3>();var uv=new List<Vector2>();var ts=new List<int>();
            Action<Vector3,Vector3,Vector3,Vector3,Vector2,Vector2> quad=(a,b,c,d,extent,offset)=>{
                int k=vs.Count;vs.AddRange(new[]{a,b,c,d});
                uv.AddRange(new[]{offset,offset+new Vector2(extent.x,0),offset+extent,offset+new Vector2(0,extent.y)});
                ts.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
            };
            for(float x=0;x<w;x+=3)for(float z=0;z<l;z+=3){
                float dx=Mathf.Min(3,w-x),dz=Mathf.Min(3,l-z);
                // U=-Z, V=+X; the original exploration sidewalk orientation.
                quad(new Vector3(x,h,z),new Vector3(x,h,z+dz),new Vector3(x+dx,h,z+dz),new Vector3(x+dx,h,z),new Vector2(-dz/3,dx/3),new Vector2(1,0));
            }
            quad(new Vector3(0,0,0),new Vector3(0,0,l),new Vector3(0,h,l),new Vector3(0,h,0),Vector2.one,Vector2.zero);
            quad(new Vector3(w,0,l),new Vector3(w,0,0),new Vector3(w,h,0),new Vector3(w,h,l),Vector2.one,Vector2.zero);
            quad(new Vector3(w,0,0),new Vector3(0,0,0),new Vector3(0,h,0),new Vector3(w,h,0),Vector2.one,Vector2.zero);
            quad(new Vector3(0,0,l),new Vector3(w,0,l),new Vector3(w,h,l),new Vector3(0,h,l),Vector2.one,Vector2.zero);
            var m=new Mesh();m.SetVertices(vs);m.SetUVs(0,uv);m.SetTriangles(ts,0);m.RecalculateNormals();m.RecalculateBounds();m.RecalculateTangents();
            return Save(m,Folder+"/Meshes/FlatOriginalSidewalk.asset");
        }
        static Mesh Patch(string name,float width,float length,int nx,int nz,Func<float,float,float> height)
        {
            var vs=new List<Vector3>();var uv=new List<Vector2>();var ts=new List<int>();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){float px=width*x/nx,pz=length*z/nz;vs.Add(new Vector3(px,height(px,pz),pz));uv.Add(new Vector2(px*.6f,pz*.6f));}
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int i=z*(nx+1)+x;ts.AddRange(new[]{i,i+nx+1,i+1,i+1,i+nx+1,i+nx+2});}
            var m=new Mesh();m.SetVertices(vs);m.SetUVs(0,uv);m.SetTriangles(ts,0);m.RecalculateNormals();m.RecalculateBounds();m.RecalculateTangents();return Save(m,Folder+"/Meshes/"+name+".asset");
        }
        static GameObject Piece(Transform parent,string name,Mesh mesh,Material material,Vector3 position)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        static GameObject Part(string name,Action<Transform> build)
        {
            var root=new GameObject(name);try{build(root.transform);
                var filters=root.GetComponentsInChildren<MeshFilter>();
                if(filters.Length>1 && filters.All(f=>f.GetComponent<MeshRenderer>().sharedMaterial==filters[0].GetComponent<MeshRenderer>().sharedMaterial))
                {
                    var material=filters[0].GetComponent<MeshRenderer>().sharedMaterial;
                    var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray(),true,true);
                    mesh=Save(mesh,Folder+"/Meshes/"+name+"_Combined.asset");foreach(var f in filters)Object.DestroyImmediate(f.gameObject);Piece(root.transform,name,mesh,material,Vector3.zero);
                }
                return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Prefabs/"+name+".prefab");}finally{Object.DestroyImmediate(root);}
        }
        static GameObject Put(Transform parent,string name,Vector3 position,Quaternion rotation)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Prefabs/"+name+".prefab");var go=(GameObject)PrefabUtility.InstantiatePrefab(source);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;return go;
        }
        static void CreateParts()
        {
            float length=settings.segmentLength;
            var curb=ContinuousCurb();
            Part("Curb_Straight_1m",t=>Piece(t,"Continuous stone curb",curb,concrete,new Vector3(0,settings.sidewalkHeight*.5f,0)));
            var cap=Box("CurbCorner",new Vector3(.38f,settings.sidewalkHeight,.38f),settings.rounding);
            Part("Curb_Corner",t=>Piece(t,"Corner",cap,concrete,new Vector3(0,settings.sidewalkHeight*.5f,0)));
            Part("Curb_End",t=>Piece(t,"End",curb,concrete,new Vector3(0,settings.sidewalkHeight*.5f,0)));
            var road=Patch("RoadStrip",settings.roadWidth,length,1,1,(x,z)=>0);
            Part("Road_Straight",t=>Piece(t,"Asphalt",road,asphalt,Vector3.zero));
            var centerLine=Patch("CenterLineStrip",.16f,length,1,1,(x,z)=>0);
            Part("Lane_Center_Solid",t=>Piece(t,"Yellow center line",centerLine,centerPaint,Vector3.zero));
            var edgeLine=Patch("EdgeLineStrip",.12f,length,1,1,(x,z)=>0);
            Part("Lane_Edge_Solid",t=>Piece(t,"White edge line",edgeLine,edgePaint,Vector3.zero));
            // 원본 텍스처의 줄눈을 사용하며 개별 보도판의 입체 엠보싱은 만들지 않습니다.
            var paving=FlatSidewalk();
            Part("Sidewalk_Straight",t=>Piece(t,"Flat original paving",paving,pavingMaterial,Vector3.zero));
            var ramp=Patch("SidewalkRamp",1.5f,settings.crossingDepth,4,1,(x,z)=>Mathf.Lerp(0,settings.sidewalkHeight,x/1.5f));
            Part("Sidewalk_Ramp",t=>Piece(t,"Sloped crossing access",ramp,pavingMaterial,Vector3.zero));
            var stripe=Patch("CrosswalkStripe",settings.stripeWidth,settings.crossingDepth,1,1,(x,z)=>0);
            Part("Crosswalk",t=>{float step=settings.stripeWidth+settings.stripeGap;float count=Mathf.Floor((settings.roadWidth-.8f)/step);float start=(settings.roadWidth-(count*step-settings.stripeGap))*.5f;for(int i=0;i<count;i++)Piece(t,"Worn paint stripe",stripe,paint,new Vector3(start+i*step,.009f,0));});
            var drain=Box("DrainBar",new Vector3(.38f,.025f,.045f),.006f);
            Part("Drain_1m",t=>{for(int i=0;i<12;i++)Piece(t,"Grating",drain,metal,new Vector3(0,.02f,i*.08f));});
            var shoulder=Patch("EarthShoulder",1.1f,length,8,18,(x,z)=>.015f+Mathf.Sin(x*5+z*2)*.014f);
            Part("Roadside_Earth",t=>Piece(t,"Uneven soil",shoulder,soil,Vector3.zero));
            var hill=Patch("Hillside",22,72,66,144,(x,z)=>HillHeight(x,z));
            Part("Hillside",t=>Piece(t,"Sculpted slope",hill,grass,Vector3.zero));
            var rock=Box("RoundedRock",new Vector3(.6f,.35f,.45f),.15f);
            Part("Roadside_Rocks",t=>{var random=new System.Random(settings.seed);for(int i=0;i<9;i++){var go=Piece(t,"Stone",rock,stone,new Vector3((float)random.NextDouble(),.12f,(float)random.NextDouble()*3));go.transform.localRotation=Quaternion.Euler(0,(float)random.NextDouble()*180,0);go.transform.localScale=Vector3.one*(.25f+(float)random.NextDouble()*.65f);}});
        }
        static float HillHeight(float x,float z){float t=Mathf.Clamp01(x/17);return .02f+Mathf.SmoothStep(0,8,t)+(Mathf.PerlinNoise(x*.28f+settings.seed,z*.18f)-.5f)*Mathf.Min(x*.22f,2);}
        struct Vertex
        {
            public Vector3 p; public Vector2 uv;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),uv=Vector2.Lerp(a.uv,b.uv,t)};
        }
        static List<Vertex> Clip(List<Vertex> polygon,Func<Vector3,float> distance,bool inside)
        {
            var result=new List<Vertex>();if(polygon.Count==0)return result;
            for(int i=0;i<polygon.Count;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Count];float da=distance(a.p),db=distance(b.p);bool ia=inside?da>=0:da<=0,ib=inside?db>=0:db<=0;
                if(ia)result.Add(a);if(ia!=ib)result.Add(Vertex.Lerp(a,b,da/(da-db)));
            }
            return result;
        }
        // 경사로 아래의 기존 바닥과 새 보도판을 실제로 절단하여 경사면이 묻히지 않게 합니다.
        static void CutRampFootprints(Transform root,float edge)
        {
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                bool original=f.name.StartsWith("TileChunk_");
                if(!original)continue;
                var mesh=f.sharedMesh;var source=mesh.vertices;var sourceUv=mesh.uv;var triangles=mesh.triangles;
                var positions=new List<Vector3>();var uvs=new List<Vector2>();var indices=new List<int>();bool changed=false;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var polygons=new List<List<Vertex>>{new List<Vertex>()};for(int j=0;j<3;j++){int id=triangles[i+j];polygons[0].Add(new Vertex{p=f.transform.TransformPoint(source[id]),uv=sourceUv.Length==source.Length?sourceUv[id]:Vector2.zero});}
                    foreach(var cutRange in original?new[]{new Vector2(-36,72)}:new[]{new Vector2(-6,settings.crossingDepth),new Vector2(7,settings.crossingDepth)})
                    {
                        float z=cutRange.x,depth=cutRange.y,width=original?settings.sidewalkWidth+.18f:1.52f;
                        var output=new List<List<Vertex>>();
                        foreach(var polygon in polygons)
                        {
                            var remaining=polygon;
                            foreach(var plane in new Func<Vector3,float>[]{v=>v.x-edge,v=>edge+width-v.x,v=>v.z-z,v=>z+depth-v.z})
                            {var outside=Clip(remaining,plane,false);if(outside.Count>=3)output.Add(outside);remaining=Clip(remaining,plane,true);if(remaining.Count==0)break;}
                            if(remaining.Count>=3)changed=true;
                        }
                        polygons=output;
                    }
                    foreach(var poly in polygons){int start=positions.Count;foreach(var v in poly){positions.Add(f.transform.InverseTransformPoint(v.p));uvs.Add(v.uv);}for(int j=1;j<poly.Count-1;j++)indices.AddRange(new[]{start,start+j,start+j+1});}
                }
                if(!changed)continue;
                var cut=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};cut.SetVertices(positions);cut.SetUVs(0,uvs);cut.SetTriangles(indices,0);cut.RecalculateNormals();cut.RecalculateTangents();cut.RecalculateBounds();
                string cutId=Hash128.Compute(f.name+f.transform.position.ToString("F3")).ToString().Substring(0,12);
                f.sharedMesh=Save(cut,Folder+"/Meshes/ExampleRampCut_"+cutId+".asset");
            }
        }
        static void CreateExample()
        {
            var profile=AssetDatabase.LoadAssetAtPath<JcEncounterBackground>("Assets/_ProtoType_Merge/JC/BattleTesting/Resources/JC_Encounters/Main_1-2_Background.asset");
            if(profile==null||profile.fittedVisualPrefab==null)throw new InvalidOperationException("Main_1-2 도로 보정 배경이 필요합니다.");
            var root=Object.Instantiate(profile.fittedVisualPrefab);root.name="Main_1-2_Detailed";
            try
            {
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(r.name.StartsWith("Mount"))r.gameObject.SetActive(false);
                    if(r.name.Contains("Road_Cross")||r.name.Contains("Road_Centerline"))r.sharedMaterials=r.sharedMaterials.Select(m=>asphalt).ToArray();
                }
                var details=new GameObject("ReusableRoadDetails").transform;details.SetParent(root.transform,false);
                float half=profile.battleRoadWidth*.5f;
                for(int i=0;i<Mathf.CeilToInt(72/settings.segmentLength);i++)
                {
                    float z=-36+i*settings.segmentLength;Put(details,"Road_Straight",new Vector3(-half,.055f,z),Quaternion.identity).transform.localScale=new Vector3(profile.battleRoadWidth/settings.roadWidth,1,1);
                    Put(details,"Sidewalk_Straight",new Vector3(half+.18f,.055f,z),Quaternion.identity);
                    Put(details,"Lane_Center_Solid",new Vector3(-.08f,.067f,z),Quaternion.identity);
                    Put(details,"Lane_Edge_Solid",new Vector3(-half+.32f,.067f,z),Quaternion.identity);
                    Put(details,"Lane_Edge_Solid",new Vector3(half-.44f,.067f,z),Quaternion.identity);
                    Put(details,"Roadside_Earth",new Vector3(-half-1.1f,.056f,z),Quaternion.identity);
                    if(i%2==0)Put(details,"Roadside_Rocks",new Vector3(-half-1.6f,.06f,z),Quaternion.identity);
                }
                for(int z=-36;z<36;z++)
                {
                    Put(details,"Curb_Straight_1m",new Vector3(half+.04f,.055f,z+.5f),Quaternion.identity);
                    Put(details,"Curb_Straight_1m",new Vector3(-half-.15f,.055f,z+.5f),Quaternion.identity);
                    if(z%6==0)Put(details,"Drain_1m",new Vector3(half-.27f,.06f,z),Quaternion.identity);
                }
                Put(details,"Hillside",new Vector3(-half-1.1f,.055f,36),Quaternion.Euler(0,180,0));
                if(settings.treePrefab!=null)
                {
                    var random=new System.Random(settings.seed);
                    for(int i=0;i<10;i++)
                    {
                        var tree=(GameObject)PrefabUtility.InstantiatePrefab(settings.treePrefab);tree.transform.SetParent(details,false);
                        float x=4+(float)random.NextDouble()*5,z=8+i*5.6f;
                        var rs=tree.GetComponentsInChildren<Renderer>();if(rs.Length==0){Object.DestroyImmediate(tree);continue;}
                        var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
                        float sc=settings.treeHeight/Mathf.Max(.1f,bounds.size.y);tree.transform.localScale*=sc;
                        tree.transform.localPosition=new Vector3(-half-1.1f-x,HillHeight(x,z)+.06f,36-z);
                        tree.transform.localRotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                        var placed=rs[0].bounds;foreach(var r in rs)placed.Encapsulate(r.bounds);
                        tree.transform.position+=Vector3.up*(HillHeight(x,z)+.06f-placed.min.y);
                        foreach(var c in tree.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
                        foreach(var c in tree.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(c);
                    }
                }
                CutRampFootprints(root.transform,half);
                profile.detailVisualPrefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Examples/Main_1-2_Detailed.prefab");profile.useDetailBackground=true;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
