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
  private const string TurnEndSfxKey="Explore_TurnEnd";
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
   if(workingProfile!=null){workingProfile.hideFlags=HideFlags.DontSave;workingProfile.name=profile.name+" (조절 중)";workingProfile.NotifyChanged();}
#endif
   appliedRevision=-1;appliedProfile=null;
  }
  static JcDayNightController owner;
  public static bool IsTransitioning => owner!=null && owner.transitioning;
  public static float CloudFactor => owner!=null && owner.applied ? Mathf.Lerp(owner.Settings.nightCloudStrength,1,owner.daylight) : 1;
  public static float CloudSpeed => IsTransitioning ? owner.Settings.cloudSpeedMultiplier : 1;
  public static float WaterBrightness => owner!=null && owner.applied ? Mathf.Lerp(owner.Settings.nightWaterBrightness,1,owner.daylight)*owner.waterMultiplier : 1;
  public static bool IsActive => owner!=null && owner.applied;
  float partyWallReduction;
  public static float PartyWallReduction(Scene scene) =>
   owner != null && owner.applied && !owner.battle && owner.gameObject.scene == scene ? owner.partyWallReduction : 0f;
  bool transitioning,applied;
  // JC | 2026-11-05 | 보스 원경은 월드 조명을 그대로 사용하며 야외 전투 전용 보정/보조광은 제외합니다.
  public bool CityWorldLighting { get; private set; }
  public bool OutdoorBattle => battle && !indoor;
  public static bool OwnsOutdoorLighting(Scene scene) =>
   owner != null && owner.applied && owner.OutdoorBattle && owner.gameObject.scene == scene;
  public JcOutdoorBattleLightingProfile BattleSource { get; private set; }
  public JcOutdoorBattleLightingProfile BattleDraft { get; private set; }
  // JC | 2026-11-04 | 태양의 일출·남중·일몰을 연속 궤도로 연결하고 산의 그림자 보정은 별도 설정으로 적용합니다.
  // JC | 2026-11-04 | 산의 외형 메시를 유지하고 시간대별 그림자 표시만 별도 수명으로 관리합니다.
  JcMountainShadows mountainShadows;
  void PrepareBattleProfile(){
   if(!OutdoorBattle || CityWorldLighting || BattleDraft != null)return;
   BattleSource=Resources.Load<JcOutdoorBattleLightingProfile>(JcOutdoorBattleLightingProfile.ResourceName);
   if(BattleSource==null){Debug.LogError("야외 전투 보정 프로필 누락",this);return;}
   BattleDraft=Instantiate(BattleSource);
   BattleDraft.hideFlags=HideFlags.DontSave;
  }
  JcBattleLampSettings lamps;
  internal static JcBattleLampSettings LampsFor(Scene scene) => OwnsOutdoorLighting(scene) ? owner.lamps : null;
  void UpdateLampSettings(double hours){
   if(!Application.isPlaying || !OutdoorBattle || CityWorldLighting)return;
   if(!lamps){
    var go=new GameObject("전투 가로등");
    go.transform.SetParent(transform,false);
    lamps=go.AddComponent<JcBattleLampSettings>();
   }
   Settings.SegmentPair(hours,out int segment,out _,out _);
   lamps.Select(segment);
  }
  bool battle, indoor, windowInitialized;
  float windowLevel, windowTarget, windowFrom, windowElapsed;
  readonly JcDayNightScreenColor screenColor=new JcDayNightScreenColor();
  readonly JcNightMaterialBindings characters=new JcNightMaterialBindings();
  readonly JcSpecialBuildingWindows specialWindows=new JcSpecialBuildingWindows();
  // JC | 2026-11-10 | 최초/레벨 교체만 전체 발견하고 평상시에는 DH가 알려준 건물만 수집합니다. 재질 재확인은 매 프레임 유지합니다.
  bool windowScanRequested=true;
  bool windowRegistryRefreshRequested=true;
  int windowRefreshFrame=-1;
  readonly HashSet<Transform> pendingWindowRoots=new HashSet<Transform>();
  readonly List<OutpostRegistry> windowOutposts=new List<OutpostRegistry>();
  readonly List<HeroUnionRegistry> windowUnions=new List<HeroUnionRegistry>();
  readonly List<DecorativeObjectRegistry> windowDecorations=new List<DecorativeObjectRegistry>();
  // JC | 2026-11-06 | 점령 색상이 실제로 바뀐 프레임에 창문 조명도 재연결해 주기적 재탐색 전의 소등을 막습니다.
  public static void RefreshBuildingWindows(Scene scene){
   if(Application.isPlaying && owner!=null && owner.isActiveAndEnabled && owner.applied && owner.gameObject.scene==scene)
    owner.UpdateBuildingWindows(true);
  }
  // JC | 2026-11-10 | 기존 씬 전체 갱신 API는 보존하고 튜토리얼 점령/향후 건물 외형 교체에는 대상 지정 API를 제공합니다.
  public static void RefreshBuildingWindows(Component root){
   if(owner!=null)owner.RefreshWindowRoot(root);
  }
  void RequestWindowScan(){windowScanRequested=true;windowRegistryRefreshRequested=true;}
  void WindowLevelLoaded(LevelLoader loader){if(loader!=null&&loader.gameObject.scene==gameObject.scene)RequestWindowScan();}
  void WindowLayoutLoaded(LevelZoneLayoutLoader loader){if(loader!=null&&loader.gameObject.scene==gameObject.scene)RequestWindowScan();}
  void WindowOutpostChanged(Outpost target){RefreshWindowRoot(target);}
  void WindowUnionChanged(HeroUnionUnit target){RefreshWindowRoot(target);}
  void RefreshWindowRoot(Component target){if(Application.isPlaying&&applied&&isActiveAndEnabled&&target!=null&&target.gameObject.scene==gameObject.scene)specialWindows.RefreshRoot(target);}
  // JC | 2026-11-10 | 생성 도중의 반복 등록은 같은 Transform으로 합치고 초기화가 끝난 갱신 시점에 한 번 처리합니다.
  void QueueWindowRoot(Component target){if(isActiveAndEnabled&&target!=null&&target.gameObject.scene==gameObject.scene)pendingWindowRoots.Add(target.transform);}
  void WindowOutpostRegistered(Outpost target){QueueWindowRoot(target);}
  void WindowUnionRegistered(HeroUnionUnit target){QueueWindowRoot(target);}
  void WindowDecorationRegistered(DecorativeObjectPlacement target){QueueWindowRoot(target);}
  // JC | 2026-11-10 | 등록 해제는 비활성화일 수 있어 후보를 삭제하지 않습니다. 파괴된 필터는 RefreshKnown이 제거합니다.
  void WindowOutpostsRebuilt(OutpostRegistry registry){foreach(var target in registry.Outposts)QueueWindowRoot(target);}
  void WindowUnionsRebuilt(HeroUnionRegistry registry){foreach(var target in registry.HeroUnions)QueueWindowRoot(target);}
  void WindowDecorationsRebuilt(DecorativeObjectRegistry registry){foreach(var target in registry.DecorativeObjects)QueueWindowRoot(target);}
  void WindowRegistryChanged(Component registry){if(registry!=null&&registry.gameObject.scene==gameObject.scene)windowRegistryRefreshRequested=true;}
  void WindowOutpostRegistryChanged(OutpostRegistry registry){WindowRegistryChanged(registry);}
  void WindowUnionRegistryChanged(HeroUnionRegistry registry){WindowRegistryChanged(registry);}
  void WindowDecorationRegistryChanged(DecorativeObjectRegistry registry){WindowRegistryChanged(registry);}
  void SubscribeWindowEvents(){
   LevelLoader.RuntimeLevelLoaded+=WindowLevelLoaded;LevelZoneLayoutLoader.RuntimeLayoutLoaded+=WindowLayoutLoaded;
   Outpost.OutpostStateChanged+=WindowOutpostChanged;HeroUnionUnit.HeroUnionStateChanged+=WindowUnionChanged;
   // JC | 2026-11-10 | 등록부 생성/교체 알림으로 늦은 구독을 해결하므로 주기적 등록부 검색도 제거합니다.
   OutpostRegistry.LifecycleChanged+=WindowOutpostRegistryChanged;
   HeroUnionRegistry.LifecycleChanged+=WindowUnionRegistryChanged;
   DecorativeObjectRegistry.LifecycleChanged+=WindowDecorationRegistryChanged;
   RequestWindowScan();
  }
  void UnsubscribeWindowEvents(){
   LevelLoader.RuntimeLevelLoaded-=WindowLevelLoaded;LevelZoneLayoutLoader.RuntimeLayoutLoaded-=WindowLayoutLoaded;
   Outpost.OutpostStateChanged-=WindowOutpostChanged;HeroUnionUnit.HeroUnionStateChanged-=WindowUnionChanged;
   OutpostRegistry.LifecycleChanged-=WindowOutpostRegistryChanged;
   HeroUnionRegistry.LifecycleChanged-=WindowUnionRegistryChanged;
   DecorativeObjectRegistry.LifecycleChanged-=WindowDecorationRegistryChanged;
   UnbindWindowRegistries();pendingWindowRoots.Clear();
  }
  // JC | 2026-11-10 | 복수 등록부/재생성에서도 이전 인스턴스의 구독이 남지 않도록 같은 목록으로 해제합니다.
  void UnbindWindowRegistries(){
   foreach(var registry in windowOutposts)if(registry!=null){registry.OutpostRegistered-=WindowOutpostRegistered;registry.OutpostUnregistered-=WindowOutpostRegistered;registry.Rebuilt-=WindowOutpostsRebuilt;}
   foreach(var registry in windowUnions)if(registry!=null){registry.HeroUnionRegistered-=WindowUnionRegistered;registry.HeroUnionUnregistered-=WindowUnionRegistered;registry.Rebuilt-=WindowUnionsRebuilt;}
   foreach(var registry in windowDecorations)if(registry!=null){registry.DecorativeObjectRegistered-=WindowDecorationRegistered;registry.DecorativeObjectUnregistered-=WindowDecorationRegistered;registry.Rebuilt-=WindowDecorationsRebuilt;}
   windowOutposts.Clear();windowUnions.Clear();windowDecorations.Clear();
  }
  void BindWindowRegistries(){
   // JC | 2026-11-10 | 최초/레벨 교체/등록부 수명 변화 때만 같은 씬의 등록부를 연결합니다. 비활성 대상의 기존 발견 정책도 유지합니다.
   windowRegistryRefreshRequested=false;UnbindWindowRegistries();
   using var rootLease=UnityEngine.Pool.ListPool<GameObject>.Get(out var roots);gameObject.scene.GetRootGameObjects(roots);
   using var outpostLease=UnityEngine.Pool.ListPool<OutpostRegistry>.Get(out var outposts);
   using var unionLease=UnityEngine.Pool.ListPool<HeroUnionRegistry>.Get(out var unions);
   using var decorationLease=UnityEngine.Pool.ListPool<DecorativeObjectRegistry>.Get(out var decorations);
   foreach(var root in roots){
    root.GetComponentsInChildren(true,outposts);foreach(var registry in outposts){windowOutposts.Add(registry);registry.OutpostRegistered+=WindowOutpostRegistered;registry.OutpostUnregistered+=WindowOutpostRegistered;registry.Rebuilt+=WindowOutpostsRebuilt;WindowOutpostsRebuilt(registry);}
    root.GetComponentsInChildren(true,unions);foreach(var registry in unions){windowUnions.Add(registry);registry.HeroUnionRegistered+=WindowUnionRegistered;registry.HeroUnionUnregistered+=WindowUnionRegistered;registry.Rebuilt+=WindowUnionsRebuilt;WindowUnionsRebuilt(registry);}
    root.GetComponentsInChildren(true,decorations);foreach(var registry in decorations){windowDecorations.Add(registry);registry.DecorativeObjectRegistered+=WindowDecorationRegistered;registry.DecorativeObjectUnregistered+=WindowDecorationRegistered;registry.Rebuilt+=WindowDecorationsRebuilt;WindowDecorationsRebuilt(registry);}
   }
  }
  void RefreshPendingWindowRoots(){
   // JC | 2026-11-10 | 순회 중 추가 알림은 다음 갱신으로 남깁니다. 자식 추가/메시 교체 시에도 대상 지정 갱신으로 재수집할 수 있습니다.
   using var lease=UnityEngine.Pool.ListPool<Transform>.Get(out var roots);roots.AddRange(pendingWindowRoots);pendingWindowRoots.Clear();
   foreach(var root in roots)if(root!=null&&root.gameObject.scene==gameObject.scene)specialWindows.RefreshRoot(root);
  }
  void UpdateBuildingWindows(bool force=false){
   if(!Application.isPlaying||!applied)return;
   if(windowRegistryRefreshRequested)BindWindowRegistries();
   // JC | 2026-11-10 | 1초 타이머를 없애고 명시된 전체 변경만 발견합니다. 단일 건물 변경은 전체 재탐색을 예약하지 않습니다.
   if(force||windowScanRequested){windowScanRequested=false;pendingWindowRoots.Clear();specialWindows.Scan(gameObject.scene);windowRefreshFrame=Time.frameCount;return;}
   if(pendingWindowRoots.Count>0)RefreshPendingWindowRoots();
   if(windowRefreshFrame!=Time.frameCount){specialWindows.RefreshKnown();windowRefreshFrame=Time.frameCount;}
  }
  double nextCharacterScan;
  readonly List<(Light light,float intensity)> extraLights=new List<(Light,float)>();
  public static double CurrentHours => owner!=null && owner.applied ? owner.shownHours : (CurrentProfile!=null?CurrentProfile.HoursForTurn(GameManager.Instance!=null?GameManager.Instance.CurrentDay:1):8.5);
  public static JcDayNightProfile SnapshotForBattle(){
   var current=owner!=null && owner.applied?owner.Settings:CurrentProfile;
   if(current==null)return null;
   var snapshot=Instantiate(current);
   snapshot.hideFlags=HideFlags.HideAndDontSave;
   return snapshot;
  }
  public static JcDayNightProfile CurrentProfile => owner!=null?owner.profile:Resources.Load<JcDayNightProfile>("JC_DayNight_Default");
  // JC | 2026-11-05 | 저장 턴을 변경하지 않는 원경 전용 시각입니다. 편집 미리보기도 동일 계산을 사용합니다.
  public static JcDayNightController EnsureCity(Scene scene,double hours,JcDayNightProfile settings){
   JcDayNightController c=null;
   foreach(var root in scene.GetRootGameObjects()){c=root.GetComponentInChildren<JcDayNightController>(true);if(c)break;}
   if(!c){var go=new GameObject("JC_CityDayNight");SceneManager.MoveGameObjectToScene(go,scene);c=go.AddComponent<JcDayNightController>();}
   c.profile=settings;c.battle=true;c.indoor=false;c.CityWorldLighting=true;c.preview=true;c.previewHour=(float)hours;
   c.ApplyHour(hours);return c;
  }
  public static JcDayNightController EnsureBattle(Scene scene,double hours,JcDayNightProfile settings,bool isIndoor){
   // JC | 2026-11-12 | 전용 JC 테스트씬도 본게임 시간대 조명 적용기를 공유합니다. 임의 탐사씬에는 설치하지 않습니다.
   if(scene.name!="TmpBattleScene" && scene.name!="TutorialBattleScene" && !JC.BattleTesting.JcStandaloneBattleTest.IsTestScene(scene))return null;
   foreach(var root in scene.GetRootGameObjects()){
    var found=root.GetComponentInChildren<JcDayNightController>(true);if(found!=null){
     if(settings!=null){found.profile=settings;found.battle=true;found.indoor=isIndoor;found.shownHours=hours;found.ApplyHour(hours);}
     return found;
    }
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
  // JC | 2026-11-10 | 복원 순서용 목록은 유지하고 잠금 중복 여부만 직접 조회합니다. 그룹이 파괴되면 같은 GO도 다시 잠급니다.
  readonly Dictionary<GameObject,CanvasGroup> lockedGroups=new Dictionary<GameObject,CanvasGroup>();
  // JC | 2026-11-10 | 단일 파티의 소유 등록부만 보관하고 PlayerParty는 매 검사 때 읽어 교체를 반영합니다.
  PartyRegistry partyRegistry;
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
  void OnEnable(){if(profile==null)profile=Resources.Load<JcDayNightProfile>("JC_DayNight_Default");SceneManager.activeSceneChanged+=SceneChanged;EnsureSegmentObjects();
   if(Application.isPlaying)SubscribeWindowEvents(); // JC | 2026-11-10 | 편집 프리뷰에서는 런타임 생성 알림을 구독하지 않습니다.
   if(gameObject.scene.path==ScenePath||gameObject.scene.name=="TutorialExploreScene")mountainShadows=GetComponent<JcMountainShadows>()??gameObject.AddComponent<JcMountainShadows>();
  }
  public void EnsureSegmentObjects(){
   for(int i=0;i<4;i++){
    JcDayNightSegment slot=null;foreach(var child in GetComponentsInChildren<JcDayNightSegment>(true))if(child.index==i&&child.transform.parent==transform){slot=child;break;}
    if(slot==null){var go=new GameObject("구간 "+(i+1)){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);slot=go.AddComponent<JcDayNightSegment>();}
    slot.index=i;slot.controller=this;
   }
  }
  void SceneChanged(Scene oldScene,Scene newScene){if(newScene!=gameObject.scene)Cancel();}
  void OnDisable(){SceneManager.activeSceneChanged-=SceneChanged;UnsubscribeWindowEvents();Cancel();if(mountainShadows!=null)mountainShadows.Restore();}
  void OnDestroy(){Cancel();DestroyObject(BattleDraft);}
  public static void CancelForScene(Scene scene){if(owner!=null && owner.gameObject.scene==scene)owner.Cancel();}
  void LateUpdate(){
   if(Settings==null || SceneManager.GetActiveScene()!=gameObject.scene)return;
   if(!Application.isPlaying){if(preview)ApplyHour(previewHour);else if(applied)Restore();return;}
   if(battle){if(!applied||appliedProfile!=Settings||appliedRevision!=Settings.revision)ApplyHour(shownHours);if(windowScanRequested||windowRegistryRefreshRequested||pendingWindowRoots.Count>0)UpdateBuildingWindows();UpdateWindowFade();return;}
   if(!transitioning && !DHGameStateRestoreService.IsRestoring){
    var tutorial = gameObject.scene.name == "TutorialExploreScene" ? FindFirstObjectByType<TutorialTurnManager>() : null;
     // JC | 2026-11-10 | 기존 턴 공급자 우선순위를 유지하며 실제로 사용할 때만 일반 턴 관리자를 검색합니다.
     int turn;
     if(tutorial!=null)turn=tutorial.CurrentTurn;
     else if(GameManager.Instance!=null)turn=GameManager.Instance.CurrentDay;
     else {var tm=FindFirstObjectByType<TurnManager>();turn=tm!=null?tm.GetDay():1;}
    if(!applied||shownTurn!=turn||appliedProfile!=Settings||appliedRevision!=Settings.revision){shownTurn=turn;ApplyHour(Settings.HoursForTurn(turn));}
   }
   if(transitioning)LockUI();
   UpdateWindowFade();
   // JC | 2026-11-10 | 캐릭터의 기존 1초 주기는 유지하고 창문만 변경 알림/후보 재확인으로 관리합니다.
   if(Time.realtimeSinceStartupAsDouble>=nextCharacterScan){characters.ScanCharacters(gameObject.scene);nextCharacterScan=Time.realtimeSinceStartupAsDouble+1;}
   UpdateBuildingWindows();
  }
  bool Busy()=>PlayerInteractionBusy() || ModalManager.HasAny || ChatModalController.IsOpen ||
   (ChatManager.Instance!=null && ChatManager.Instance.IsRunning) ||
   (DHWorldEventRuntimeManager.Instance!=null && DHWorldEventRuntimeManager.Instance.IsRunning) ||
   // 턴 진행 중 예약된 조건 검사는 다음 플레이어 턴에 실행합니다. 예약 자체를 기다리면 교착됩니다.
   (SceneFadeController.Instance!=null && SceneFadeController.Instance.IsTransitioning);
  // JC | 2026-11-10 | 전환 대기 중 반복 전체 검색을 피하고 등록부 소멸·비활성화 시에만 재확보합니다.
  bool PlayerInteractionBusy(){
   if(partyRegistry==null||!partyRegistry.gameObject.activeInHierarchy)partyRegistry=FindFirstObjectByType<PartyRegistry>();
   var party=partyRegistry!=null?partyRegistry.PlayerParty:null;
   var runtime=party!=null?party.GetComponent<PartyRuntime>():null;
   return party!=null && (party.IsMoving || (runtime!=null && runtime.IsInputLocked));
  }
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
    AudioManager.Instance?.SetBgmDucking(this,true);
    AudioManager.Instance?.PlaySfxForDuration(TurnEndSfxKey,duration);
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
   }finally{AudioManager.Instance?.SetBgmDucking(this,false);transitioning=false;UnlockUI();}
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
   if(battle && !CityWorldLighting)foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var light in root.GetComponentsInChildren<Light>())
    if(light!=sun && !light.GetComponentInParent<JcBattleLampGroup>())extraLights.Add((light,light.intensity));
   applied=true;
  }
  public void ApplyHour(double hours){
   if(Settings==null||SceneManager.GetActiveScene()!=gameObject.scene)return;
   if(mountainShadows!=null)mountainShadows.ApplyNow();
   // JC | 2026-11-10 | 시각 보간마다 씬 전체를 다시 찾지 않고 발견 주기 또는 등록된 창문 갱신만 수행합니다.
   Capture();UpdateBuildingWindows();shownHours=hours;appliedProfile=Settings;appliedRevision=Settings.revision;var k=Settings.Evaluate(hours);daylight=k.daylight;
   var look=Settings.SampleSegment(hours);var baseline=Settings.SampleSegment(hours,true);
   PrepareBattleProfile();
   UpdateLampSettings(hours);
   partyWallReduction=Mathf.Clamp01(look.occludedPartyClarity * .01f);
   float characterBrightness=OutdoorBattle && BattleDraft!=null ? BattleDraft.Apply(Settings,hours,look) : 1;
   waterMultiplier=look.waterMultiplier;
   // 방위각은 광원의 위치입니다. 방향광의 forward는 빛의 진행 방향이므로 반대쪽을 향합니다.
   if(sun!=null){sun.transform.rotation=indoor?oldRotation:Quaternion.Euler(k.elevation,k.azimuth+180f,0);sun.color=indoor?Color.Lerp(oldLightColor,new Color(.70f,.79f,1),.25f*(1-daylight)):k.lightColor;sun.intensity=indoor?oldIntensity*Mathf.Lerp(.7f,1,daylight):k.intensity*Mathf.Lerp(Settings.nightLightBoost,1,daylight);sun.shadowStrength=Mathf.Lerp(.45f,oldShadow,daylight);}
   // 실외 구간 값은 최종 색상·강도입니다. 시간 키의 일몰색을 다시 곱하면 전환 중 붉은색이 증폭됩니다.
   if(sun!=null){sun.color=indoor?JcDayNightProfile.CorrectColor(sun.color,look.lightColor,baseline.lightColor):look.lightColor;sun.intensity=indoor?sun.intensity*look.lightIntensity/Mathf.Max(.0001f,baseline.lightIntensity):look.lightIntensity;sun.shadowStrength=Mathf.Clamp01(sun.shadowStrength*look.shadowMultiplier);}
   // JC | 2026-11-04 | 태양·달 교대 중 회전은 직접광·그림자가 꺼진 순간에 수행하고 환경광은 유지합니다.
   if(sun!=null&&!indoor){float handoff=Settings.CelestialHandoffMultiplier(hours);sun.intensity*=handoff;sun.shadowStrength*=handoff;}
   Color sky=Color.Lerp(Settings.nightSky,Settings.daySky,daylight);
   Color horizon=Color.Lerp(Settings.nightHorizon,Settings.dayHorizon,daylight);
   float sunset=Mathf.Clamp01((k.lightColor.r-k.lightColor.b)*1.3f);horizon=Color.Lerp(horizon,new Color(.85f,.44f,.28f),sunset);
   Color ground=Color.Lerp(Settings.nightGround,Settings.dayGround,daylight);
   float boost=Mathf.Lerp(Settings.nightAmbientBoost,1,daylight);sky*=boost;horizon*=boost;ground*=boost;
   sky=JcDayNightProfile.CorrectColor(sky,look.sky,baseline.sky);horizon=JcDayNightProfile.CorrectColor(horizon,look.horizon,baseline.horizon);ground=JcDayNightProfile.CorrectColor(ground,look.ground,baseline.ground);
   if(!indoor){sky=look.sky;horizon=look.horizon;ground=look.ground;}
   if(indoor){sky=Color.Lerp(oldSky,sky,.35f);horizon=Color.Lerp(oldEquator/ .55f,horizon,.35f);ground=Color.Lerp(oldGround,ground,.35f);}
   float secondarySun = OutdoorBattle && BattleDraft ? BattleDraft.SecondarySun(Settings,hours) : 1f;
   foreach(var entry in extraLights)if(entry.light!=null)
    entry.light.intensity=entry.intensity*(indoor?1:Mathf.Lerp(.3f,1,daylight))*(entry.light.type==LightType.Directional?secondarySun:1f);
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
   Shader.SetGlobalVector("_JcNightReadability",new Vector4(look.characterFill,indoor?1:Mathf.Lerp(.66f,1,daylight)*characterBrightness,look.npcFill,look.enemyFill));
   // JC | 2026-11-05 | 원경 시간대 변경이 실내/캐릭터까지 후처리로 착색하지 않게 합니다.
   if(!CityWorldLighting)screenColor.Apply(gameObject.scene,look);
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
   // JC | 2026-11-10 | 매 프레임 새 UI도 수집하되 Canvas 하위 Selectable의 임시 배열은 재사용 목록으로 바꿉니다.
   using var selectableLease=UnityEngine.Pool.ListPool<Selectable>.Get(out var selectables);
   foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)){
    if(!canvas.isRootCanvas||canvas.renderMode==RenderMode.WorldSpace)continue;
    LockGroup(canvas.gameObject,Settings.disabledUIAlpha);
    canvas.GetComponentsInChildren(true,selectables);
    foreach(var selectable in selectables)LockGroup(selectable.gameObject,1);
   }
  }
  void LockGroup(GameObject go,float dim){
   // JC | 2026-11-10 | 선형 검색과 캡처 람다를 없애되 기존 CanvasGroup이 살아 있을 때만 중복 잠금을 생략합니다.
   if(lockedGroups.TryGetValue(go,out var locked)&&locked!=null)return;
   // 이동 UI와 공유하는 그룹은 원래 제어자에 연출 잠금을 합산합니다.
   // 연출 도중 변경된 기본 잠금 상태를 종료 시 그대로 반영합니다.
   var g=go.GetComponent<CanvasGroup>();bool added=g==null;if(added)g=go.AddComponent<CanvasGroup>();
   UIInputBlocker blocker=null;
   if(!added)foreach(var candidate in UnityEngine.Object.FindObjectsByType<UIInputBlocker>(FindObjectsInactive.Include,FindObjectsSortMode.None))
    if(candidate.Controls(g)){blocker=candidate;break;}
   locks.Add(new UILock{group=g,blocker=blocker,added=added,alpha=g.alpha,interactable=g.interactable,blocks=g.blocksRaycasts});
   lockedGroups[go]=g; // JC | 2026-11-10 | 복원 목록과 중복 조회를 동일한 잠금 수명으로 관리합니다.
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
   lockedGroups.Clear(); // JC | 2026-11-10 | 종료·취소 후 다음 전환에서 모든 대상을 다시 잠글 수 있게 합니다.
   if(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject==null && selection!=null && selection.activeInHierarchy){var s=selection.GetComponent<Selectable>();if(s!=null && s.IsInteractable())EventSystem.current.SetSelectedGameObject(selection);}
   selection=null;
  }
  public void Cancel(){AudioManager.Instance?.SetBgmDucking(this,false);transitioning=false;UnlockUI();Restore();}
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
   windowScanRequested=true;windowRegistryRefreshRequested=true;pendingWindowRoots.Clear();windowRefreshFrame=-1; // JC | 2026-11-10 | 취소 후 재진입에서 최초 발견·등록부 연결을 다시 수행합니다.
   DestroyObject(skybox);DestroyObject(cube);skybox=null;cube=null;applied=false;shownTurn=-1;
   if(owner==this){Shader.SetGlobalVector("_JcDayNight",Vector4.zero);Shader.SetGlobalVector("_JcWindowLight",Vector4.zero);Shader.SetGlobalVector("_JcNightReadability",Vector4.zero);owner=null;}
  }
  static void DestroyObject(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
 }
}

