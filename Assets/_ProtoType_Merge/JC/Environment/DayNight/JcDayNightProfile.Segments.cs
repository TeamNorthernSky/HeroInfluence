using System;
using UnityEngine;
namespace JC.Environment {
 public sealed partial class JcDayNightProfile {
  [Serializable] public sealed class Segment {
   [Range(0,23.99f),Tooltip("24시간 순환 안에서 이 구간의 기준 시각입니다. 구간 번호 순서는 순환 순서입니다.")] public float hour;
   [ColorUsage(false,true),Tooltip("방향광의 색상입니다.")] public Color lightColor;
   [Min(0),Tooltip("실외 방향광 강도입니다. 실내에서는 기존 조명에 대한 상대 보정으로 적용됩니다.")] public float lightIntensity;
   [Min(0),Tooltip("기존 그림자 강도에 곱하는 배율입니다.")] public float shadowMultiplier=1;
   [ColorUsage(false,true),Tooltip("하늘 위쪽 색상입니다.")] public Color sky;
   [ColorUsage(false,true),Tooltip("수평선과 안개의 기준 색상입니다.")] public Color horizon;
   [ColorUsage(false,true),Tooltip("지면 방향 환경색입니다.")] public Color ground;
   [Min(0),Tooltip("물체에 비치는 환경광 배율입니다. 하늘 자체의 밝기와 별개입니다.")] public float ambientMultiplier=1;
   [ColorUsage(false,true),Tooltip("환경 반사에 곱하는 색상입니다. 물체 원본 재질은 변경하지 않습니다.")] public Color reflectionTint=Color.white;
   [Min(0),Tooltip("기존 환경 반사의 강도에 곱하는 배율입니다.")] public float reflectionMultiplier=1;
   [Range(0,.5f),Tooltip("플레이어 파티의 야간 보조 발광 강도입니다.")] public float characterFill=.16f;
   [Tooltip("플레이어 파티의 보조 밝기를 사용합니다.")] public bool partyFillEnabled=true;
   [Tooltip("필드 NPC의 보조 밝기를 사용합니다.")] public bool npcFillEnabled=true;
   [Range(0,.5f),Tooltip("필드 NPC의 야간 보조 발광 강도입니다.")] public float npcFill=.16f;
   [Tooltip("필드 전투 이벤트 적의 보조 밝기를 사용합니다. 실제 전투씬의 적 조명과는 별개입니다.")] public bool enemyFillEnabled=true;
   [Range(0,.5f),Tooltip("필드 전투 이벤트 적의 야간 보조 발광 강도입니다.")] public float enemyFill=.16f;
   [Tooltip("HUD를 제외한 카메라 화면의 색보정을 사용합니다.")] public bool screenColorEnabled;
   [Range(0,1),Tooltip("화면 색보정 적용 강도입니다.")] public float screenColorStrength=1;
   [ColorUsage(false),Tooltip("최종 화면 색상에 곱하는 필터입니다. 흰색이 원래 색상입니다.")] public Color screenColorFilter=Color.white;
   [Range(-100,100),Tooltip("화면 채도입니다. 0이 원래 값입니다.")] public float screenSaturation;
   [Range(-100,100),Tooltip("화면 대비입니다. 0이 원래 값입니다.")] public float screenContrast;
   [Range(-5,5),Tooltip("화면 노출 보정입니다. 0이 원래 값입니다.")] public float screenExposure;
   [Min(0),Tooltip("기존 수면 밝기에 곱하는 배율입니다.")] public float waterMultiplier=1;
  }
  [SerializeField,HideInInspector] public Segment[] segments={DefaultSegment(0),DefaultSegment(1),DefaultSegment(2),DefaultSegment(3)};
  [NonSerialized] public int revision;
  void OnValidate(){revision++;}
  public void NotifyChanged(){revision++;}
  // 구현 시점의 외형을 고정한 복원 기준입니다. 프로필·Inspector에서 덮어쓸 수 없습니다.
  static float DefaultHour(int i)=>i==0?8.5f:i==1?14.5f:i==2?20.5f:2.5f;
  public static Segment DefaultSegment(int i){
   float d=i<2?1:i==2?.24f:.19f;
   Color light=i==0?new Color(1,.9f,.78f):i==1?new Color(1,.94f,.86f):i==2?new Color(.48f,.62f,1):new Color(.46f,.59f,1);
   float boost=Mathf.Lerp(1.2f,1,d);
   Color horizon=Color.Lerp(new Color(.22f,.26f,.36f),new Color(.72f,.80f,.88f),d);
   horizon=Color.Lerp(horizon,new Color(.85f,.44f,.28f),Mathf.Clamp01((light.r-light.b)*1.3f));
   return new Segment{hour=DefaultHour(i),lightColor=light,lightIntensity=(i<2?1.15f:i==2?.14f:.11f)*Mathf.Lerp(1.25f,1,d),
    sky=Color.Lerp(new Color(.12f,.17f,.28f),new Color(.30f,.52f,.78f),d)*boost,horizon=horizon*boost,
    ground=Color.Lerp(new Color(.10f,.13f,.19f),new Color(.22f,.27f,.23f),d)*boost};
  }
  public void ResetSegment(int index){EnsureSegments();segments[index]=DefaultSegment(index);NotifyChanged();}
  public void ResetAllSegments(){for(int i=0;i<4;i++)ResetSegment(i);}
  public void EnsureSegments(){if(segments==null||segments.Length!=4)segments=new[]{DefaultSegment(0),DefaultSegment(1),DefaultSegment(2),DefaultSegment(3)};for(int i=0;i<4;i++)if(segments[i]==null)segments[i]=DefaultSegment(i);}
  public bool ValidSegmentTimes(){
   if(segments==null||segments.Length!=4)return false;
   for(int i=0;i<4;i++)if(segments[i]==null)return false;
   float sum=0;for(int i=0;i<4;i++){if(segments[i]==null||float.IsNaN(segments[i].hour)||float.IsInfinity(segments[i].hour))return false;float span=Mathf.Repeat(segments[(i+1)%4].hour-segments[i].hour,24);if(span<.01f)return false;sum+=span;}
   return Mathf.Abs(sum-24)<.01f;
  }
  void SegmentPair(double hours,out int a,out int b,out float t){
   EnsureSegments();float h=Mathf.Repeat((float)(hours%24),24);bool valid=ValidSegmentTimes();
   for(a=0;a<4;a++){b=(a+1)%4;float from=valid?segments[a].hour:DefaultHour(a);float to=valid?segments[b].hour:DefaultHour(b);float span=Mathf.Repeat(to-from,24);float elapsed=Mathf.Repeat(h-from,24);if(elapsed<span){t=elapsed/span;return;}}
   a=0;b=1;t=0;
  }
  float MapSegmentHour(double hours){SegmentPair(hours,out int a,out int b,out float t);return Mathf.Repeat(DefaultHour(a)+Mathf.Repeat(DefaultHour(b)-DefaultHour(a),24)*t,24);}
  public Segment SampleSegment(double hours,bool defaults=false){
   SegmentPair(hours,out int a,out int b,out float t);t=Mathf.SmoothStep(0,1,t);var x=defaults?DefaultSegment(a):segments[a];var y=defaults?DefaultSegment(b):segments[b];
   return new Segment{lightColor=Color.Lerp(x.lightColor,y.lightColor,t),lightIntensity=Mathf.Lerp(x.lightIntensity,y.lightIntensity,t),shadowMultiplier=Mathf.Lerp(x.shadowMultiplier,y.shadowMultiplier,t),
    sky=Color.Lerp(x.sky,y.sky,t),horizon=Color.Lerp(x.horizon,y.horizon,t),ground=Color.Lerp(x.ground,y.ground,t),ambientMultiplier=Mathf.Lerp(x.ambientMultiplier,y.ambientMultiplier,t),
    reflectionTint=Color.Lerp(x.reflectionTint,y.reflectionTint,t),reflectionMultiplier=Mathf.Lerp(x.reflectionMultiplier,y.reflectionMultiplier,t),characterFill=Mathf.Lerp(x.partyFillEnabled?x.characterFill:0,y.partyFillEnabled?y.characterFill:0,t),
    npcFill=Mathf.Lerp(x.npcFillEnabled?x.npcFill:0,y.npcFillEnabled?y.npcFill:0,t),enemyFill=Mathf.Lerp(x.enemyFillEnabled?x.enemyFill:0,y.enemyFillEnabled?y.enemyFill:0,t),
    screenColorEnabled=true,screenColorStrength=Mathf.Lerp(x.screenColorEnabled?x.screenColorStrength:0,y.screenColorEnabled?y.screenColorStrength:0,t),
    screenColorFilter=Color.Lerp(x.screenColorFilter,y.screenColorFilter,t),screenSaturation=Mathf.Lerp(x.screenSaturation,y.screenSaturation,t),screenContrast=Mathf.Lerp(x.screenContrast,y.screenContrast,t),screenExposure=Mathf.Lerp(x.screenExposure,y.screenExposure,t),waterMultiplier=Mathf.Lerp(x.waterMultiplier,y.waterMultiplier,t)};
  }
  public static Color CorrectColor(Color original,Color value,Color baseline)=>new Color(original.r*value.r/Mathf.Max(.0001f,baseline.r),original.g*value.g/Mathf.Max(.0001f,baseline.g),original.b*value.b/Mathf.Max(.0001f,baseline.b),original.a);
 }
}
