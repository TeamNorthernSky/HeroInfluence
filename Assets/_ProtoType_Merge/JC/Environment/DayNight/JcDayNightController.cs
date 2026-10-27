using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
namespace JC.Environment {
 [ExecuteAlways,DisallowMultipleComponent]
 public sealed class JcDayNightController : MonoBehaviour {
  public const string ScenePath="Assets/_ProtoType_Merge/Scenes/DHScene_3.unity";
  [SerializeField,Tooltip("시작 시각, 턴당 시간, 연출 길이와 시간대 조명 설정입니다.")] public JcDayNightProfile profile;
  [SerializeField,Tooltip("편집 모드에서만 아래 시각을 미리 봅니다. 해제하면 원래 환경을 복원합니다.")] public bool preview;
  [SerializeField,Range(0,24),Tooltip("편집용 시각입니다. 게임 진행과 저장 데이터는 변경하지 않습니다.")] public float previewHour=8.5f;
  static JcDayNightController owner;
  public static bool IsTransitioning => owner!=null && owner.transitioning;
  public static float CloudFactor => owner!=null && owner.applied ? Mathf.Lerp(owner.profile.nightCloudStrength,1,owner.daylight) : 1;
  public static float CloudSpeed => IsTransitioning ? owner.profile.cloudSpeedMultiplier : 1;
  public static float WaterBrightness => owner!=null && owner.applied ? Mathf.Lerp(owner.profile.nightWaterBrightness,1,owner.daylight) : 1;
  public static bool IsActive => owner!=null && owner.applied;
  bool transitioning,applied;
  float daylight=1;
  int shownTurn=-1;
  double shownHours;
  Light sun; Quaternion oldRotation; Color oldLightColor; float oldIntensity,oldShadow;
  AmbientMode oldAmbientMode; Color oldSky,oldEquator,oldGround,oldFog;
  float oldAmbientIntensity,oldReflectionIntensity;
  Material oldSkybox,skybox;
  DefaultReflectionMode oldReflectionMode; Texture oldReflection; Cubemap cube;
  SphericalHarmonicsL2 oldProbe;
  struct ProbeState {public ReflectionProbe probe;public ReflectionProbeMode mode;public Texture texture;}
  readonly List<ProbeState> probes=new List<ProbeState>();
  Canvas clockCanvas; TextMeshProUGUI clockText;
  sealed class UILock {public CanvasGroup group;public UIInputBlocker blocker;public bool added,interactable,blocks;public float alpha;}
  readonly List<UILock> locks=new List<UILock>();
  GameObject selection;
  public static JcDayNightController Ensure(Scene scene) {
   if(scene.path!=ScenePath && scene.name!="TutorialExploreScene")return null;
   foreach(var root in scene.GetRootGameObjects()){
    var c=root.GetComponentInChildren<JcDayNightController>(true);if(c!=null)return c;
   }
   var go=new GameObject("JC_DayNight"){hideFlags=HideFlags.DontSave};
   SceneManager.MoveGameObjectToScene(go,scene);
   var parent=Array.Find(scene.GetRootGameObjects(),r=>r.name=="JC_Environment");if(parent!=null)go.transform.SetParent(parent.transform,false);
   return go.AddComponent<JcDayNightController>();
  }
  void OnEnable(){if(profile==null)profile=Resources.Load<JcDayNightProfile>("JC_DayNight_Default");SceneManager.activeSceneChanged+=SceneChanged;}
  void SceneChanged(Scene oldScene,Scene newScene){if(newScene!=gameObject.scene)Cancel();}
  void OnDisable(){SceneManager.activeSceneChanged-=SceneChanged;Cancel();}
  void OnDestroy(){Cancel();}
  public static void CancelForScene(Scene scene){if(owner!=null && owner.gameObject.scene==scene)owner.Cancel();}
  void LateUpdate(){
   if(profile==null || SceneManager.GetActiveScene()!=gameObject.scene)return;
   if(!Application.isPlaying){if(preview)ApplyHour(previewHour);else if(applied)Restore();return;}
   if(!transitioning && !DHGameStateRestoreService.IsRestoring){
    var tm=FindFirstObjectByType<TurnManager>();
    var tutorial = gameObject.scene.name == "TutorialExploreScene" ? FindFirstObjectByType<TutorialTurnManager>() : null;
    int turn=tutorial!=null?tutorial.CurrentTurn:GameManager.Instance!=null?GameManager.Instance.CurrentDay:tm!=null?tm.GetDay():1;
    if(!applied||shownTurn!=turn){shownTurn=turn;ApplyHour(profile.HoursForTurn(turn));}
   }
   if(transitioning)LockUI();
   UpdateClock();
  }
  bool Busy()=>PlayerInteractionBusy() || ModalManager.HasAny || ChatModalController.IsOpen ||
   (ChatManager.Instance!=null && ChatManager.Instance.IsRunning) ||
   (DHWorldEventRuntimeManager.Instance!=null && DHWorldEventRuntimeManager.Instance.IsRunning) ||
   // 턴 진행 중 예약된 조건 검사는 다음 플레이어 턴에 실행합니다. 예약 자체를 기다리면 교착됩니다.
   (SceneFadeController.Instance!=null && SceneFadeController.Instance.IsTransitioning);
  bool PlayerInteractionBusy(){var registry=FindFirstObjectByType<PartyRegistry>();var party=registry!=null?registry.PlayerParty:null;var runtime=party!=null?party.GetComponent<PartyRuntime>():null;return party!=null && (party.IsMoving || (runtime!=null && runtime.IsInputLocked));}
  public IEnumerator Transition(int fromTurn,int toTurn){
   if(profile==null || !isActiveAndEnabled || transitioning)yield break;
   // 기존 모달/대화가 끝나기 전에는 ESC를 막지 않습니다.
   while(Busy()){if(!isActiveAndEnabled || DHGameEndState.IsEnding || SceneManager.GetActiveScene()!=gameObject.scene)yield break;yield return null;}
   if(!isActiveAndEnabled || DHGameEndState.IsEnding || SceneManager.GetActiveScene()!=gameObject.scene)yield break;
   Capture();owner=this;transitioning=true;selection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
   if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
   LockUI();
   double from=profile.HoursForTurn(fromTurn),to=profile.HoursForTurn(toTurn);float elapsed=0;
   var timeline=profile.BuildTransitionTimeline(from,to);int segment=1;
   float duration=timeline[timeline.Length-1];
   try{
    ApplyHour(from);
    while(elapsed<duration){
     if(!isActiveAndEnabled || DHGameEndState.IsEnding || SceneManager.GetActiveScene()!=gameObject.scene)yield break;
     elapsed+=Time.unscaledDeltaTime;
     float time=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/duration))*duration;
     while(segment<timeline.Length-1 && timeline[segment]<time)segment++;
     float progress=(segment-1+Mathf.InverseLerp(timeline[segment-1],timeline[segment],time))/(timeline.Length-1);
     ApplyHour(from+(to-from)*progress);
     UpdateClock();yield return null;
    }
    ApplyHour(to);shownTurn=toTurn;
   }finally{transitioning=false;UnlockUI();}
  }
  void Capture(){
   if(applied)return;
   if(owner!=null && owner!=this)owner.Cancel();
   owner=this;
   sun=RenderSettings.sun;
   if(sun==null||sun.gameObject.scene!=gameObject.scene){sun=null;foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var l in root.GetComponentsInChildren<Light>())if(l.type==LightType.Directional){sun=l;break;}}
   if(sun!=null){oldRotation=sun.transform.rotation;oldLightColor=sun.color;oldIntensity=sun.intensity;oldShadow=sun.shadowStrength;}
   oldAmbientMode=RenderSettings.ambientMode;oldSky=RenderSettings.ambientSkyColor;oldEquator=RenderSettings.ambientEquatorColor;oldGround=RenderSettings.ambientGroundColor;
   oldAmbientIntensity=RenderSettings.ambientIntensity;oldFog=RenderSettings.fogColor;oldProbe=RenderSettings.ambientProbe;
   oldSkybox=RenderSettings.skybox;oldReflectionMode=RenderSettings.defaultReflectionMode;oldReflection=RenderSettings.customReflectionTexture;oldReflectionIntensity=RenderSettings.reflectionIntensity;
   var shader=profile.skyShader;if(shader!=null){skybox=new Material(shader){hideFlags=HideFlags.HideAndDontSave};RenderSettings.skybox=skybox;}
   cube=new Cubemap(16,TextureFormat.RGBAHalf,false){name="JC time sky reflection",hideFlags=HideFlags.HideAndDontSave};
   RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;
   foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var p in root.GetComponentsInChildren<ReflectionProbe>()){
    probes.Add(new ProbeState{probe=p,mode=p.mode,texture=p.customBakedTexture});p.mode=ReflectionProbeMode.Custom;p.customBakedTexture=cube;
   }
   applied=true;
  }
  public void ApplyHour(double hours){
   if(profile==null||SceneManager.GetActiveScene()!=gameObject.scene)return;
   Capture();shownHours=hours;var k=profile.Evaluate(hours);daylight=k.daylight;
   // 방위각은 광원의 위치입니다. 방향광의 forward는 빛의 진행 방향이므로 반대쪽을 향합니다.
   if(sun!=null){sun.transform.rotation=Quaternion.Euler(k.elevation,k.azimuth+180f,0);sun.color=k.lightColor;sun.intensity=k.intensity;sun.shadowStrength=Mathf.Lerp(.45f,oldShadow,daylight);}
   Color sky=Color.Lerp(profile.nightSky,profile.daySky,daylight);
   Color horizon=Color.Lerp(profile.nightHorizon,profile.dayHorizon,daylight);
   float sunset=Mathf.Clamp01((k.lightColor.r-k.lightColor.b)*1.3f);horizon=Color.Lerp(horizon,new Color(.85f,.44f,.28f),sunset);
   Color ground=Color.Lerp(profile.nightGround,profile.dayGround,daylight);
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientIntensity=1;
   RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=horizon*.55f;RenderSettings.ambientGroundColor=ground;
   var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(horizon.linear*.4f);sh.AddDirectionalLight(Vector3.up,sky.linear,.7f);RenderSettings.ambientProbe=sh;
   RenderSettings.fogColor=horizon;
   if(skybox!=null){skybox.SetColor("_Top",sky);skybox.SetColor("_Horizon",horizon);skybox.SetColor("_Bottom",ground);}
   UpdateReflection(sky,horizon,ground);
   Shader.SetGlobalVector("_JcDayNight",new Vector4(1,daylight,profile.nightBankFloor,WaterBrightness));
  }
  readonly Color[] reflectionPixels=new Color[256];
  void UpdateReflection(Color sky,Color horizon,Color ground){
   for(int f=0;f<6;f++){
    var colors=reflectionPixels;for(int y=0;y<16;y++)for(int x=0;x<16;x++){
     float u=(x+.5f)/8-1,v=(y+.5f)/8-1;Vector3 d;
     switch(f){case 0:d=new Vector3(1,-v,-u);break;case 1:d=new Vector3(-1,-v,u);break;case 2:d=new Vector3(u,1,v);break;case 3:d=new Vector3(u,-1,-v);break;case 4:d=new Vector3(u,-v,1);break;default:d=new Vector3(-u,-v,-1);break;}
     float h=d.normalized.y;colors[y*16+x]=(h>=0?Color.Lerp(horizon,sky,Mathf.Sqrt(h)):Color.Lerp(horizon,ground,Mathf.Sqrt(-h))).linear;
    }cube.SetPixels(colors,(CubemapFace)f);
   }cube.Apply(false);RenderSettings.reflectionIntensity=oldReflectionIntensity;
  }
  void LockUI(){
   foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)){
    if(!canvas.isRootCanvas||canvas==clockCanvas||canvas.renderMode==RenderMode.WorldSpace)continue;
    LockGroup(canvas.gameObject,profile.disabledUIAlpha);
    foreach(var selectable in canvas.GetComponentsInChildren<Selectable>(true))LockGroup(selectable.gameObject,1);
   }
  }
  void LockGroup(GameObject go,float dim){
   if(locks.Exists(x=>x.group!=null && x.group.gameObject==go))return;
   // 이동 UI와 공유하는 그룹은 원래 제어자에 연출 잠금을 합산합니다.
   // 연출 도중 변경된 기본 잠금 상태를 종료 시 그대로 반영합니다.
   var g=go.GetComponent<CanvasGroup>();bool added=g==null;if(added)g=go.AddComponent<CanvasGroup>();
   UIInputBlocker blocker=null;
   if(!added)foreach(var candidate in UnityEngine.Object.FindObjectsByType<UIInputBlocker>(FindObjectsInactive.Include,FindObjectsSortMode.None))
    if(candidate.Controls(g)){blocker=candidate;break;}
   locks.Add(new UILock{group=g,blocker=blocker,added=added,alpha=g.alpha,interactable=g.interactable,blocks=g.blocksRaycasts});
   if(blocker!=null)blocker.SetAdditionalLock(this,true);
   else {g.interactable=false;g.blocksRaycasts=true;}
   g.alpha*=dim;
  }
  void UnlockUI(){
   foreach(var state in locks){
    if(state.blocker!=null)state.blocker.SetAdditionalLock(this,false);
    if(state.group==null)continue;
    if(state.added){state.group.interactable=true;state.group.alpha=1;DestroyObject(state.group);}
    else {if(state.blocker==null){state.group.interactable=state.interactable;state.group.blocksRaycasts=state.blocks;}state.group.alpha=state.alpha;}
   }
   locks.Clear();
   if(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject==null && selection!=null && selection.activeInHierarchy){var s=selection.GetComponent<Selectable>();if(s!=null && s.IsInteractable())EventSystem.current.SetSelectedGameObject(selection);}
   selection=null;
  }
  void UpdateClock(){
   if(!Application.isPlaying)return;
   if(clockCanvas==null){
    var go=new GameObject("JC 탐사 시각",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
    clockCanvas=go.GetComponent<Canvas>();clockCanvas.renderMode=RenderMode.ScreenSpaceOverlay;clockCanvas.sortingOrder=500;
    var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
    var text=new GameObject("시각",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(go.transform,false);clockText=text.GetComponent<TextMeshProUGUI>();clockText.raycastTarget=false;
    clockText.font=TMP_Settings.defaultFontAsset;foreach(var existing in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))if(existing!=clockText && existing.font!=null && existing.font.HasCharacter('시')){clockText.font=existing.font;break;}clockText.fontSize=26;clockText.alignment=TextAlignmentOptions.Center;clockText.color=new Color(.93f,.96f,1);clockText.outlineWidth=.15f;clockText.outlineColor=Color.black;
    var rt=text.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,1);rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-65);rt.sizeDelta=new Vector2(420,55);
   }
   int minutes=(int)Math.Round(shownHours*60);int h=((minutes/60)%24+24)%24,m=(minutes%60+60)%60;
   clockText.text=(transitioning?"시간 경과  ":"")+h.ToString("00")+":"+m.ToString("00");
  }
  public void Cancel(){transitioning=false;UnlockUI();Restore();if(clockCanvas!=null)DestroyObject(clockCanvas.gameObject);clockCanvas=null;clockText=null;}
  void Restore(){
   if(!applied)return;
   if(sun!=null){sun.transform.rotation=oldRotation;sun.color=oldLightColor;sun.intensity=oldIntensity;sun.shadowStrength=oldShadow;}
   if(SceneManager.GetActiveScene()==gameObject.scene){
   RenderSettings.ambientMode=oldAmbientMode;RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;RenderSettings.ambientIntensity=oldAmbientIntensity;RenderSettings.ambientProbe=oldProbe;RenderSettings.fogColor=oldFog;
   RenderSettings.skybox=oldSkybox;RenderSettings.defaultReflectionMode=oldReflectionMode;RenderSettings.customReflectionTexture=oldReflection;RenderSettings.reflectionIntensity=oldReflectionIntensity;
   }
   foreach(var p in probes)if(p.probe!=null){p.probe.mode=p.mode;p.probe.customBakedTexture=p.texture;}probes.Clear();
   DestroyObject(skybox);DestroyObject(cube);skybox=null;cube=null;applied=false;shownTurn=-1;
   Shader.SetGlobalVector("_JcDayNight",Vector4.zero);if(owner==this)owner=null;
  }
  static void DestroyObject(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
 }
}

