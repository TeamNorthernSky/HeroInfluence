using System;
using System.Collections.Generic;
using System.Linq;
using JC.Env;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-17 | 보스 공유 프리팹에 실물 창틀 메시와 월드 산 외형을 추가합니다.
    // 원본 유리/도시/씬 저장값은 보존하고 새 생성물만 갱신합니다. 열린 씬 저장·리로드는 수행하지 않습니다.
    public static class JcBossBackdropBuilder
    {
        const string Folder="Assets/_ProtoType_Merge/JC/Environment/BossStage/Backdrop";
        const string FrameName="JC_WindowFrames";
        const string MountainName="도시 원경 · 차폐 산";
        static string StagePath => AssetDatabase.GetAssetPath(JcBossBattleBackground.Load().visualPrefab);
        static void FolderReady()
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_ProtoType_Merge/JC/Environment/BossStage","Backdrop");
        }
        static T Save<T>(T value,string name) where T:UnityEngine.Object
        {
            var path=Folder+"/"+name;var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old)
            {
                // JC | 2026-11-17 | 메시를 직렬화 복사만 하면 열린 씬의 GPU 버퍼가 이전 창틀을 표시하므로 메시 API로 함께 갱신합니다.
                if(value is Mesh source && old is Mesh target)
                {
                    target.Clear();target.indexFormat=source.indexFormat;target.vertices=source.vertices;
                    target.normals=source.normals;target.uv=source.uv;target.triangles=source.triangles;
                    target.bounds=source.bounds;target.name=source.name;
                }
                else EditorUtility.CopySerialized(value,old);
                UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);return old;
            }
            AssetDatabase.CreateAsset(value,path);return value;
        }
        static void RemoveOwned(GameObject stage,string name)
        {
            var prior=stage.transform.Find(name);if(prior)UnityEngine.Object.DestroyImmediate(prior.gameObject);
        }
        public static string BuildFrames()
        {
            FolderReady();var settings=JcBossBattleBackground.Load();var stage=PrefabUtility.LoadPrefabContents(StagePath);
            try
            {
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
                var panes=stage.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial && r.sharedMaterial.shader==settings.glassProfile.shader).ToArray();
                if(panes.Length!=6)throw new InvalidOperationException("기존 유리 6개 연결을 먼저 확인하세요.");
                int bars=0;
                foreach(var pane in panes)
                {
                    var bounds=pane.GetComponent<MeshFilter>().sharedMesh.bounds;
                    int thin=bounds.size.x<bounds.size.z?0:2, horizontal=thin==0?2:0;
                    var scale=pane.transform.lossyScale;
                    // JC | 2026-11-17 | 사용자 이미지 검수에 따라 외곽·내부 살의 폭과 돌출 두께를 기존의 절반으로 줄입니다.
                    float rim=.09f/Mathf.Abs(scale[horizontal]),rail=.09f/Mathf.Abs(scale.y),depth=.15f/Mathf.Abs(scale[thin]);
                    var matrix=stage.transform.worldToLocalMatrix*pane.transform.localToWorldMatrix;
                    // JC | 2026-11-17 | 유리 바깥 경계 4변은 유지합니다. 유리 메시의 크기는 바꾸지 않습니다.
                    Action<Vector3,Vector3> box=(c,s)=>{Box(c,s,matrix,vertices,normals,uv,triangles);bars++;};
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        var c=bounds.center;c[horizontal]+=sign*(bounds.extents[horizontal]-rim*.5f);
                        var size=bounds.size;size[thin]=depth;size[horizontal]=rim;box(c,size);
                        c=bounds.center;c.y+=sign*(bounds.extents.y-rail*.5f);
                        size=bounds.size;size[thin]=depth;size.y=rail;size[horizontal]-=2*rim;box(c,size);
                    }
                    // JC | 2026-11-17 | X측 긴 창은 이미지의 1·3·5번째 살을 삭제해 2·4번째만 유지합니다.
                    // X측 -Z 끝 짧은 창(GlassBay1)과 Z측 맨 오른쪽 창(GlassBay6)은 내부 세로살을 없앱니다.
                    // Z측 긴 창은 사용자 확인대로 세로살 2개/유리 3칸으로 통일합니다. 산 배치는 이번 수정에서 건드리지 않습니다.
                    int divisions=pane.name=="GlassBay1" || pane.name=="GlassBay6" ? 1 : 3;
                    for(int i=1;i<divisions;i++)
                    {
                        var c=bounds.center;c[horizontal]=Mathf.Lerp(bounds.min[horizontal],bounds.max[horizontal],i/(float)divisions);
                        var size=bounds.size;size[thin]=depth*.8f;size[horizontal]=rim*.65f;size.y-=2*rail;box(c,size);
                    }
                    // JC | 2026-11-17 | 사용자 후속 요청으로 상단 내부 가로살은 생성하지 않습니다.
                }
                var mesh=new Mesh{name="JC 보스 창틀 · 6개 창 연속 프레임"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
                mesh=Save(mesh,"WindowFrames.asset");
                RemoveOwned(stage,FrameName);var root=new GameObject(FrameName);root.transform.SetParent(stage.transform,false);
                root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();
                // JC | 2026-11-17 | 재생성해도 몰딩 공용 재질로 돌아가지 않도록 창틀 전용 에셋을 사용합니다.
                renderer.sharedMaterial=WindowFrameMaterial();
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                PrefabUtility.SaveAsPrefabAsset(stage,StagePath);
                return $"유리 {panes.Length}개 보존; 창틀 살 {bars}개; vertices={vertices.Count}; triangles={triangles.Count/3}";
            }
            finally { PrefabUtility.UnloadPrefabContents(stage); }
        }
        // JC | 2026-11-17 | 현재 금속색/광택을 최초 한 번 복제하고 이후 전용 재질의 사용자 설정을 보존합니다.
        public static Material WindowFrameMaterial()
        {
            FolderReady();
            const string path = Folder + "/WindowFrameMetal.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/_ProtoType_Merge/JC/Environment/BossStage/WindowPilot/WindowMetal.mat");
            if (!original) throw new InvalidOperationException("창틀 초기 금속 재질 누락");
            material = new Material(original) { name = "WindowFrameMetal" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        static void Box(Vector3 center,Vector3 size,Matrix4x4 matrix,List<Vector3> vertices,List<Vector3> normals,List<Vector2> uv,List<int> triangles)
        {
            var ext=size*.5f;
            foreach(var n in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                var u=Mathf.Abs(n.y)>.5f?Vector3.right:Vector3.up;var v=Vector3.Cross(n,u);int first=vertices.Count;
                foreach(var p in new[]{-u-v,u-v,u+v,-u+v}) {vertices.Add(matrix.MultiplyPoint3x4(center+Vector3.Scale(n+p,ext)));normals.Add(matrix.MultiplyVector(n).normalized);}
                uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
            }
        }
        public static string BuildExterior()
        {
            FolderReady();var settings=JcBossBattleBackground.Load();var stage=PrefabUtility.LoadPrefabContents(StagePath);
            try
            {
                if(!stage.transform.Find(FrameName))throw new InvalidOperationException("창틀 작업을 먼저 완료해야 합니다.");
                // JC | 2026-11-17 | Zone1/Level1-A에서 실제 사용 중인 Mt04 메시·재질을 재사용합니다. 배치/충돌 스크립트는 복제하지 않습니다.
                var mountain=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ProtoType_Merge/DH/AlphaAsset/DecoAsset/Mountain/Mt04 (S1)SM_Nature_Landscape_08.prefab");
                if(!mountain)throw new InvalidOperationException("월드 산 Mt04 원본 누락");
                var filter=mountain.GetComponentInChildren<MeshFilter>();var material=filter.GetComponent<MeshRenderer>().sharedMaterial;
                RemoveOwned(stage,MountainName);var group=new GameObject(MountainName);group.transform.SetParent(stage.transform,false);
                var time=group.AddComponent<JcBossCityTime>();time.profile=settings.cityTimeProfile;
                // JC | 2026-11-17 | 연장 바닥까지 포함한 실내 외곽에서 산의 안쪽 면을 16 무대 단위 이상 띄웁니다.
                // -X/-Z 축은 고정하고 기존 +방향 도시 전망과 사용자 도시 오프셋은 유지합니다.
                var room=InteriorBounds(stage.transform);
                AddMountain(group.transform,filter,material,"산 -X",new Vector3(room.min.x-60,-18,room.center.z),90);
                AddMountain(group.transform,filter,material,"산 -Z",new Vector3(room.center.x,-18,room.min.z-60),0);
                var path=Folder+"/JC_BossDistantDOF.asset";
                var profile=AssetDatabase.LoadAssetAtPath<JcDofProfile>(path);
                if(!profile)
                {
                    profile=ScriptableObject.CreateInstance<JcDofProfile>();profile.settings=JcDofSettings.Default;
                    var s=profile.settings;s.mode=DepthOfFieldMode.Gaussian;s.autoFocus=false;s.gaussianStart=80;s.gaussianEnd=180;s.gaussianMaxRadius=1.3f;s.highQualitySampling=true;profile.settings=s;
                    AssetDatabase.CreateAsset(profile,path);
                }
                var dof=stage.transform.Find("JC_BossDistantDOF");
                if(!dof){dof=new GameObject("JC_BossDistantDOF").transform;dof.SetParent(stage.transform,false);}
                var controller=dof.GetComponent<JcBossDistantDof>();if(!controller)controller=dof.gameObject.AddComponent<JcBossDistantDof>();controller.Source=profile;
                PrefabUtility.SaveAsPrefabAsset(stage,StagePath);
                return "월드 Mt04 산 2개(-X/-Z) 및 보스 전용 Gaussian DOF 프로필/조절기 연결 완료";
            }
            finally { PrefabUtility.UnloadPrefabContents(stage); }
        }
        static void AddMountain(Transform parent,MeshFilter source,Material material,string name,Vector3 bottomCenter,float yaw)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            // JC | 2026-11-17 | 원본 모델의 오프센터 피벗을 보정합니다. 400m 폭·약 78m 높이의 산으로 두 음수 방향을 겹쳐 막습니다.
            var sourceScale=source.transform.lossyScale;
            var scale=new Vector3(Mathf.Abs(sourceScale.x),Mathf.Abs(sourceScale.y),Mathf.Abs(sourceScale.z))*8;
            go.transform.localScale=scale;
            var bounds=source.sharedMesh.bounds;var bottom=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            go.transform.localPosition=bottomCenter-go.transform.localRotation*Vector3.Scale(bottom,scale);
            go.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
        }
        // JC | 2026-11-17 | 프리팹 루트의 배율·이동과 무관하게 월드 Bounds를 무대 좌표로 돌려 차폐 배치를 계산합니다.
        static Bounds InteriorBounds(Transform stage)
        {
            bool first=true;var result=new Bounds();
            foreach(var renderer in stage.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.GetComponentInParent<JcBossCityTime>())continue;
                var b=renderer.bounds;
                for(int i=0;i<8;i++)
                {
                    var point=stage.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
                }
            }
            if(first)throw new InvalidOperationException("실내 메시 경계가 없습니다.");
            return result;
        }
    }
}
