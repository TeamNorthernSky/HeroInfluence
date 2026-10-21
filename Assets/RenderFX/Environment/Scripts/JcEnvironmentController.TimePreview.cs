using UnityEngine;
using UnityEngine.Rendering;
namespace JC.Env
{
    public partial class JcEnvironmentController
    {
        [SerializeField,InspectorName("시간대 미리보기 설정"),Tooltip("게임 시작 시각과 수동 시간 연출의 별도 설정입니다. 기존 낮·밤 프로파일과 물 설정은 수정하지 않습니다.")]
        private JcTimePreviewSettings timePreviewSettings;
        public JcTimePreviewSettings TimePreviewSettings {get=>timePreviewSettings;set=>timePreviewSettings=value;}
        public bool IsTimePreviewActive=>previewSky!=null;
        public float PreviewHour {get;private set;}=12;
        Material previewSky,previousSky;
        Light previewLight,previousSun;
        Color previousColor;
        Quaternion previousRotation;
        float previousIntensity,previousAmbient,previousReflection;
        AmbientMode previousAmbientMode;
        SphericalHarmonicsL2 previousProbe;
        float lastEnvironmentUpdate=-100;

        public bool BeginTimePreview()
        {
            if(IsTimePreviewActive)return true;
            var s=timePreviewSettings;
            if(!CanApply||s==null||s.day==null||s.night==null||s.skyTemplate==null)return false;
            previewLight=ResolveLight();if(previewLight==null)return false;
            previousSky=RenderSettings.skybox;previousSun=RenderSettings.sun;previousColor=previewLight.color;previousIntensity=previewLight.intensity;previousRotation=previewLight.transform.rotation;
            previousAmbient=RenderSettings.ambientIntensity;previousReflection=RenderSettings.reflectionIntensity;previousAmbientMode=RenderSettings.ambientMode;previousProbe=RenderSettings.ambientProbe;
            previewSky=new Material(s.skyTemplate){name="JC Time Preview (temporary)",hideFlags=HideFlags.HideAndDontSave};
            RenderSettings.skybox=previewSky;RenderSettings.sun=previewLight;lastEnvironmentUpdate=-100;
            return true;
        }
        public void SetPreviewHour(float hour,bool refreshEnvironment=true)
        {
            var settings=timePreviewSettings;
            if(settings==null||settings.day==null||settings.night==null||settings.skyTemplate==null){StopTimePreview();return;}
            if(!BeginTimePreview())return;
            PreviewHour=Mathf.Repeat(hour,24);var s=timePreviewSettings;var v=s.Evaluate(PreviewHour);
            previewSky.SetFloat("_Rotation",s.landscapeRotation);previewSky.SetVector("_Weights",v.weights);previewSky.SetVector("_SunDirection",v.sun);previewSky.SetVector("_MoonDirection",v.moon);
            previewSky.SetFloat("_CloudSpeed",s.day.skyCloudSpeed);previewSky.SetFloat("_CloudOffset",s.day.skyCloudOffset);previewSky.SetFloat("_CloudOpacity",s.day.skyCloudOpacity);
            previewSky.SetFloat("_DayExposure",s.day.skyExposure);previewSky.SetFloat("_NightExposure",s.night.skyExposure);
            previewLight.transform.rotation=v.lightRotation;previewLight.color=v.lightColor;previewLight.intensity=v.lightIntensity;
            RenderSettings.ambientMode=AmbientMode.Skybox;RenderSettings.ambientIntensity=v.ambient;RenderSettings.reflectionIntensity=v.reflection;
            // 환경광·반사 재계산 요청을 매 프레임 누적하지 않습니다.
            if(refreshEnvironment||Time.realtimeSinceStartup-lastEnvironmentUpdate>.35f){DynamicGI.UpdateEnvironment();lastEnvironmentUpdate=Time.realtimeSinceStartup;}
        }
        public void StopTimePreview()
        {
            if(!IsTimePreviewActive)return;
            if(RenderSettings.skybox==previewSky){RenderSettings.skybox=previousSky;RenderSettings.sun=previousSun;RenderSettings.ambientMode=previousAmbientMode;RenderSettings.ambientIntensity=previousAmbient;RenderSettings.reflectionIntensity=previousReflection;RenderSettings.ambientProbe=previousProbe;DynamicGI.UpdateEnvironment();}
            if(previewLight!=null){previewLight.color=previousColor;previewLight.intensity=previousIntensity;previewLight.transform.rotation=previousRotation;}
            var old=previewSky;previewSky=null;if(Application.isPlaying)Destroy(old);else DestroyImmediate(old);
        }
    }
}
