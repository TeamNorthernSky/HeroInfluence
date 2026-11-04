using UnityEngine;
namespace JC.Environment {
 public sealed partial class JcDayNightProfile {
  [Header("태양·달 분할 경로")]
  [Tooltip("오전·오후를 정오점에서 연결하는 별도 곡선과 남쪽을 지나는 달빛 경로를 사용합니다.")]
  // JC | 2026-11-04 | 저장 필드명은 유지하고 조절 화면에는 분할 경로임을 명시합니다.
  [InspectorName("태양·달 분할 경로 사용")] public bool continuousSunArc;
  [Range(10,80),Tooltip("실제 표시 시각 12시의 태양 고도입니다. 낮출수록 그림자가 길어집니다.")]
  // JC | 2026-11-04 | 두 곡선과 다른 기준점은 유지하고 정오 태양 고도를40도로 맞춥니다.
  public float solarNoonElevation=40;
  // JC | 2026-11-04 | 이전 원호 기울기 저장값은 남기되 분할 경로에서는 사용하지 않습니다.
  [HideInInspector] public float solarArcLatitude=37.5f;

  [Range(.05f,1),Tooltip("외곽 산의 그림자 계산용 높이 비율입니다. 0.5이면 지면 높이0을 기준으로 절반 높이의 그림자를 그립니다. 산의 외형·충돌은 바꾸지 않으며 1이면 원래 그림자로 복원합니다.")]
  public float mountainShadowHeight=.5f;
  // JC | 2026-11-04 | 왼쪽 Mount03은 다른 외곽 산보다 높아 별도 비율로 그림자 범위를 제한합니다.
  [Range(.05f,1),Tooltip("왼쪽 높은 산(Mount03)의 그림자 높이 비율입니다. 외형은 유지하며 기본0.15, 1이면 원래 그림자로 복원합니다.")]
  public float leftMountainShadowHeight=.15f;

  // JC | 2026-11-04 | 일출100°·일몰260°를 낮과 교대 경로에서 함께 사용해 전 구간의 남향 그림자를 방지합니다.
  const float DawnAzimuth=100f,DuskAzimuth=260f;
  const float DawnKeyHour=6.5f,DuskKeyHour=18f,NoonHour=12f;

  // 색감의 구간별 시간 재매핑은 유지하되, 태양 최고점은 실제 12시에 고정합니다.
  float ClockHourForKey(float keyHour){
   bool valid=ValidSegmentTimes();
   for(int i=0;i<4;i++){
    int next=(i+1)%4;
    float referenceSpan=Mathf.Repeat(DefaultHour(next)-DefaultHour(i),24);
    float elapsed=Mathf.Repeat(keyHour-DefaultHour(i),24);
    if(elapsed>=referenceSpan)continue;
    float from=valid?segments[i].hour:DefaultHour(i),to=valid?segments[next].hour:DefaultHour(next);
    return Mathf.Repeat(from+Mathf.Repeat(to-from,24)*elapsed/referenceSpan,24);
   }
   return keyHour;
  }
  // JC | 2026-11-04 | 오전·오후는 독립적인 3점 곡선입니다. 정오 위치만 공유하고 정오 접선은 강제하지 않습니다.
  static float ThreePoint(float h,float t0,float t1,float t2,float v0,float v1,float v2,bool easeEnds=false){
   float first=(v1-v0)/(t1-t0),last=(v2-v1)/(t2-t1);
   float middle=MotionSlope(first,last,t1-t0,t2-t1);
   bool left=h<=t1;float span=left?t1-t0:t2-t1,t=Mathf.Clamp01((h-(left?t0:t1))/span);
   float a=left?v0:v1,b=left?v1:v2,m0=(left?(easeEnds?0:first):middle)*span,m1=(left?middle:(easeEnds?0:last))*span;
   float q=t*t,c=q*t;return (2*c-3*q+1)*a+(c-2*q+t)*m0+(-2*c+3*q)*b+(c-q)*m1;
  }
  bool TrySunArc(double hours,out float elevation,out float azimuth){
   float h=Mathf.Repeat((float)(hours%24),24),dawn=ClockHourForKey(DawnKeyHour),dusk=ClockHourForKey(DuskKeyHour);
   float morning=ClockHourForKey(8.5f),afternoon=ClockHourForKey(14.5f),noon=Mathf.Clamp(solarNoonElevation,10,80);
   if(h>=dawn&&h<=dusk){
    if(h<=NoonHour){
     elevation=ThreePoint(h,dawn,morning,NoonHour,8,38,noon);
     azimuth=ThreePoint(h,dawn,morning,NoonHour,DawnAzimuth,110,180);
    }else{
     elevation=ThreePoint(h,NoonHour,afternoon,dusk,noon,45,8);
     azimuth=ThreePoint(h,NoonHour,afternoon,dusk,180,225,DuskAzimuth);
    }
    return true;
   }
   // JC | 2026-11-04 | 북쪽을 도는 기존 달빛 키를 사용하지 않고 날짜를 잇는 시각축에서 동남→남→서남으로 진행합니다.
   float night=h<dawn?h+24:h,moonrise=ClockHourForKey(20.5f),moonset=ClockHourForKey(5f)+24;
   if(night<moonrise){Handoff(night,dusk,moonrise,8,DuskAzimuth,12,110,out elevation,out azimuth);return true;}
   if(night>moonset){Handoff(night,moonset,dawn+24,18,250,8,DawnAzimuth,out elevation,out azimuth);return true;}
   // JC | 2026-11-04 | 자정 달빛도40도로 맞춰 같은 물체의 정오·자정 그림자 길이를 일치시킵니다.
   elevation=ThreePoint(night,moonrise,24,moonset,12,40,18,true);
   azimuth=ThreePoint(night,moonrise,24,moonset,110,180,250,true);return true;
  }
  // JC | 2026-11-04 | 교대 구간의 중앙에서만 광원을 회전시키고, 그동안 직접광과 그림자를0으로 만들어 방향 전환을 숨깁니다.
  static void Handoff(float h,float from,float to,float e0,float a0,float e1,float a1,out float e,out float a){
   float t=Mathf.InverseLerp(from,to,h),turn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.65f,t));
   e=Mathf.Lerp(e0,e1,turn);a=Mathf.Lerp(a0,a1,turn);
  }
  public float CelestialHandoffMultiplier(double hours){
   if(!continuousSunArc)return 1;
   float h=Mathf.Repeat((float)(hours%24),24),dawn=ClockHourForKey(DawnKeyHour),dusk=ClockHourForKey(DuskKeyHour);
   if(h>=dawn&&h<=dusk)return 1;
   float night=h<dawn?h+24:h,rise=ClockHourForKey(20.5f),set=ClockHourForKey(5f)+24;
   float t=night<rise?Mathf.InverseLerp(dusk,rise,night):night>set?Mathf.InverseLerp(set,dawn+24,night):-1;
   if(t<0)return 1;
   if(t<.35f)return 1-Mathf.SmoothStep(0,1,t/.35f);
   return t>.65f?Mathf.SmoothStep(0,1,(t-.65f)/.35f):0;
  }
  float MotionValue(Key key,bool azimuth)=>azimuth?key.azimuth:key.elevation;
 }
}
