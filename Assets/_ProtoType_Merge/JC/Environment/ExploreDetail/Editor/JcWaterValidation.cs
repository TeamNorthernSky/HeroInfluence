using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace JC.EnvironmentDetail.Editor
{
    public static class JcWaterValidation
    {
        public static string Run(JcExploreDetailOverlay overlay)
        {
            if(Application.isPlaying)throw new InvalidOperationException("편집 모드 검사입니다.");
            var water=JcWaterController.Ensure(overlay); var saved=water.Settings.Copy(); bool paused=water.Paused;
            var sourceProfile=EditorJsonUtility.ToJson(water.Profile);
            var bake=overlay.SelectedBake; string bakeSettings=bake.settingsSnapshot;
            var bakeCheck=JcExploreDetailBaker.Check(overlay);
            var root=overlay.VisualRoot; var renderers=root.GetComponentsInChildren<MeshRenderer>();
            var meshes=root.GetComponentsInChildren<MeshFilter>().Select(x=>x.sharedMesh).ToArray();
            var banks=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null && m.shader.name=="JC/Environment/Canal Bank").ToArray();
            var originalWater=bake.prefab.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).First(m=>m.shader.name=="JC/Environment/Calm Canal Water");
            var originalJson=EditorJsonUtility.ToJson(originalWater); var report=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);count++;report.AppendLine("PASS "+label);};
            Func<Material> live=()=>overlay.VisualRoot.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).First(water.Owns);
            try
            {
                check(water.RendererCount>0 && water.MaterialCount==1,"수면 렌더러와 공통 물 재질 연결");
                water.Settings.baseColor=Color.magenta;water.Settings.highlightStrength=2.3f;water.Settings.flowSpeed=.21f;water.Apply();
                check(live().GetColor("_BaseColor")==Color.magenta && Mathf.Approximately(live().GetVector("_Specular").x,2.3f),"물색·광원 하이라이트 즉시 갱신");
                check(Mathf.Approximately(live().GetVector("_Flow").z,.21f),"흐름 속도 즉시 갱신");
                water.Settings.edgeVariation=.6f;water.Settings.edgeVariationSize=4;water.Apply();
                check(Vector4.Distance(live().GetVector("_EdgeVariation"),new Vector4(.6f,4,0,0))<.0001f,"물가색 불규칙성 보존");
                check(!live().HasProperty("_Streak") && !live().HasProperty("_StreakPattern"),"스트레이크 셰이더 경로 제거");
                check(live().GetTexture("_NormalBroad")!=null && live().GetTexture("_NormalDetail")!=null,"큰 물결·잔물결 노멀맵 연결");
                water.Settings.broadNormalStrength=.31f;water.Settings.detailNormalStrength=.17f;water.Settings.detailNormalSize=.42f;water.Settings.rippleSpeed=.09f;water.Apply();
                check(Vector4.Distance(live().GetVector("_Normals"),new Vector4(.31f,.17f,.42f,.09f))<.0001f,"두 노멀 강도·크기·속도 실시간 반영");
                check(!live().HasProperty("_ReflectionWave"),"분리된 반사 전용 물결 설정 제거");
                check(live().GetTexture("_FlowMap").name=="JC constrained canal flow","확정 메시 기반 흐름 보정 연결");
                var waterMap=live().GetTexture("_FlowMap") as Texture2D;
                check(waterMap.format==TextureFormat.RGBAHalf,"흐름 지도 수면 마스크 연결");
                var waterPixels=waterMap.GetPixels();
                check(waterPixels.Any(c=>c.b>.9f)&&waterPixels.Any(c=>c.b<.1f),"수면 마스크가 물과 육지를 구분");
                var wet=new bool[225];var raw=Enumerable.Repeat(new Color(.8f,.9f,0,1),225).ToArray();
                for(int y=0;y<15;y++)for(int x=0;x<15;x++)wet[y*15+x]=(x>=6&&x<=8&&y<=13)||(y>=11&&y<=13);
                var fixedFlow=JcWaterFlowConstraint.Constrain(raw,wet,15,15);
                check(Mathf.Approximately(fixedFlow[5*15+7].r,.5f)&&fixedFlow[5*15+7].g==1,"T자 진입부 수로 평행 유지");
                check(fixedFlow[12*15+2].r==1&&Mathf.Approximately(fixedFlow[12*15+2].g,.5f),"T자 가로 수로 평행 유지");
                check(fixedFlow[12*15+7].r>.5f&&fixedFlow[12*15+7].g>.5f,"T자 교차 내부 합류 방향 유지");
                check(raw.All(c=>c==new Color(.8f,.9f,0,1)),"흐름 지도 원본 배열 불변");
                water.Settings.reflection=.72f;water.Settings.reflectionClarity=.8f;water.Settings.reflectionDistortion=.02f;water.Apply();
                check(Mathf.Approximately(live().GetVector("_Reflection").x,.72f),"반사 강도 적용");
                check(!live().HasProperty("_Highlight") && !live().HasProperty("_CrossFlow") && !live().HasProperty("_ShoreHighlight"),"곡선·횡방향·물가 하이라이트 셰이더 설정 제거");
                check(!typeof(JcWaterSettings).GetFields().Any(f=>f.Name.StartsWith("streak") || f.Name.StartsWith("shore") || f.Name.StartsWith("morph")),"폐기된 인스펙터 필드 제거");
                water.Settings.waveShading=.47f;water.Settings.waveShadeSize=2.3f;water.Settings.waveShadeSoftness=.8f;water.Apply();
                check(Vector4.Distance(live().GetVector("_WaveShade"),new Vector4(.47f,2.3f,.8f,0))<.0001f,"음영 조절 즉시 갱신");
                water.Settings.waveShading=0;water.Apply();
                check(live().GetVector("_WaveShade").x==0 && Mathf.Approximately(live().GetVector("_Specular").x,2.3f),"음영만 끄면 직접광 하이라이트 보존");
                water.Settings.highlightRoughness=.45f;water.Settings.highlightSoftness=.7f;water.Settings.sparkleStrength=.8f;water.Apply();
                check(Vector4.Distance(live().GetVector("_Specular"),new Vector4(2.3f,.45f,.7f,.8f))<.0001f,"하이라이트 강도·폭·부드러움·잔반짝임 전달");
                water.Settings.reflection=0;water.Apply();
                check(Mathf.Approximately(live().GetVector("_Specular").x,2.3f),"환경 반사를 꺼도 직접광 하이라이트 유지");
                water.Settings.reflection=.72f;water.Settings.highlightStrength=0;water.Apply();
                check(live().GetVector("_Specular").x==0 && Mathf.Approximately(live().GetVector("_Reflection").x,.72f),"직접광 하이라이트를 꺼도 환경 반사 유지");
                var copied=water.Settings.Copy();
                check(copied.highlightStrength==0 && copied.highlightRoughness==.45f && copied.highlightSoftness==.7f && copied.sparkleStrength==.8f,"하이라이트 프로필 직렬화·복사");
                check(root==overlay.VisualRoot && meshes.SequenceEqual(root.GetComponentsInChildren<MeshFilter>().Select(x=>x.sharedMesh)),"재생성 없이 동일 메시 유지");
                check(banks.SequenceEqual(renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null && m.shader.name=="JC/Environment/Canal Bank")),"호안 슬롯 보존");
                check(originalJson==EditorJsonUtility.ToJson(originalWater),"베이크 원본 재질 불변");
                check(sourceProfile==EditorJsonUtility.ToJson(water.Profile),"임시 조절은 저장 프로필 불변");
                check(bakeSettings==bake.settingsSnapshot && JcExploreDetailBaker.Check(overlay)==bakeCheck,"물 조절은 기존 베이크 판정 유지");
                water.Settings.followCanal=false;water.Apply();check(live().GetFloat("_FollowCanal")==0,"공통 방향 전환");
                water.Settings.followCanal=true;water.Apply();check(live().GetFloat("_FollowCanal")==1,"베이크 방향 지도 재사용");
                var beforeDisable=live();water.enabled=false;
                check(water.MaterialCount==0 && beforeDisable==null,"비활성화 시 임시 재질 해제");
                check(renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null && m.shader.name=="JC/Environment/Calm Canal Water").All(EditorUtility.IsPersistent),"비활성화 시 영구 재질 복원");
                water.enabled=true;check(water.RendererCount>0 && live().GetColor("_BaseColor")==Color.magenta,"재활성화 시 설정 재적용");
                var old=live();overlay.Refresh();
                check(old==live() && water.RendererCount>0 && live().GetColor("_BaseColor")==Color.magenta,"같은 베이크 재표시는 물 재질·임시 설정 보존");
                water.LoadSaved();check(water.Settings.baseColor==water.Profile.settings.baseColor,"저장값 불러오기");
                check(live().renderQueue==(int)RenderQueue.Transparent,"수면 투명 렌더 큐");
                water.Settings.surfaceOpacity=.42f;water.Settings.depthAbsorption=.7f;water.Apply();
                check(Vector4.Distance(live().GetVector("_Transmission"),new Vector4(.42f,.7f,0,0))<.0001f,"수면 투과·수심 흡수 실시간 적용");
                var u=water.Underwater;check(u!=null&&u.WetCount>0&&u.HasRoute,"확정 수면 범위와 수중 이동 경로 생성");
                bool inWater=true;for(int n=0;n<=64;n++)
                {
                    u.Tick(water.Settings,n*4);var pos=u.Capsule.position;
                    inWater&=u.Contains(pos)&&u.Contains(pos+Vector3.right*.3f)&&u.Contains(pos-Vector3.right*.3f)&&u.Contains(pos+Vector3.forward*1.1f)&&u.Contains(pos-Vector3.forward*1.1f);
                    inWater&=pos.y+.21f<u.RouteA.y;
                }
                check(inWater,"왕복 전 구간 캡슐이 수면 아래·수로 안에 유지");
                check(u.Capsule.GetComponent<Collider>()==null,"실루엣 시제품 게임 충돌 없음");
                water.Settings.underwaterCapsule=false;water.Apply();check(!u.Capsule.gameObject.activeSelf,"캡슐 표시 토글");
                water.Settings.underwaterCapsule=true;water.Apply();check(u.Capsule.gameObject.activeSelf,"캡슐 재표시");
                water.enabled=false;check(water.Underwater==null,"비활성 시 수중 공간 해제");water.enabled=true;
                CheckReflection(water,renderers.First(r=>r.sharedMaterials.Any(water.Owns)),check);
                check(!ShaderUtil.GetShaderMessages(originalWater.shader).Any(m=>m.severity.ToString()=="Error"),"수면 셰이더 컴파일");
                report.Insert(0,$"물 비플레이 검사 {count}개 통과\n");return report.ToString();
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(saved),water.Settings);water.Paused=paused;water.enabled=true;water.Apply();
            }
        }
        static void CheckReflection(JcWaterController water,Renderer surface,Action<bool,string> check)
        {
            var go=new GameObject("JC reflection validation"){hideFlags=HideFlags.HideAndDontSave};
            var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=surface.bounds.center+new Vector3(3,6,-8);
            camera.transform.LookAt(surface.bounds.center);camera.farClipPlane=200;camera.aspect=1;
            var target=new RenderTexture(64,64,24);target.Create();
            bool culling=GL.invertCulling;float pass=Shader.GetGlobalFloat("_JcWaterReflectionPass");
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            var block=new MaterialPropertyBlock();
            try
            {
                water.Settings.planarReflection=true;water.Settings.reflection=.7f;water.Apply();
                RenderPipeline.SubmitRenderRequest(camera,request);
                surface.GetPropertyBlock(block);
                check(water.ReflectionViewCount>0 && block.GetFloat("_PlanarAvailable")==1 && block.GetTexture("_PlanarReflection")!=null,"원근 카메라 반사 영상 생성·수면 연결");
                camera.orthographic=true;camera.orthographicSize=8;
                RenderPipeline.SubmitRenderRequest(camera,request);
                check(GL.invertCulling==culling && Shader.GetGlobalFloat("_JcWaterReflectionPass")==pass,"직교 카메라 렌더·전역 렌더 상태 복원");
                water.Settings.planarReflection=false;water.Apply();
                surface.GetPropertyBlock(block);
                check(water.ReflectionViewCount==0 && block.GetFloat("_PlanarAvailable")==0,"반사 끄기 시 카메라·텍스처 해제 및 바인딩 제거");
                water.Settings.planarReflection=true;water.Apply();RenderPipeline.SubmitRenderRequest(camera,request);
                water.enabled=false;
                check(water.ReflectionViewCount==0,"컴포넌트 비활성화 시 반사 리소스 해제");
                water.enabled=true;
            }
            finally {camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
