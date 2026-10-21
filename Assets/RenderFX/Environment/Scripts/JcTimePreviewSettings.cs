using UnityEngine;
namespace JC.Env
{
    [CreateAssetMenu(menuName="JC Environment/시간대 연출 설정",fileName="ENV_TimePreview")]
    public sealed class JcTimePreviewSettings : ScriptableObject
    {
        [InspectorName("낮 기준 프로파일"),Tooltip("낮 조명의 기준값을 읽습니다. 미리보기는 원본을 수정하지 않습니다.")] public JcEnvironmentProfile day;
        [InspectorName("밤 기준 프로파일"),Tooltip("밤 조명의 기준값을 읽습니다. 미리보기는 원본을 수정하지 않습니다.")] public JcEnvironmentProfile night;
        [InspectorName("시간대 전용 하늘 재질"),Tooltip("천체가 분리된 시간대 전용 하늘 재질. 복제본만 사용합니다.")] public Material skyTemplate;
        [InspectorName("고정 풍경 회전"),Tooltip("낮 풍경의 기존 방위입니다. 태양 이동과 무관하게 고정됩니다.")] public float landscapeRotation;
        [InspectorName("동쪽 월드 방향"),Tooltip("기준 탐사 카메라의 화면 오른쪽입니다. 카메라 이동과 무관하게 유지합니다.")] public Vector3 east=Vector3.right;
        [InspectorName("정오 태양 최대 고도"),Range(20,80),Tooltip("천체 궤적의 정오 높이입니다. 라이트 고도차는 별도로 더합니다.")] public float maximumElevation=45;
        [InspectorName("태양 라이트 고도차"),Range(-40,40),Tooltip("하늘 태양보다 직접광을 높게/낮게 배치하는 의도적 고도차입니다.")] public float sunLightAltitudeOffset;
        [InspectorName("달 라이트 고도차"),Range(-40,40),Tooltip("밤 프로파일에서 유지할 달과 직접광의 고도차입니다.")] public float moonLightAltitudeOffset;
        [InspectorName("일출 시각"),Range(4,7),Tooltip("태양이 지평선을 통과하는 시각입니다. 기본 05:45입니다.")] public float sunrise=5.75f;
        [InspectorName("일몰 시각"),Range(17,20),Tooltip("태양이 지평선을 통과하는 시각입니다. 기본 17:45로, 18시는 초저녁입니다.")] public float sunset=17.75f;
        [InspectorName("6시간 연출 길이"),Range(1,10),Tooltip("다음 6시간으로 이동하는 미리보기의 실제 재생 시간(초)입니다.")] public float transitionSeconds=3;
        [InspectorName("여명·석양 직접광"),Tooltip("낮은 태양의 따뜻한 빛 색입니다.")] public Color warmLight=new Color(1,.36f,.14f);
        [InspectorName("여명·석양 환경광 배율"),Range(0,2),Tooltip("전용 여명·석양 배경에서 생성하는 환경광의 세기입니다.")] public float twilightAmbient=.65f;

        [InspectorName("게임 시작 시각"),Range(0,24),Tooltip("시간대 설정이 연결된 탐사 씬의 시작 시각입니다. 수동 미리보기도 이 시각에서 시작합니다.")] public float startHour=12;

        public struct Sample {public Vector3 sun,moon,weights;public Quaternion lightRotation;public Color lightColor;public float lightIntensity,ambient,reflection;}
        static float Smooth(float a,float b,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));
        public Sample Evaluate(float hour)
        {
            float h=Mathf.Repeat(hour-sunrise,24),length=Mathf.Clamp(sunset-sunrise,1,23);
            float phase=h<=length?h/length*Mathf.PI:Mathf.PI+(h-length)/(24-length)*Mathf.PI;
            var e=Vector3.ProjectOnPlane(east,Vector3.up).normalized;if(e.sqrMagnitude<.5f)e=Vector3.right;
            var south=Vector3.Cross(Vector3.up,e);float max=maximumElevation*Mathf.Deg2Rad;
            var sun=(e*Mathf.Cos(phase)+(south*Mathf.Cos(max)+Vector3.up*Mathf.Sin(max))*Mathf.Sin(phase)).normalized;
            float altitude=Mathf.Asin(sun.y)*Mathf.Rad2Deg;
            float d=Smooth(0,18,altitude),n=1-Smooth(-14,-3,altitude),tw=1-d-n;
            bool solar=altitude>=0;var celestial=solar?sun:-sun;
            float offset=solar?sunLightAltitudeOffset:moonLightAltitudeOffset;
            float lightElevation=Mathf.Clamp(Mathf.Abs(altitude)+offset,.5f,89)*Mathf.Deg2Rad;
            var horizontal=Vector3.ProjectOnPlane(celestial,Vector3.up).normalized;
            var lightDirection=horizontal*Mathf.Cos(lightElevation)+Vector3.up*Mathf.Sin(lightElevation);
            return new Sample{sun=sun,moon=-sun,weights=new Vector3(d,tw,n),lightRotation=Quaternion.LookRotation(-lightDirection,Vector3.up),
                lightColor=solar?Color.Lerp(warmLight,day.lightColor,d):night.lightColor,
                lightIntensity=solar?day.lightIntensity*Smooth(0,12,altitude):night.lightIntensity*Smooth(0,16,-altitude),
                ambient=day.ambientIntensity*d+twilightAmbient*tw+night.ambientIntensity*n,
                reflection=day.reflectionIntensity*(1-n)+night.reflectionIntensity*n};
        }
    }
}
