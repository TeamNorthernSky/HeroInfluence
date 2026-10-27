using System;
using UnityEngine;
namespace JC.Environment {
 [CreateAssetMenu(menuName="JC Environment/탐사 낮밤 설정")]
 public sealed class JcDayNightProfile : ScriptableObject {
  [Tooltip("시간대 하늘 셰이더입니다. 빌드에서 유지되는 명시적 참조입니다.")] public Shader skyShader;
  [Tooltip("1턴 시작 시각입니다. 8.5는 08:30입니다."),Range(0,24)] public float startHour=8.5f;
  [Tooltip("턴마다 흐르는 게임 시간입니다."),Range(1,24)] public float hoursPerTurn=6;
  [Tooltip("턴 전환의 기본 초입니다. 일출·일몰 감속 구간은 추가 시간이 붙습니다. 0이면 즉시 적용합니다."),Min(0)] public float transitionSeconds=2;
  [Header("일출·일몰 감속")]
  [Tooltip("일출 감속의 중심 시각입니다."),Range(0,24)] public float sunriseHour=6.5f;
  [Tooltip("일몰 감속의 중심 시각입니다."),Range(0,24)] public float sunsetHour=18;
  [Tooltip("중심 전후로 감속할 게임 시간입니다. 1.5이면 전후 1시간 30분입니다."),Range(0,6)] public float twilightHalfWidth=1.5f;
  [Tooltip("중심 구간을 늘리는 배율입니다. 1이면 감속하지 않습니다."),Range(1,8)] public float twilightDurationMultiplier=3;
  [Tooltip("타임랩스 중 구름 이동 배율입니다. 게임 전체 속도는 바꾸지 않습니다."),Min(1)] public float cloudSpeedMultiplier=5;
  [Tooltip("전환 중 HUD의 밝기 비율입니다."),Range(.1f,1)] public float disabledUIAlpha=.45f;
  [Header("환경 색상")]
  [Tooltip("낮/밤 하늘의 상부 색상입니다.")] public Color daySky=new Color(.30f,.52f,.78f), nightSky=new Color(.12f,.17f,.28f);
  [Tooltip("낮/밤 수평선 및 환경광의 기준 색상입니다.")] public Color dayHorizon=new Color(.72f,.80f,.88f), nightHorizon=new Color(.22f,.26f,.36f);
  [Tooltip("낮/밤 지면 방향 환경광입니다.")] public Color dayGround=new Color(.22f,.27f,.23f), nightGround=new Color(.10f,.13f,.19f);
  [Tooltip("밤 수면/수중의 기준 밝기 배율입니다."),Range(0,1)] public float nightWaterBrightness=.24f;
  [Tooltip("밤 구름 그림자 강도의 기준 배율입니다."),Range(0,1)] public float nightCloudStrength=.08f;
  [Tooltip("밤 호안 최소 환경광입니다."),Range(0,.4f)] public float nightBankFloor=.06f;
  [Tooltip("시간대의 조명·환경·물·구름 설정입니다. 시각 오름차순, 첫 항목0시/마지막24시로 구성합니다.")]
  public Key[] keys={
   K(0,30,300,.12f,new Color(.46f,.60f,1),.20f),
   K(5,18,340,.10f,new Color(.46f,.58f,1),.18f),
   K(6.5f,8,70,.55f,new Color(1,.65f,.44f),.55f),
   K(8.5f,38,110,1.15f,new Color(1,.90f,.78f),1),
   K(12,65,180,1.2f,new Color(1,.97f,.90f),1),
   K(14.5f,45,225,1.15f,new Color(1,.94f,.86f),1),
   K(18,8,280,.5f,new Color(1,.52f,.32f),.5f),
   K(20.5f,25,295,.14f,new Color(.48f,.62f,1),.24f),
   K(24,30,300,.12f,new Color(.46f,.60f,1),.20f)
  };
  [Serializable] public class Key {
   [Tooltip("시각입니다(0~24).")] public float hour;
   [Tooltip("광원의 고도입니다. 밤에는 달빛을 표현합니다.")] public float elevation;
   [Tooltip("광원이 있는 방위입니다. 북쪽(+Z)=0°, 동쪽(+X)=90°, 남쪽=180°, 서쪽=270°. 그림자는 반대 방향으로 드리워집니다.")] public float azimuth;
   [Tooltip("방향광 강도입니다.")] public float intensity;
   [Tooltip("방향광 색상입니다.")] public Color lightColor;
   [Tooltip("환경 밝기입니다. 밤에도 탐사 대상을 식별할 수 있게 설정합니다."),Range(.05f,1)] public float daylight;
  }
  static Key K(float h,float e,float a,float i,Color c,float d)=>new Key{hour=h,elevation=e,azimuth=a,intensity=i,lightColor=c,daylight=d};
  public double HoursForTurn(int turn)=>startHour+(Math.Max(1,turn)-1)*(double)hoursPerTurn;
  public float[] BuildTransitionTimeline(double from,double to){
   const int samples=256;
   var times=new float[samples+1];
   float step=Mathf.Max(0,transitionSeconds)/samples;
   for(int i=1;i<=samples;i++){
    float h=(float)((from+(to-from)*(i-.5)/samples)%24);
    float distance=Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(h*15,sunriseHour*15)),Mathf.Abs(Mathf.DeltaAngle(h*15,sunsetHour*15)))/15;
    float t=twilightHalfWidth>0?Mathf.Clamp01(1-distance/twilightHalfWidth):0;
    float weight=1+(Mathf.Max(1,twilightDurationMultiplier)-1)*Mathf.SmoothStep(0,1,t);
    times[i]=times[i-1]+step*weight;
   }
   return times;
  }
  public Key Evaluate(double totalHours){
   float h=(float)(totalHours%24);if(h<0)h+=24;
   if(keys==null||keys.Length<2) return K(h,38,110,1.15f,Color.white,1);
   int segment=keys.Length-2;
   for(int i=1;i<keys.Length;i++)if(h<=keys[i].hour){segment=i-1;break;}
   Key a=keys[segment],b=keys[segment+1];
   float progress=Mathf.InverseLerp(a.hour,b.hour,h);
   float t=Mathf.SmoothStep(0,1,progress);
   return K(h,InterpolateMotion(segment,progress,false),InterpolateMotion(segment,progress,true),Mathf.Lerp(a.intensity,b.intensity,t),Color.Lerp(a.lightColor,b.lightColor,t),Mathf.Lerp(a.daylight,b.daylight,t));
  }
  // 시간 키 경계에서 속도를 공유합니다. 고도의 극점에서는 높이 변화만 반전하고 방위는 계속 진행합니다.
  float InterpolateMotion(int segment,float t,bool azimuth){
   Key a=keys[segment],b=keys[segment+1];
   if(keys.Length<3)return azimuth?Mathf.LerpAngle(a.azimuth,b.azimuth,t):Mathf.Lerp(a.elevation,b.elevation,t);
   Key previous=keys[segment>0?segment-1:keys.Length-2];
   Key next=keys[segment+2<keys.Length?segment+2:1];
   float span=Mathf.Max(.001f,b.hour-a.hour);
   float before=a.hour-previous.hour;if(before<=0)before+=24;
   float after=next.hour-b.hour;if(after<=0)after+=24;
   before=Mathf.Max(.001f,before);after=Mathf.Max(.001f,after);
   float value=azimuth?a.azimuth:a.elevation;
   float delta=azimuth?Mathf.DeltaAngle(a.azimuth,b.azimuth):b.elevation-a.elevation;
   float left=(azimuth?Mathf.DeltaAngle(previous.azimuth,a.azimuth):a.elevation-previous.elevation)/before;
   float middle=delta/span;
   float right=(azimuth?Mathf.DeltaAngle(b.azimuth,next.azimuth):next.elevation-b.elevation)/after;
   float m0=MotionSlope(left,middle,before,span)*span;
   float m1=MotionSlope(middle,right,span,after)*span;
   float t2=t*t,t3=t2*t;
   return value+(-2*t3+3*t2)*delta+(t3-2*t2+t)*m0+(t3-t2)*m1;
  }
  static float MotionSlope(float left,float right,float before,float after){
   if(left*right<=0)return 0;
   float w0=2*after+before,w1=after+2*before;
   return (w0+w1)/(w0/left+w1/right);
  }
 }
}
