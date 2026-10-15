using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
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
                water.Settings.baseColor=Color.magenta;water.Settings.foamDensity=.73f;water.Settings.flowSpeed=.21f;water.Apply();
                check(live().GetColor("_BaseColor")==Color.magenta && Mathf.Approximately(live().GetVector("_Foam").y,.73f),"물색·포말 즉시 갱신");
                check(Mathf.Approximately(live().GetVector("_Flow").z,.21f),"흐름 속도 즉시 갱신");
                water.Settings.waveShading=.47f;water.Settings.waveShadeSize=2.3f;water.Settings.waveShadeSoftness=.8f;water.Settings.foamCurvature=1.4f;water.Apply();
                check(Vector4.Distance(live().GetVector("_WaveShade"),new Vector4(.47f,2.3f,.8f,0))<.0001f && Mathf.Approximately(live().GetFloat("_FoamCurvature"),1.4f),"음영·곡선 조절 즉시 갱신");
                var copied=water.Settings.Copy();
                check(Mathf.Approximately(copied.waveShading,.47f) && Mathf.Approximately(copied.foamCurvature,1.4f),"설정 복사에 음영·곡선 포함");
                water.Settings.waveShading=0;water.Apply();
                check(live().GetVector("_WaveShade").x==0 && Mathf.Approximately(live().GetVector("_Foam").y,.73f),"음영만 끄면 포말 설정 보존");
                water.Settings.foamOpacity=.4f;water.Settings.foamFade=.3f;water.Apply();
                check(Mathf.Approximately(live().GetFloat("_FoamOpacity"),.4f) && Mathf.Approximately(live().GetFloat("_FoamFade"),.3f),"포말 투명도·소멸 보간 즉시 반영");
                copied=water.Settings.Copy();
                check(Mathf.Approximately(copied.foamOpacity,.4f) && Mathf.Approximately(copied.foamFade,.3f),"포말 투명도·소멸 보간 저장 복사");
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
                check(!ShaderUtil.GetShaderMessages(originalWater.shader).Any(m=>m.severity.ToString()=="Error"),"수면 셰이더 컴파일");
                report.Insert(0,$"물 비플레이 검사 {count}개 통과\n");return report.ToString();
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(saved),water.Settings);water.Paused=paused;water.enabled=true;water.Apply();
            }
        }
    }
}
