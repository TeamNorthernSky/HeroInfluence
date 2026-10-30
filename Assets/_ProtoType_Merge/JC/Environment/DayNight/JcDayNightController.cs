using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace JC.Environment {
 [ExecuteAlways,DisallowMultipleComponent]
 public sealed class JcDayNightController : MonoBehaviour {
  public const string ScenePath="Assets/_ProtoType_Merge/Scenes/DHScene_3.unity";
  [SerializeField,Tooltip("시작 시각, 턴당 시간, 연출 길이와 시간대 조명 설정입니다.")] public JcDayNightProfile profile;
  [SerializeField,Tooltip("편집 모드에서만 아래 시각을 미리 봅니다. 해제하면 원래 환경을 복원합니다.")] public bool preview;
  [SerializeField,Range(0,24),Tooltip("편집용 시각입니다. 게임 진행과 저장 데이터는 변경하지 않습니다.")] public float previewHour=8.5f;
  // The asset is never edited by the tuning UI until the user explicitly saves it.
#if UNITY_EDITOR
  [NonSerialized] JcDayNightProfile workingProfile, workingSource;
#endif
  public JcDayNightProfile Settings {
   get {
#if UNITY_EDITOR
    if(profile==null)return null;
    if(workingProfile==null || workingSource!=profile)ReloadProfile();
    return workingProfile;
#else
    return profile;
#endif
   }
  }
  public void ReloadProfile(){
#if UNITY_EDITOR
   if(workingProfile!=null)DestroyObject(workingProfile);
   workingSource=profile;
   workingProfile=profile!=null?Instantiate(profile):null;
   if(workingProfile!=null){workingProfile.hideFlags=HideFlags.HideAndDontSave;workingProfile.name=profile.name+" (조절 중)";workingProfile.NotifyChanged();}
#endif
   appliedRevision=-1;appliedProfile=null;
  }
  static JcDayNightController owner;
  public static bool IsTransitioning => owner!=null && owner.transitioning;
  public static float CloudFactor => owner!=null && owner.applied ? Mathf.Lerp(owner.Settings.nightCloudStrength,1,owner.daylight) : 1;
  public static float CloudSpeed => IsTransitioning ? owner.Settings.cloudSpeedMultiplier : 1;
  public static float WaterBrightness => owner!=null && owner.applied ? Mathf.Lerp(owner.Settings.nightWaterBrightness,1,owner.daylight)*owner.waterMultiplier : 1;
  public static bool IsActive => owner!=null && owner.applied;
  bool transitioning,applied;
  bool battle, indoor, windowInitialized;
  float windowLevel, windowTarget, windowFrom, windowElapsed;
  readonly JcDayNightScreenColor screenColor=new JcDayNightScreenColor();
  readonly JcNightMaterialBindings characters=new JcNightMaterialBindings();
  readonly JcSpecialBuildingWindows specialWindows=new JcSpecialBuildingWindows();
  double nextCharacterScan;
  readonly List<(Light light,float intensity)> extraLights=new List<(Light,float)>();
  public static double CurrentHours => owner!=null && owner.applied ? owner.shownHours : (CurrentProfile!=null?CurrentProfile.HoursForTurn(GameManager.Instance!=null?GameManager.Instance.CurrentDay:1):8.5);
  public static JcDayNightProfile CurrentProfile => owner!=null?owner.profile:Resources.Load<JcDayNightProfile>("JC_DayNight_Default");
  public static JcDayNightController EnsureBattle(Scene scene,double hours,JcDayNightProfile settings,bool isIndoor){
   if(scene.name!="TmpBattleScene" && scene.name!="TutorialBattleScene")return null;
   foreach(var root in scene.GetRootGameObjects()){
    var found=root.GetComponentInChildren<JcDayNightController>(true);if(found!=null)return found;
   }
   var go=new GameObject("JC_BattleDayNight");SceneManager.MoveGameObjectToScene(go,scene);
   var c=go.AddComponent<JcDayNightController>();c.profile=settings;c.battle=true;c.indoor=isIndoor;c.shownHours=hours;c.ApplyHour(hours);return c;
  }
  int appliedRevision=-1; JcDayNightProfile appliedProfile;
  float waterMultiplier=1;
  float daylight=1;
  int shownTurn=-1;
  double shownHours;
  Light sun; Quaternion oldRotation; Color oldLightColor; float oldIntensity,oldShadow;
  AmbientMode oldAmbientMode; Color oldSky,oldEquator,oldGround,oldFog;
  float oldAmbientIntensity,oldReflectionIntensity;
  Material oldSkybox,skybox;
  DefaultReflectionMode oldReflectionMode; Texture oldReflection; Cubemap cube;
  SphericalHarmonicsL2 oldProbe;
  struct ProbeState {public ReflectionProbe probe;public ReflectionProbeMode mode;public Texture texture;public float intensity;}
  readonly List<ProbeState> probes=new List<ProbeState>();
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
  void OnEnable(){if(profile==null)profile=Resources.Load<JcDayNightProfile>("JC_DayNight_Default");SceneManager.activeSceneChanged+=SceneChanged;EnsureSegmentObjects();}
  public void EnsureSegmentObjects(){
   for(int i=0;i<4;i++){
    JcDayNightSegment slot=null;foreach(var child in GetComponentsInChildren<JcDayNightSegment>(true))if(child.index==i&&child.transform.parent==transform){slot=child;break;}
    if(slot==null){var go=new GameObject("구간 "+(i+1)){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);slot=go.AddComponent<JcDayNightSegment>();}
    slot.index=i;slot.controller=this;
   }
  }
  void SceneChanged(Scene oldScene,Scene newScene){if(newScene!=gameObject.scene)Cancel();}
  void OnDisable(){SceneManager.activeSceneChanged-=SceneChanged;Cancel();}
  void OnDestroy(){Cancel();}
  public static void CancelForScene(Scene scene){if(owner!=null && owner.gameObject.scene==scene)owner.Cancel();}
  void LateUpdate(){
   if(Settings==null || SceneManager.GetActiveScene()!=gameObject.scene)return;
   if(!Application.isPlaying){if(preview)ApplyHour(previewHour);else if(applied)Restore();return;}
   if(battle){if(!applied||appliedProfile!=Settings||appliedRevision!=Settings.revision)ApplyHour(shownHours);UpdateWindowFade();return;}
   if(!transitioning && !DHGameStateRestoreService.IsRestoring){
    var tm=FindFirstObjectByType<TurnManager>();
    var tutorial = gameObject.scene.name == "TutorialExploreScene" ? FindFirstObjectByType<TutorialTurnManager>() : null;
    int turn=tutorial!=null?tutorial.CurrentTurn:GameManager.Instance!=null?GameManager.Instance.CurrentDay:tm!=null?tm.GetDay():1;
    if(!applied||shownTurn!=turn||appliedProfile!=Settings||appliedRevision!=Settings.revision){shownTurn=turn;ApplyHour(Settings.HoursForTurn(turn));}
   }
   if(transitioning)LockUI();
   UpdateWindowFade();
   if(Time.realtimeSinceStartupAsDouble>=nextCharacterScan){characters.ScanCharacters(gameObject.scene);specialWindows.Scan(gameObject.scene);nextCharacterScan=Time.realtimeSinceStartupAsDouble+1;}
  }
  bool Busy()=>PlayerInteractionBusy() || ModalManager.HasAny || ChatModalController.IsOpen ||
   (ChatManager.Instance!=null && ChatManager.Instance.IsRunning) ||
   (DHWorldEventRuntimeManager.Instance!=null && DHWorldEventRuntimeManager.Instance.IsRunning) ||
   // 턴 진행 중 예약된 조건 검사는 다음 플레이어 턴에 실행합니다. 예약 자체를 기다리면 교착됩니다.
   (SceneFadeController.Instance!=null && SceneFadeController.Instance.IsTransitioning);
  bool PlayerInteractionBusy(){var registry=FindFirstObjectByType<PartyRegistry>();var party=registry!=null?registry.PlayerParty:null;var runtime=party!=null?party.GetComponent<PartyRuntime>():null;return party!=null && (party.IsMoving || (runtime!=null && runtime.IsInputLocked));}
  public IEnumerator Transition(int fromTurn,int toTurn){
   if(Settings==null || !isActiveAndEnabled || transitioning)yield break;
   // 기존 모달/대화가 끝나기 전에는 ESC를 막지 않습니다.
   while(Busy()){if(!isActiveAndEnabled || DHGameEndState.IsEnding || SceneManager.GetActiveScene()!=gameObject.scene)yield break;yield return null;}
   if(!isActiveAndEnabled || DHGameEndState.IsEnding || SceneManager.GetActiveScene()!=gameObject.scene)yield break;
   Capture();owner=this;transitioning=true;selection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
   if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
   LockUI();
   double from=Settings.HoursForTurn(fromTurn),to=Settings.HoursForTurn(toTurn);float elapsed=0;
   var timeline=Settings.BuildTransitionTimeline(from,to);int segment=1;
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
     yield return null;
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
   var shader=Settings.skyShader;if(shader!=null){skybox=new Material(shader){hideFlags=HideFlags.HideAndDontSave};RenderSettings.skybox=skybox;}
   cube=new Cubemap(16,TextureFormat.RGBAHalf,false){name="JC time sky reflection",hideFlags=HideFlags.HideAndDontSave};
   RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;
   foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var p in root.GetComponentsInChildren<ReflectionProbe>()){
    probes.Add(new ProbeState{probe=p,mode=p.mode,texture=p.customBakedTexture,intensity=p.intensity});p.mode=ReflectionProbeMode.Custom;p.customBakedTexture=cube;
   }
   if(battle)foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var light in root.GetComponentsInChildren<Light>())
    if(light!=sun)extraLights.Add((light,light.intensity));
   applied=true;
  }
  public void ApplyHour(double hours){
   if(Settings==null||SceneManager.GetActiveScene()!=gameObject.scene)return;
   Capture();if(Application.isPlaying)specialWindows.Scan(gameObject.scene);shownHours=hours;appliedProfile=Settings;appliedRevision=Settings.revision;var k=Settings.Evaluate(hours);daylight=k.daylight;
   var look=Settings.SampleSegment(hours);var baseline=Settings.SampleSegment(hours,true);waterMultiplier=look.waterMultiplier;
   // 방위각은 광원의 위치입니다. 방향광의 forward는 빛의 진행 방향이므로 반대쪽을 향합니다.
   if(sun!=null){sun.transform.rotation=indoor?oldRotation:Quaternion.Euler(k.elevation,k.azimuth+180f,0);sun.color=indoor?Color.Lerp(oldLightColor,new Color(.70f,.79f,1),.25f*(1-daylight)):k.lightColor;sun.intensity=indoor?oldIntensity*Mathf.Lerp(.7f,1,daylight):k.intensity*Mathf.Lerp(Settings.nightLightBoost,1,daylight);sun.shadowStrength=Mathf.Lerp(.45f,oldShadow,daylight);}
   if(sun!=null){sun.color=JcDayNightProfile.CorrectColor(sun.color,look.lightColor,baseline.lightColor);sun.intensity*=look.lightIntensity/Mathf.Max(.0001f,baseline.lightIntensity);sun.shadowStrength=Mathf.Clamp01(sun.shadowStrength*look.shadowMultiplier);}
   Color sky=Color.Lerp(Settings.nightSky,Settings.daySky,daylight);
   Color horizon=Color.Lerp(Settings.nightHorizon,Settings.dayHorizon,daylight);
   float sunset=Mathf.Clamp01((k.lightColor.r-k.lightColor.b)*1.3f);horizon=Color.Lerp(horizon,new Color(.85f,.44f,.28f),sunset);
   Color ground=Color.Lerp(Settings.nightGround,Settings.dayGround,daylight);
   float boost=Mathf.Lerp(Settings.nightAmbientBoost,1,daylight);sky*=boost;horizon*=boost;ground*=boost;
   sky=JcDayNightProfile.CorrectColor(sky,look.sky,baseline.sky);horizon=JcDayNightProfile.CorrectColor(horizon,look.horizon,baseline.horizon);ground=JcDayNightProfile.CorrectColor(ground,look.ground,baseline.ground);
   if(indoor){sky=Color.Lerp(oldSky,sky,.35f);horizon=Color.Lerp(oldEquator/ .55f,horizon,.35f);ground=Color.Lerp(oldGround,ground,.35f);}
   foreach(var entry in extraLights)if(entry.light!=null)entry.light.intensity=entry.intensity*(indoor?1:Mathf.Lerp(.3f,1,daylight));
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientIntensity=1;
   float ambientNight=Mathf.InverseLerp(1,.3f,daylight);
   float objectAmbient=Mathf.Lerp(1,Mathf.Max(1,indoor?Settings.indoorNightObjectAmbientBoost:Settings.nightObjectAmbientBoost),ambientNight);
   objectAmbient*=look.ambientMultiplier;
   RenderSettings.ambientSkyColor=(sky.linear*objectAmbient).gamma;RenderSettings.ambientEquatorColor=((horizon*.55f).linear*objectAmbient).gamma;RenderSettings.ambientGroundColor=(ground.linear*objectAmbient).gamma;
   var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(horizon.linear*(.4f*objectAmbient));sh.AddDirectionalLight(Vector3.up,sky.linear,.7f*objectAmbient);RenderSettings.ambientProbe=sh;
   RenderSettings.fogColor=horizon;
   if(skybox!=null){skybox.SetColor("_Top",sky);skybox.SetColor("_Horizon",horizon);skybox.SetColor("_Bottom",ground);}
   UpdateReflection(sky*look.reflectionTint,horizon*look.reflectionTint,ground*look.reflectionTint);
   RenderSettings.reflectionIntensity=oldReflectionIntensity*look.reflectionMultiplier;
   foreach(var state in probes)if(state.probe!=null)state.probe.intensity=state.intensity*look.reflectionMultiplier;
   Shader.SetGlobalVector("_JcDayNight",new Vector4(1,daylight,Settings.nightBankFloor,WaterBrightness));
   Shader.SetGlobalVector("_JcNightReadability",new Vector4(look.characterFill,indoor?1:Mathf.Lerp(.66f,1,daylight),look.npcFill,look.enemyFill));
   screenColor.Apply(gameObject.scene,look);
   if(!battle && Time.realtimeSinceStartupAsDouble>=nextCharacterScan){characters.ScanCharacters(gameObject.scene);nextCharacterScan=Time.realtimeSinceStartupAsDouble+1;}
   float target=Settings.WindowsOn(hours)?1:0;
   if(!windowInitialized || !Application.isPlaying){windowInitialized=true;windowFrom=windowLevel=windowTarget=target;}
   else if(target!=windowTarget){windowFrom=windowLevel;windowTarget=target;windowElapsed=0;}
   PublishWindows();
  }
  void UpdateWindowFade(){
   if(!applied)return;
   windowElapsed+=Time.unscaledDeltaTime;
   windowLevel=Mathf.Lerp(windowFrom,windowTarget,Mathf.Clamp01(windowElapsed/Mathf.Max(.01f,Settings.windowFadeSeconds)));
   PublishWindows();
  }
  void PublishWindows(){Shader.SetGlobalVector("_JcWindowLight",new Vector4(windowLevel,Settings.windowIntensity,0,0));Shader.SetGlobalColor("_JcWindowTint",Settings.windowTint);}
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
    if(!canvas.isRootCanvas||canvas.renderMode==RenderMode.WorldSpace)continue;
    LockGroup(canvas.gameObject,Settings.disabledUIAlpha);
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
  public void Cancel(){transitioning=false;UnlockUI();Restore();}
  void Restore(){
   if(!applied)return;
   if(sun!=null){sun.transform.rotation=oldRotation;sun.color=oldLightColor;sun.intensity=oldIntensity;sun.shadowStrength=oldShadow;}
   if(SceneManager.GetActiveScene()==gameObject.scene){
   RenderSettings.ambientMode=oldAmbientMode;RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;RenderSettings.ambientIntensity=oldAmbientIntensity;RenderSettings.ambientProbe=oldProbe;RenderSettings.fogColor=oldFog;
   RenderSettings.skybox=oldSkybox;RenderSettings.defaultReflectionMode=oldReflectionMode;RenderSettings.customReflectionTexture=oldReflection;RenderSettings.reflectionIntensity=oldReflectionIntensity;
   }
   foreach(var p in probes)if(p.probe!=null){p.probe.mode=p.mode;p.probe.customBakedTexture=p.texture;p.probe.intensity=p.intensity;}probes.Clear();
   screenColor.Dispose();specialWindows.Dispose();characters.Dispose();foreach(var entry in extraLights)if(entry.light!=null)entry.light.intensity=entry.intensity;extraLights.Clear();
   windowInitialized=false;
   DestroyObject(skybox);DestroyObject(cube);skybox=null;cube=null;applied=false;shownTurn=-1;
   if(owner==this){Shader.SetGlobalVector("_JcDayNight",Vector4.zero);Shader.SetGlobalVector("_JcWindowLight",Vector4.zero);Shader.SetGlobalVector("_JcNightReadability",Vector4.zero);owner=null;}
  }
  static void DestroyObject(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
 }
}

