using System;
using System.Collections.Generic;
using UnityEngine;
using JC.BattleTesting;

/// <summary>본게임·튜토리얼·테스트씬이 공유하는 카메라와 스킬 UI 연출. 전투 생성·승패·저장은 변경하지 않습니다.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class BattlePresentationController : MonoBehaviour
{
    [Tooltip("이 씬의 전투 관리자입니다. 턴 이벤트를 연결합니다.")]
    public BattleSceneManager battle;
    [Tooltip("스킬 시전 시 이동하고 복귀할 전투 카메라입니다.")]
    public Camera previewCamera;
    [Tooltip("아군 배치 기준점입니다. 턴 카메라 구도 계산에 사용합니다.")]
    public Transform playerPlace;
    [Tooltip("적 배치 기준점입니다. 턴 카메라 구도 계산에 사용합니다.")]
    public Transform enemyPlace;
    [Tooltip("턴 전환에 사용할 공용 카메라 구도 프로필입니다. 테스트 단축키는 Preview에서만 처리합니다.")]
    public JcBattleCameraProfile cameraProfile;
    [InspectorName("공통 스킬 후딜 (초)"), Min(0f), Tooltip("플레이어·적 스킬 종료 후 카메라 복귀와 다음 턴 진행 전 대기합니다. 개별 후딜과는 더하지 않고 긴 값을 사용합니다.")]
    public float postSkillDelay = 0.4f;
    [Header("턴 카메라")]
    [Tooltip("플레이어 턴에는 A, 적 턴에는 적 진영 뒤의 B 구도로 이동합니다.")]
    public bool useTurnCamera = true;
    [InspectorName("카메라 이동 시간 (초)")]
    [Min(0.01f), Tooltip("카메라가 출발 위치에서 도착 위치까지 이동하는 전체 시간입니다.")]
    public float turnCameraTransitionSeconds = 0.45f;
    [InspectorName("카메라 이동 곡선")]
    [Tooltip("클릭해서 그래프를 편집합니다. 가로축은 시간(0=출발, 1=도착), 세로축은 이동 진행도(0=출발점, 1=도착점)입니다. 기울기가 가파를수록 빠르고, 완만할수록 느립니다. 시작점 (0,0)과 끝점 (1,1)은 유지하세요. 위치·회전·시야각에 함께 적용됩니다.")]
    public AnimationCurve turnCameraMoveCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));
    [Min(0f), Tooltip("적 턴 카메라 이동이 끝난 뒤 공격을 시작하기 전 대기 시간입니다.")]
    public float enemyAttackDelay = 0.5f;
    [Header("고정 카메라 공격 줌")]
    [InspectorName("공격 줌 사용"), Tooltip("턴 카메라 이동을 껐을 때 플레이어와 적의 스킬 시전 중 줌을 적용합니다.")]
    public bool useAttackZoom = true;
    [InspectorName("줌 강도"), Range(0f, 0.4f), Tooltip("0.1이면 화각을 10% 좁혀 확대합니다. 0은 확대하지 않습니다.")]
    public float attackZoomAmount = 0.08f;
    [Tooltip("줌 진입 시간(초)입니다. 0이면 즉시 이동합니다.")]
    [InspectorName("줌인 시간 (초)"), Min(0f)] public float attackZoomInSeconds = 0.18f;
    [Tooltip("원래 구도로 복귀하는 시간(초)입니다. 0이면 즉시 복귀합니다.")]
    [InspectorName("줌 복귀 시간 (초)"), Min(0f)] public float attackZoomOutSeconds = 0.25f;
    [Tooltip("줌 시작 시 카메라를 내릴 거리(월드 단위)입니다. 0이면 하강하지 않습니다.")]
    [InspectorName("줌 하강 거리"), Min(0f)] public float attackZoomDrop = 0.6f;
    [Tooltip("공격 초점 쪽으로 접근할 거리(월드 단위)입니다. 0이면 접근하지 않습니다.")]
    [InspectorName("캐릭터 쪽 접근 거리"), Min(0f)] public float attackZoomApproach = 0.6f;
    [InspectorName("캐릭터 초점 높이"), Min(0f), Tooltip("캐릭터 발 위치에서 초점을 올릴 높이입니다.")]
    public float attackFocusHeight = 1.1f;
    [InspectorName("방패병 복귀 후 카메라 대기 (초)"), Min(0f)]
    [Tooltip("방패병 연출 후 복귀 전 대기 시간(초)입니다. 0이면 추가 대기가 없습니다.")]
    public float shieldCameraReturnDelay = 0.35f;
    [InspectorName("원거리 적 공격 시 대상도 함께 표시")]
    [Tooltip("적 원거리 공격에서 시전자와 피격 대상을 함께 담습니다.")]
    public bool frameEnemyRangedTarget = true;
    // JC | 2026-11-11 | 정본 4계열 외의 기존 쌍 구도 경로에만 쓰는 항목입니다. 공통 정본 선택은 전용 조절기에서 합니다.
    [HideInInspector]
    public bool frameBlackBulletTarget = true;
    [InspectorName("원거리 공격 초점의 피격자 비중"), Range(.5f, .8f)]
    [Tooltip("원거리 적 구도의 피격 대상 비중입니다. 값이 클수록 대상을 향합니다. 블랙불릿의 화면 범위 구도는 이 값 대신 두 인물의 투영 중심을 사용합니다.")]
    public float rangedTargetFocusWeight = 0.65f;
    [Header("루미나 투사체 연출")]
    [Tooltip("루미나 투사체와 연쇄 효과를 카메라 초점이 따라갑니다.")]
    public bool followLuminaProjectile = true;
    [Tooltip("루미나 투사체를 향해 일정하게 회전하는 속도입니다. 낮출수록 천천히 전환합니다.")]
    [InspectorName("투사체 초점 회전 속도 (도/초)"), Min(1f)] public float projectileFocusSpeed = 24f;
    [Tooltip("플레어 봄 시전 전 대기 시간(초)입니다. 0이면 대기하지 않습니다.")]
    [InspectorName("플레어 봄 준비 시간 (초)"), Min(0f)] public float flareBombCastDelay = 0.35f;
    [Tooltip("체인 라이트닝 시전 전 대기 시간(초)입니다. 0이면 대기하지 않습니다.")]
    [InspectorName("체인 라이트닝 준비 시간 (초)"), Min(0f)] public float chainLightningCastDelay = 0.35f;
    [Tooltip("체인 라이트닝 종료 후 대기 시간(초)입니다. 공통 후딜과 긴 값을 사용합니다.")]
    [InspectorName("체인 라이트닝 종료 후 대기 (초)"), Min(0f)] public float chainLightningEndDelay = 0.4f;
    [InspectorName("네코밍 빛 이펙트 시 적에게 초점 이동")]
    [Tooltip("네코밍 빛 효과가 나타날 때 적 대상으로 초점을 전환합니다.")]
    public bool focusNekoLightTarget = true;
    [InspectorName("널 위해 준비했어 초점 전환 보정 (초)")]
    [Tooltip("0은 적 위 광선 등장 시점, 음수는 더 일찍, 양수는 더 늦게 적에게 초점을 넘깁니다. 1배속 기준 시간이며 전투 배속을 따릅니다.")]
    public float nekoPawFocusTimeOffset;
    [Header("스킬 패널 연출")]
    [Tooltip("대상 지정 후 왼쪽 화면 밖으로 숨기고, 다음 플레이어 턴에 다시 표시할 UI 패널입니다.")]
    public RectTransform skillPanel;
    [Min(0f), Tooltip("UI가 화면 왼쪽으로 숨거나 다시 나오는 시간입니다.")]
    public float skillPanelEntranceSeconds = 0.2f;
    [Min(0f), Tooltip("UI가 화면 밖으로 숨은 뒤 스킬 발동 전 기다리는 시간입니다.")]
    public float skillCastDelay = 0.5f;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private float savedFov;
    private BattleFlowManager flow;
    private BattleTurnBanner turnBanner;
    private Vector2 panelHome;
    private bool panelPositionInitialized;
    private bool panelRequestedVisible;
    private bool entryHudPresented;
    // JC | 2026-11-12 | 치트/일반 전투의 공통 종료 이후 좌측 HUD 재진입을 막습니다. 카메라·보상 진행은 유지합니다.
    private bool skillPanelBattleEnded;
    private bool SkillPanelBattleEnded => skillPanelBattleEnded || (flow != null && flow.IsBattleEnded);
    public void PrepareEntryHud(bool showPlayerPanel)
    {
        showPlayerPanel &= !BattleRuntimeSettings.IsAutoBattle;
        // JC | 2026-11-12 | 늦게 끝난 첫 진입 연출도 종료된 HUD를 다시 열지 않습니다.
        if (SkillPanelBattleEnded) { HideSkillPanelOnBattleEnd(); return; }
        if (panelMotion != null) StopCoroutine(panelMotion);
        panelMotion = null;
        panelRequestedVisible = showPlayerPanel;
        entryHudPresented = showPlayerPanel;
        if (skillPanel != null)
            skillPanel.anchoredPosition = showPlayerPanel ? panelHome : HiddenPanelPosition;
    }
    private Coroutine panelMotion;
    private Vector2 panelDestination;
    private Coroutine cameraMotion;
    private Coroutine turnPresentation;
    private IDisposable turnFlowLock;
    private int turnCameraView = -1;
    private bool attackZoomActive;
    public bool IsAttackCameraActive => attackZoomActive;
    public JcBossCamera BossCamera { get; set; }
    // JC | 2026-11-11 | 은행 기본뷰/환경 식별만 유지합니다. 블랙불릿 저장 구도는 아래 공통 조절기가 소유합니다.
    public JcBankBattleBackground BankBackground { get; set; }
    // JC | 2026-11-11 | 모든 전투에 같은 정본1/2 선택을 연결합니다. ASB 스킬/Timeline/피해 판정은 그대로 둡니다.
    [InspectorName("블랙불릿 공통 카메라 조절기"), Tooltip("정본1/2 선택과 조절을 모든 전투가 공유합니다. 코어 공통 연출은 제외합니다.")]
    public JcBlackBulletCameraControls blackBulletCameraControls;
    // JC | 2026-11-11 | 후방 흐림은 별도 담당자에게 시작/복귀/시간 알림만 전달합니다. 카메라 구도와 공격 판정은 유지합니다.
    public JC.BattleUI.JcSkillRearBlurController skillRearBlur;
    // JC | 2026-11-11 | 선택은 공격 시작에 확정하고 두 정본이 동일한 시간·복귀·표시 보호 경로를 사용합니다.
    private JcBlackBulletCameraCanon blackBulletCanon;
    private int blackBulletTargetSlot;
    private Matrix4x4 blackBulletWorldFromReference = Matrix4x4.identity;
    // JC | 2026-11-16 | 보스 전장의 수평 변환과 렌즈 기울기를 분리합니다. 일반전에는 전달하지 않습니다.
    private float? blackBulletBasisPitchOffset;
    private Quaternion blackBulletReferenceRotation = Quaternion.identity;
    private Vector3 blackBulletCalculationPosition;
    private bool blackBulletCloseUpActive, blackBulletCloseUpMoving;
    // JC | 2026-11-10 | 스킬 발동 즉시 현재 자세에서 전환하며 기존 대사·공격 재생과 병행합니다. 준비 이동·대기 시계는 두지 않습니다.
    private bool blackBulletCloseUpAttackRunning;
    private int blackBulletCloseUpFamily;
    private float blackBulletTransitionElapsed;
    // JC | 2026-11-10 | 공격 중에는 실제 Timeline 시계만 읽습니다. 배속/구간 속도/타격 정지가 이미 반영된 값에 다시 배속을 곱하지 않습니다.
    private UnityEngine.Playables.PlayableDirector blackBulletTimeline;
    private bool blackBulletTimelineActive;
    private double blackBulletTimelineHighWater;
    private int blackBulletClockFrame = -1;
    private BattleManager blackBulletClockBattle;
    // JC | 2026-11-10 | 실제 출발 자세와 최종 구도 계산용 가상 초기 자세를 분리해 중간 이동 없이 직접 연결합니다.
    private Vector3 blackBulletBasisPosition;
    private Quaternion blackBulletBasisRotation;
    private Vector3 blackBulletCloseUpPosition;
    private Quaternion blackBulletCloseUpRotation;
    private float blackBulletCloseUpFov;
    // JC | 2026-11-10 | 보호 영역은 공격 시작에 고정하고 초안 편집 때만 재구성합니다. 잔여 숫자는 턴 진행을 막지 않고 FOV만 보호합니다.
    private Vector3[] blackBulletBodyPoints;
    // JC | 2026-11-10 | 배치별 연속 가중치와 스킬 시작에 확정한 목적지를 전환·유지에 재사용합니다.
    // JC | 2026-11-10 | 중앙 구도는 두 인물의 실제 위치로 풀며, 메뉴 기준 위치와 열별 구도 가중치는 사용하지 않습니다.
    // JC | 2026-11-10 | 셀 열과 적 진형 방향을 구분하여, 우측 시전자→적 좌측일 때 지정 구도의 가중치를 1로 고정합니다.
    private float blackBulletGroundHeight, blackBulletColumnBlend;
    // JC | 2026-11-10 | 보간 양 끝의 셀과 보호 범위를 보존해 중앙열도 같은 두 구도를 사용합니다.
    private JcBlackBulletCameraFraming.ColumnReference? blackBulletColumnReference;
    public double BlackBulletSolveMilliseconds { get; private set; }
    public double BlackBulletCaptureMilliseconds { get; private set; }
    private Bounds blackBulletPopupBounds, blackBulletTargetBody;
    private bool blackBulletHasTargetBody;
    private bool blackBulletHasPopupBounds, blackBulletProtection, blackBulletReturning, blackBulletResidual;
    private int blackBulletPopupOwner, blackBulletSettingsHash;
    private float blackBulletResidualFov, blackBulletReferenceFov;
    private readonly List<Vector3> blackBulletLivePoints = new List<Vector3>();
    private bool blackBulletConstraintWarning;
    private Vector3[] blackBulletCloseUpPoints;
    // JC | 2026-11-10 | 실제 메시를 기준으로 구도를 풀되 고정된 공격 중에는 재사용합니다. 인스펙터/해상도 변경 때만 재계산합니다.
    private bool blackBulletFrameResolved;
    private Vector4 blackBulletFrameProjection;
    private float blackBulletFramePadding;
    private Vector3 blackBulletFramePosition;
    private Quaternion blackBulletFrameRotation;
    private float blackBulletFrameFov;
    private Vector3 attackCameraPosition;
    private Quaternion attackCameraRotation;
    private float attackCameraFov, attackCameraOrthoSize;
    private bool focusShieldGuard;
    private readonly System.Collections.Generic.List<GameObject> shieldGuardEffects = new System.Collections.Generic.List<GameObject>();
    private BattleCharactor attackFocusActor;
    private BattleCharactor attackFocusTarget;
    private readonly List<BattleCharactor> rifleBarrageUnits = new List<BattleCharactor>();
    private bool focusDebuffTarget;
    private bool frameRangedPair;
    private float attackZoomBaseFov;
    private Transform luminaProjectile;
    private JC.VFX.ChainLightningVfx luminaLightning;
    private JC.VFX.SolarPrismVfx luminaSolarPrism;
    private JC.VFX.PrismExplosionVfx luminaPrismExplosion;
    private BattleCharactor nekoLightTarget;
    private bool focusNekoBarrage;
    private JC.VFX.TaoBarrageVfx nekoBarrageEffect;
    private Bounds nekoBarrageBounds;
    private JC.VFX.PawForYouVfx nekoFallingPaw;
    // JC | 2026-11-16 | 보스의 4020/4021 낙하 구도만 공용 출력 안에서 보정합니다. 일반전·은행과 VFX 타이밍은 유지합니다.
    private bool bossNekoPawFrame, bossNekoPawDescending;
    private float bossNekoPawBlend;
    private readonly List<Vector3> bossNekoPawProtection = new List<Vector3>();
    private readonly List<Vector3> bossNekoPawLivePoints = new List<Vector3>();
    // JC | 2026-11-16 | 보스의 단일 목적지 투사체/일반 돌진에서 카메라 초점만 진형 왼쪽으로 이동합니다.
    // 실제 궤적·피해·전투 배치는 그대로 두고, 시작과 종료 때 해제해 다른 스킬에 이어지지 않게 합니다.
    private bool bossFollowProjectile, bossFollowActor, bossFollowPathReady;
    // JC | 2026-11-17 | 열/방향은 시전 때 고정하고 오프셋 크기는 공유 초안에서 매 프레임 읽어 실시간 편집을 반영합니다.
    private Vector3 bossFollowStart, bossFollowTravel, bossFollowDirection;
    private int bossFollowColumn;
    // JC | 2026-11-17 | 루미나 광역기는 실제 VFX 초점과 대상 중심으로 진행률을 구합니다. 순차 착탄에도 보정량이 역행하지 않게 보관합니다.
    private bool bossFollowArea, bossAreaFocusLive, bossAreaFlightStarted;
    private Vector3 bossAreaCenter;
    private float bossAreaColumn, bossAreaProgress;
    private readonly List<BattleCharactor> bossAreaTargets = new List<BattleCharactor>();
    private readonly List<Vector3> bossAreaProtection = new List<Vector3>();
    private readonly List<Vector3> bossAreaLivePoints = new List<Vector3>();
    // JC | 2026-11-17 | 저스티스 크래시는 실행기가 확정한 광역 경로/대상과 Timeline 구간만 관찰합니다. 이동·타격은 변경하지 않습니다.
    private bool bossJusticeSkill, bossJusticeReady, bossJusticeReturning;
    private SkillPresentationDirector.MovingAttackPlan bossJusticePlan;
    private UnityEngine.Playables.PlayableDirector bossJusticeTimeline;
    private double bossJusticeStartTime, bossJusticeEndTime;
    private float bossJusticeProgress, bossJusticeColumn;
    private readonly List<BattleCharactor> bossJusticeTargets = new List<BattleCharactor>();
    private readonly List<Vector3> bossJusticeProtection = new List<Vector3>();
    private readonly List<Vector3> bossJusticeLivePoints = new List<Vector3>();
    // JC | 2026-11-17 | 시전자 렌더러 목록은 한 번만 수집하고 이동 중 bounds만 읽어 매 프레임 배열 할당을 피합니다.
    private Renderer[] bossJusticeActorRenderers;
    // JC | 2026-11-17 | 타격 종료 구도를 한 번 보관하고 실제 귀환 진행률로 기본뷰에 수렴시킵니다. 후열 좌측 등 고정 칸에 의존하지 않습니다.
    private bool bossJusticeReturnCaptured;
    // JC | 2026-11-17 | 시전자 중심의 준비 줌에서 실제 진입 이동에 맞춰 기존 타격 구도로 선형 전환합니다.
    private Vector3 bossJusticeAttackPosition, bossJusticeCastPosition;
    private float bossJusticeCastFov;
    private Pose bossJusticeReturnPose;
    private float bossJusticeReturnFov, bossJusticeReturnProgress;
    // JC | 2026-11-17 | 네코밍 광역 구도는 발사 시 기준 위치와 가상 초점을 함께 이동하여 사용자가 맞춘 회전을 유지합니다.
    private Vector3 bossBarrageBasePosition, bossBarrageOffset;
    private readonly List<BattleCharactor> bossBarrageTargets = new List<BattleCharactor>();
    private readonly List<Vector3> bossBarrageProtection = new List<Vector3>();
    private readonly List<Vector3> bossBarrageLivePoints = new List<Vector3>();
    private bool focusNekoHeal;
    private readonly List<Transform> nekoHealProjectiles = new List<Transform>();
    private readonly List<BattleCharactor> nekoHealTargets = new List<BattleCharactor>();
    private JC.VFX.JcVfxPartSequence nekoPawSequence;
    private Coroutine nekoPawFocusRoutine;
    private bool projectileFocusLatched;
    private Vector3 projectileFocusPoint;
    private bool followAttackFocus;
    public static BattlePresentationController FindInScene(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var controller in root.GetComponentsInChildren<BattlePresentationController>(false))
                if (controller.isActiveAndEnabled) return controller;
        return null;
    }

    // 기획팀 테스트씬은 원래 Preview와 직렬화 설정을 그대로 사용합니다.
    public static BattlePresentationPreview FindPreviewInScene(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var preview in root.GetComponentsInChildren<BattlePresentationPreview>(false))
                if (preview.isActiveAndEnabled) return preview;
        return null;
    }

    private T FindSceneComponent<T>() where T : Component
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }

    private void Awake()
    {
        if (previewCamera != null)
        {
            savedPosition = previewCamera.transform.position;
            savedRotation = previewCamera.transform.rotation;
            savedFov = previewCamera.fieldOfView;
        }
        if (skillPanel != null)
        {
            panelHome = skillPanel.anchoredPosition;
            skillPanel.anchoredPosition = HiddenPanelPosition;
            panelPositionInitialized = true;
        }
        flow = battle != null ? battle.GetComponent<BattleFlowManager>() : null;
        BattleRuntimeSettings.AutoBattleChanged += HandleAutoBattleChanged;
        // JC | 2026-11-12 | 스킬을 거치지 않는 Shift+9/0도 같은 전투 종료 이벤트에서 패널 전체를 닫습니다.
        if (flow != null)
        {
            flow.OnTurnStarted += HandleTurnStarted;
            flow.OnBattleEnded += HandleBattleEnded;
            if (flow.IsBattleEnded) HideSkillPanelOnBattleEnd();
        }
        turnBanner = FindSceneComponent<BattleTurnBanner>();
        if (turnBanner != null) turnBanner.TurnBannerShown += HandleTurnBannerShown;
        // JC | 2026-11-16 | 기존 비행 목적지 이벤트만 구독하며 전투/투사체 실행 경로는 변경하지 않습니다.
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightDestinationResolved += HandleBossProjectileDestination;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightStarted += HandleProjectileStarted;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightFinished += HandleProjectileFinished;
        UnitEffectPresenter.EffectSpawned += HandleSkillEffectSpawned;
        BattleVisualDirector.HealPresented += HandleHealPresented;
    }

    private void LateUpdate()
    {
        // JC | 2026-11-16 | 런타임 연결 카메라도 공통 출력에 등록하며 렌더 임시값을 다음 계산에 누적하지 않습니다.
        RegisterCameraOutput();
        RestoreCameraOverlay();
        // JC | 2026-11-10 | 복귀 후 남은 숫자도 보존하되 전투 턴/팝업 수명은 늘리지 않습니다.
        UpdateBlackBulletResidual();
        // JC | 2026-11-10 | 스킬 발동부터 직접 전환을 기존 대사·공격과 병행합니다. 전환 중에는 쌍 구도 보정과 경쟁하지 않습니다.
        if (blackBulletCloseUpActive)
        {
            // JC | 2026-11-10 | 메뉴/콘솔 정지 중에는 초기 구도 보정까지 멈추고, 재개 후 같은 공격 진행 위치를 이어갑니다.
            if (Time.timeScale <= 0f) return;
            TickBlackBulletCloseUp(ReadBlackBulletCloseUpDelta());
            if (blackBulletCloseUpActive) return;
        }
        if (!attackZoomActive || !followAttackFocus || previewCamera == null || attackFocusActor == null) return;
        // JC | 2026-11-17 | 타격 후에는 멀어지는 시전자와 적을 함께 감싸는 화각 계산을 끝냅니다.
        // 귀환 중 전장을 계속 넓히던 이전 보호 경로 대신, 해당 시전 직전의 구도로 위치·회전·화각을 함께 되돌립니다.
        // JC | 2026-11-17 | 아직 경로가 시작되지 않은 준비 모션에서 후열 시전자를 향해 무대 밖으로 회전하지 않습니다.
        if (bossJusticeSkill && !bossJusticeReady) return;
        if (bossJusticeReady)
        {
            UpdateBossJusticePhase();
            if (bossJusticeReturning)
            {
                ApplyBossJusticeReturn();
                return;
            }
        }
        nekoHealProjectiles.RemoveAll(projectile => projectile == null);
        // JC | 2026-11-16 | 보스도 기존 일반 전투의 추적·보간식을 사용합니다.
        // JC | 2026-11-17 | 크래시는 준비뷰에서 접근할 때만 보간하며 기존 타격 위치는 유지합니다.
        var position = bossJusticeReady ? bossJusticeAttackPosition : previewCamera.transform.position;
        var rotation = previewCamera.transform.rotation;
        float fov = previewCamera.fieldOfView;
        // JC | 2026-11-17 | 매 프레임 기준 위치에서 계산하여 보정이 누적되지 않으며, 기존 최종 출력 한 곳으로 전달합니다.
        if (BossCamera && focusNekoBarrage)
        {
            Vector2 requested = BossCamera.Draft.nekoBarrageOffset;
            bossBarrageOffset = Vector3.Lerp(bossBarrageOffset, new Vector3(requested.x, requested.y, 0f),
                1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            position = bossBarrageBasePosition + bossBarrageOffset;
        }
        // JC | 2026-11-06 | ASB 공용 연출 경로 안에서 블랙불릿의 화면 범위를 계산합니다.
        // 별도 카메라 제어나 각도 임계값 없이 진입·유지 모두 같은 구도 계산을 사용합니다.
        if (UseProjectedPairFraming)
        {
            ResolveProjectedPair(position, out var targetRotation, out var targetFov);
            float blend = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            rotation = Quaternion.Slerp(rotation, targetRotation, blend);
            fov = Mathf.Lerp(fov, targetFov, blend);
        }
        // JC | 2026-11-16 | 낙하부터 착탄까지 보정을 0.25초에 걸쳐 적용하고 효과 종료에도 유지해 화각이 되튀지 않게 합니다.
        if (bossNekoPawFrame)
        {
            if (nekoFallingPaw && (nekoFallingPaw.DropDescending || nekoFallingPaw.DropImpacted)) bossNekoPawDescending = true;
            else if (!nekoFallingPaw && nekoLightTarget) bossNekoPawDescending = true;
            bossNekoPawBlend = Mathf.MoveTowards(bossNekoPawBlend, bossNekoPawDescending ? 1f : 0f, Time.unscaledDeltaTime / .25f);
        }
        Vector3 direction = AttackFocusPosition - position;
        // JC | 2026-11-17 | 위치와 초점의 같은 이동량이 상쇄되어 기존 추적 회전이 유지됩니다.
        if (BossCamera && focusNekoBarrage) direction += bossBarrageOffset;
        if (!UseProjectedPairFraming && direction.sqrMagnitude > .001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            // JC | 2026-11-16 | 기존 추적 목표에만 하향 2.5도를 더합니다. 현재 회전에 누적하지 않고 기존 회전 보간을 유지합니다.
            if (bossNekoPawFrame) targetRotation *= Quaternion.Euler(2.5f * bossNekoPawBlend, 0f, 0f);
            rotation = projectileFocusLatched
                ? Quaternion.RotateTowards(previewCamera.transform.rotation, targetRotation,
                    projectileFocusSpeed * Time.unscaledDeltaTime)
                : Quaternion.Slerp(previewCamera.transform.rotation, targetRotation,
                    1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        }
        if (!UseProjectedPairFraming && (focusNekoBarrage || (frameRangedPair && attackFocusTarget != null) || (focusNekoHeal && (nekoHealProjectiles.Count > 0 || nekoHealTargets.Count > 0))))
        {
            float neededFov = Mathf.Max(attackZoomBaseFov,
                RequiredPairFov(position, rotation));
            fov = Mathf.Lerp(fov, neededFov,
                1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            // JC | 2026-11-17 | 보스 광역 구도 이동 중에도 필요한 표시 화각 아래로 내려가지 않게 합니다.
            if (BossCamera && focusNekoBarrage) fov = Mathf.Max(fov, neededFov);
        }
        if (bossNekoPawFrame && bossNekoPawBlend > 0f)
        {
            // JC | 2026-11-16 | 낙하 전 화각에서 최대 12%만 좁히되 대상·낙하 초점·실제 피해 숫자의 표시 여유를 우선합니다.
            float desiredFov = Mathf.Max(attackZoomBaseFov * Mathf.Lerp(1f, .88f, bossNekoPawBlend),
                RequiredBossNekoPawFov(position, rotation));
            fov = Mathf.Lerp(fov, desiredFov, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        }
        // JC | 2026-11-17 | 광역 초점 이동 뒤 실제 대상과 피해 숫자의 표시 범위를 최종 회전 기준으로 확인합니다.
        if (bossFollowArea && bossAreaFlightStarted)
        {
            bossAreaLivePoints.Clear();
            bossAreaLivePoints.AddRange(bossAreaProtection);
            bossAreaLivePoints.Add(projectileFocusPoint);
            foreach (var unit in bossAreaTargets)
                if (unit) DamagePopupEffect.AppendActiveCameraPoints(unit.transform.GetInstanceID(), bossAreaLivePoints);
            float required = RequiredAreaPointsFov(bossAreaLivePoints, position, rotation);
            fov = Mathf.Max(required, Mathf.Lerp(fov, Mathf.Max(attackZoomBaseFov, required),
                1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)));
        }
        // JC | 2026-11-17 | 크래시 초점을 왼쪽에 남겨도 이동 중 저스티스·적·피해 숫자는 최종 회전의 화각 안에 유지합니다.
        if (bossJusticeReady)
        {
            bossJusticeLivePoints.Clear();
            bossJusticeLivePoints.AddRange(bossJusticeProtection);
            foreach (var renderer in bossJusticeActorRenderers)
                if (renderer && renderer.enabled && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    for (int i = 0; i < 8; i++) bossJusticeLivePoints.Add(JcBlackBulletCameraFraming.Corner(renderer.bounds, i));
            foreach (var unit in bossJusticeTargets)
                if (unit) DamagePopupEffect.AppendActiveCameraPoints(unit.transform.GetInstanceID(), bossJusticeLivePoints);
            float required = RequiredAreaPointsFov(bossJusticeLivePoints, position, rotation);
            fov = Mathf.Max(required, Mathf.Lerp(fov, Mathf.Max(attackZoomBaseFov, required),
                1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)));
        }
        // JC | 2026-11-17 | 준비 줌을 출발점으로 사용하여 발동 직후 기본뷰로 튀지 않고 기존 타격 구도까지 확대합니다.
        if (bossJusticeReady)
        {
            float blend = BossJusticeStartBlend();
            position = Vector3.Lerp(bossJusticeCastPosition, position, blend);
            rotation = Quaternion.Slerp(attackCameraRotation, rotation, blend);
            fov = Mathf.Lerp(bossJusticeCastFov, fov, blend);
        }
        ApplyCameraPose(position,rotation,fov);
    }

    // JC | 2026-11-17 | 실제 접근 진행률을 이징 없이 사용합니다. 준비 줌을 유지하다 이동 시작과 함께 선형 확대합니다.
    private float BossJusticeStartBlend()
    {
        if (bossJusticeTimeline && bossJusticeTimeline.time >= bossJusticeStartTime) return 1f;
        var travel = Vector3.ProjectOnPlane(bossJusticePlan.PathPoints.Entry - bossJusticePlan.OriginPosition, Vector3.up);
        if (travel.sqrMagnitude < .0001f) return 1f;
        var moved = Vector3.ProjectOnPlane(attackFocusActor.transform.position - bossJusticePlan.OriginPosition, Vector3.up);
        return Mathf.Clamp01(Vector3.Dot(moved, travel) / travel.sqrMagnitude);
    }

    // JC | 2026-11-16 | 보스 낙하 스킬의 보호점은 시작 시 캡처하고 현재 초점/숫자만 갱신합니다. 별도 카메라 작성 경로는 만들지 않습니다.
    private void CaptureBossNekoPawProtection()
    {
        bossNekoPawProtection.Clear();
        if (!attackFocusTarget) return;
        foreach (var renderer in attackFocusTarget.GetComponentsInChildren<Renderer>())
            if (renderer.enabled && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                for (int i = 0; i < 8; i++) bossNekoPawProtection.Add(JcBlackBulletCameraFraming.Corner(renderer.bounds, i));
        var visual = FindSceneComponent<BattleVisualDirector>();
        if (visual && visual.TryGetDamageCameraEnvelope(attackFocusTarget.transform, 4, .3f, attackCameraRotation, out var popup))
            for (int i = 0; i < 8; i++) bossNekoPawProtection.Add(JcBlackBulletCameraFraming.Corner(popup, i));
    }

    private float RequiredBossNekoPawFov(Vector3 position, Quaternion rotation)
    {
        // JC | 2026-11-16 | 화면 가장자리 8%를 남겨 착탄 효과와 숫자를 보호합니다. 상승 중 화면 밖으로 나가는 기존 연출은 변경하지 않습니다.
        bossNekoPawLivePoints.Clear();
        bossNekoPawLivePoints.AddRange(bossNekoPawProtection);
        var effect = new Bounds(AttackFocusPosition, Vector3.one * 2.4f);
        for (int i = 0; i < 8; i++) bossNekoPawLivePoints.Add(JcBlackBulletCameraFraming.Corner(effect, i));
        if (attackFocusTarget) DamagePopupEffect.AppendActiveCameraPoints(attackFocusTarget.transform.GetInstanceID(), bossNekoPawLivePoints);
        float tangent = 0f;
        var inverse = Quaternion.Inverse(rotation);
        foreach (var point in bossNekoPawLivePoints)
        {
            var local = inverse * (point - position);
            float depth = Mathf.Max(previewCamera.nearClipPlane, local.z);
            tangent = Mathf.Max(tangent, Mathf.Max(Mathf.Abs(local.y), Mathf.Abs(local.x) / Mathf.Max(.1f, previewCamera.aspect)) / (depth * .84f));
        }
        return 2f * Mathf.Atan(tangent) * Mathf.Rad2Deg;
    }

    // JC | 2026-11-06 | 블랙불릿만 시작 시 확정한 두 인물의 구도를 복귀까지 보관합니다.
    // 표적 소멸이 시전자 단독 구도로 전환되는 조건이 되지 않게 합니다.
    // JC | 2026-11-11 | 코어·다른 캐릭터 경로는 유지하고, 두 정본의 원점 변환에 같은 앵커를 사용합니다.
    private bool blackBulletPairActive;
    private Vector3 blackBulletActorAnchor;
    private Vector3 blackBulletTargetAnchor;
    private bool UseProjectedPairFraming => blackBulletPairActive;

    private void ResolveProjectedPair(Vector3 position, out Quaternion rotation, out float fov)
    {
        // JC | 2026-11-06 | 근접 젬 스킬도 출발점과 표적을 포함한 시작 범위를 사용합니다.
        // 접근할수록 두 신체 사이가 좁아져 과확대되거나, 복귀 동작에 따라 재확대되는 일을 막습니다.
        ResolvePairFraming(blackBulletActorAnchor, blackBulletTargetAnchor,
            position, previewCamera.aspect, out rotation, out fov);
    }

    // JC | 2026-11-06 | 월드 좌표의 피격자 가중 평균은 원근 투영 후 중심과 다릅니다.
    // 기존 신체 여유(반폭 .6, 발 -.25~머리 2.45)를 가진 두 상자의 투영 범위를
    // 가운데에 맞추고 화면의 84% 안에 담습니다. 기존 FOV를 하한으로 쓰지 않아
    // 두 인물이 가까우면 실제로 좁은 화각을 선택합니다. 카메라에 값을 쓰지 않는 순수 계산입니다.
    private static void ResolvePairFraming(Vector3 actor, Vector3 target, Vector3 position,
        float aspect, out Quaternion rotation, out float fov)
    {
        rotation = Quaternion.LookRotation((actor + target) * .5f + Vector3.up * 1.1f - position);
        Vector2 minimum = default, maximum = default;
        for (int iteration = 0; iteration < 10; iteration++)
        {
            PairProjection(actor, target, position, rotation, out minimum, out maximum);
            var center = (minimum + maximum) * .5f;
            rotation = Quaternion.LookRotation(rotation * new Vector3(center.x, center.y, 1f));
        }
        PairProjection(actor, target, position, rotation, out minimum, out maximum);
        float tangent = Mathf.Max(Mathf.Max(Mathf.Abs(minimum.y), Mathf.Abs(maximum.y)),
            Mathf.Max(Mathf.Abs(minimum.x), Mathf.Abs(maximum.x)) / Mathf.Max(.01f, aspect));
        fov = 2f * Mathf.Atan(tangent / .84f) * Mathf.Rad2Deg;
    }

    // JC | 2026-11-06 | 가까운 시전자와 먼 표적 각각의 깊이로 투영합니다.
    // 발·머리 두 점만 확대하던 계산과 달리 신체 폭과 깊이를 함께 포함하며 매 프레임 할당하지 않습니다.
    private static void PairProjection(Vector3 actor, Vector3 target, Vector3 position,
        Quaternion rotation, out Vector2 minimum, out Vector2 maximum)
    {
        minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var inverse = Quaternion.Inverse(rotation);
        for (int unit = 0; unit < 2; unit++)
            for (int corner = 0; corner < 8; corner++)
            {
                var point = (unit == 0 ? actor : target) + new Vector3(
                    (corner & 1) == 0 ? -.6f : .6f,
                    (corner & 2) == 0 ? -.25f : 2.45f,
                    (corner & 4) == 0 ? -.6f : .6f);
                var local = inverse * (point - position);
                var projected = new Vector2(local.x, local.y) / Mathf.Max(.01f, local.z);
                minimum = Vector2.Min(minimum, projected);
                maximum = Vector2.Max(maximum, projected);
            }
    }

    // JC | 2026-11-16 | 기본뷰/스킬/잔여 숫자/렌더 효과가 공유하는 유일한 Transform/FOV 출력입니다.
    // 표시 보호는 ApplyCameraPose에서만 수행하고, 타격 효과 뒤에 재보정하지 않아 일반전의 기존 결과를 유지합니다.
    public static void WriteCameraPose(Camera camera, Vector3 position, Quaternion rotation, float fov, float? orthoSize = null)
    {
        if (!camera) return;
        camera.transform.SetPositionAndRotation(position,rotation);
        camera.fieldOfView=fov;
        if (orthoSize.HasValue) camera.orthographicSize=orthoSize.Value;
    }
    static readonly Dictionary<Camera,BattlePresentationController> cameraOwners = new Dictionary<Camera,BattlePresentationController>();
    Camera outputCamera;
    Pose renderBase;
    float renderBaseFov, renderBaseSize;
    bool renderOverlay;
    public static BattlePresentationController ForCamera(Camera camera) =>
        camera && cameraOwners.TryGetValue(camera,out var owner) && owner && owner.isActiveAndEnabled ? owner : null;
    void RegisterCameraOutput()
    {
        if(outputCamera==previewCamera) return;
        UnregisterCameraOutput();outputCamera=previewCamera;
        if(outputCamera) cameraOwners[outputCamera]=this;
    }
    void UnregisterCameraOutput()
    {
        RestoreCameraOverlay();
        if(outputCamera && cameraOwners.TryGetValue(outputCamera,out var owner) && owner==this)cameraOwners.Remove(outputCamera);
        outputCamera=null;
    }
    void OnEnable()
    {
        RegisterCameraOutput();
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering+=BeginCameraOverlay;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering+=EndCameraOverlay;
    }
    void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering-=BeginCameraOverlay;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=EndCameraOverlay;
        UnregisterCameraOutput();
    }
    void BeginCameraOverlay(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        if(camera!=outputCamera || ForCamera(camera)!=this) return;
        RestoreCameraOverlay();
        renderBase=new Pose(camera.transform.position,camera.transform.rotation);
        renderBaseFov=camera.fieldOfView;renderBaseSize=camera.orthographicSize;
        var shake=camera.GetComponent<CameraShakeEffect>();var punch=camera.GetComponent<BattleCameraHitPunch>();
        Vector3 offset=shake && shake.isActiveAndEnabled ? shake.SampleRenderOffset() : Vector3.zero;
        float amount=punch && punch.isActiveAndEnabled ? punch.CurrentAmount : 0;
        // 기존 타격 줌의 무시 임계값과 식을 그대로 보존합니다.
        if(amount<=.0001f)amount=0;
        if(offset==Vector3.zero && amount==0)return;
        float fov=renderBaseFov,size=renderBaseSize;
        if(amount>0) {
            float scale=1/(1+amount);
            if(camera.orthographic)size*=scale;
            else fov=2*Mathf.Atan(Mathf.Tan(fov*.5f*Mathf.Deg2Rad)*scale)*Mathf.Rad2Deg;
        }
        WriteCameraPose(camera,renderBase.position+offset,renderBase.rotation,fov,size);
        renderOverlay=true;
    }
    void EndCameraOverlay(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        if(camera==outputCamera)RestoreCameraOverlay();
    }
    void RestoreCameraOverlay()
    {
        if(!renderOverlay || !outputCamera)return;
        renderOverlay=false;
        WriteCameraPose(outputCamera,renderBase.position,renderBase.rotation,renderBaseFov,renderBaseSize);
    }

    private void ApplyCameraPose(Vector3 position, Quaternion rotation, float fov, float blackBulletEntryBlend = 1f)
    {
        RegisterCameraOutput();
        // JC | 2026-11-16 | 모든 전장이 기존 일반 전투 표시 보호식을 적용한 뒤 공통 출력에 전달합니다.
        if ((blackBulletProtection || blackBulletResidual) && blackBulletCameraControls && blackBulletCameraControls.Draft)
        {
            RefreshBlackBulletProtection();
            if (blackBulletResidual) blackBulletResidualFov = fov;
            float required = RequiredBlackBulletFov(position, rotation, blackBulletEntryBlend);
            if (required < 0) { ReportBlackBulletConstraint(); return; }
            fov = Mathf.Max(fov,required);

        }
        WriteCameraPose(previewCamera,position,rotation,fov);
    }

    // JC | 2026-11-11 | 두 정본의 전환·복귀·잔여 숫자에 같은 가시성 보호식을 사용합니다.
    private float RequiredBlackBulletFov(Vector3 position, Quaternion rotation, float entryBlend)
    {
        float required = 0;
        var settings = blackBulletCameraControls.Draft;
        var pixels = new Vector2(previewCamera.pixelWidth,previewCamera.pixelHeight);
        if (blackBulletProtection && !blackBulletReturning && blackBulletCloseUpPoints != null)
        {
            required = JcBlackBulletCameraFraming.RequiredTransitionFov(blackBulletCloseUpPoints,position,rotation,
                previewCamera.aspect,pixels,settings,blackBulletCloseUpPosition,blackBulletCloseUpRotation,
                blackBulletCloseUpFov,entryBlend,out bool possible,previewCamera.nearClipPlane);
            if (!possible) return -1;
        }
        blackBulletLivePoints.Clear();
        DamagePopupEffect.AppendActiveCameraPoints(blackBulletPopupOwner,blackBulletLivePoints,rotation);
        if (blackBulletLivePoints.Count > 0)
        {
            float live = JcBlackBulletCameraFraming.RequiredFov(blackBulletLivePoints.ToArray(),position,rotation,
                previewCamera.aspect,pixels,settings,out bool possible,previewCamera.nearClipPlane);
            if (!possible) return -1;
            required = Mathf.Max(required,live);
        }
        return required;
    }

    // JC | 2026-11-16 | 보스 전용 마지막점 고정을 제거하고 일반 전투의 스킬별 초점 규칙을 공유합니다.
    // JC | 2026-11-16 | 진입/추적은 같은 초점 보정 한 곳을 통과하고 기존 회전 보간·화각·복귀식을 유지합니다.
    private Vector3 AttackFocusPosition => ResolveBossFollowFocus(RawAttackFocusPosition);

    private Vector3 RawAttackFocusPosition
    {
        get
        {
            if (focusShieldGuard)
                return attackFocusActor.transform.position + Vector3.up * attackFocusHeight;
            if (rifleBarrageUnits.Count > 0)
            {
                var bounds = new Bounds(attackFocusActor.transform.position, Vector3.zero);
                foreach (var unit in rifleBarrageUnits)
                    if (unit != null) bounds.Encapsulate(unit.transform.position);
                return bounds.center + Vector3.up * attackFocusHeight;
            }
            if (focusNekoHeal && nekoHealProjectiles.Count > 0)
            {
                Vector3 center = Vector3.zero;
                int count = 0;
                foreach (var projectile in nekoHealProjectiles)
                    if (projectile != null) { center += projectile.position; count++; }
                if (count > 0)
                {
                    return center/count;
                }
            }
            if (focusNekoHeal && nekoHealTargets.Count > 0)
            {
                Vector3 center = Vector3.zero;
                int count = 0;
                foreach (var target in nekoHealTargets)
                    if (target != null && !target.IsDead) { center += target.transform.position; count++; }
                if (count > 0) return center / count + Vector3.up * attackFocusHeight;
            }
            if (focusDebuffTarget && attackFocusTarget != null && !attackFocusTarget.IsDead)
                return attackFocusTarget.transform.position + Vector3.up * attackFocusHeight;
            if (focusNekoBarrage) return nekoBarrageBounds.center;
            if (focusNekoLightTarget && nekoFallingPaw != null && nekoFallingPaw.IsPlaying)
            {
                var point = nekoFallingPaw.FallingFocusPosition;
                return point;
            }
            if (focusNekoLightTarget && nekoLightTarget != null)
                return nekoLightTarget.transform.position + Vector3.up * attackFocusHeight;
            if (followLuminaProjectile)
            {
                // JC | 2026-11-17 | 마지막 수정 소멸 후에도 도착 보정을 유지하며 충전 중에는 진행을 시작하지 않습니다.
                bossAreaFocusLive = false;
                if (luminaProjectile != null)
                {
                    projectileFocusPoint = luminaProjectile.position;
                    projectileFocusLatched = true;
                }
                else if (luminaLightning != null && luminaLightning.TryGetCameraFocus(out var lightningPoint))
                {
                    projectileFocusPoint = lightningPoint;
                    projectileFocusLatched = true;
                }
                else if (luminaSolarPrism != null && luminaSolarPrism.TryGetCameraFocus(out var solarPoint))
                {
                    bossAreaFocusLive = true;
                    projectileFocusPoint = solarPoint;
                    projectileFocusLatched = true;
                }
                else if (luminaPrismExplosion != null && luminaPrismExplosion.TryGetCameraFocus(out var prismPoint))
                {
                    bossAreaFocusLive = true;
                    projectileFocusPoint = prismPoint;
                    projectileFocusLatched = true;
                }
                if (projectileFocusLatched) return projectileFocusPoint;
            }
            Vector3 focus = attackFocusActor.transform.position;
            if (frameRangedPair && attackFocusTarget != null)
                focus = Vector3.Lerp(focus, attackFocusTarget.transform.position, rangedTargetFocusWeight);
            return focus + Vector3.up * attackFocusHeight;
        }
    }

    // JC | 2026-11-16 | 실제 연출 카탈로그와 이동 판정을 재사용합니다. BB 고정 샷, 회복, 연쇄·낙하·회전 공격은 제외합니다.
    private void PrepareBossFollow(BattleCharactor actor, BattleCharactor target, SkillData skill)
    {
        ResetBossFollow();
        if (!BossCamera || !actor || !actor.IsPlayer || !target || target.IsPlayer ||
            !target.OccupiedCell || !playerPlace || skill == null) return;
        var manager = FindSceneComponent<BattleManager>();
        if (!manager) return;
        var presentation = manager.PresentationCatalog?.Get(skill.skillIndex);
        if (presentation == null) return;
        // JC | 2026-11-17 | 커스텀 VFX의 2030/2040 계열도 기존 보스 초점 계산에 연결합니다. 코어 공유 VFX는 해당하지 않습니다.
        int family = HeroSkillRules.FamilyId(skill.skillIndex);
        // JC | 2026-11-17 | 보스의 저스티스 1040/1041만 선택합니다. 실제 광역 경로를 전달받기 전에는 기존 추적을 유지합니다.
        bossJusticeSkill = actor.PresentationIndex == "10001" && family == 1040 &&
            presentation.PresentationArchetype == PresentationArchetype.MovingAttack &&
            presentation.MovingAttack?.HitMode == MovingAttackHitMode.SequentialAoE &&
            !(skill.skillKey?.StartsWith("HCS", StringComparison.Ordinal) ?? false);
        bossFollowArea = followLuminaProjectile && actor.PresentationIndex == "10002" &&
            (family == 2030 || family == 2040) && !(skill.skillKey?.StartsWith("HCS", StringComparison.Ordinal) ?? false);
        var projectile = manager.Presentation.GetProjectileVisual(actor, skill, presentation);
        // JC | 2026-11-16 | 포물선도 수평 진행은 직선이므로 높이는 그대로 추적합니다. 상공 낙하와 연쇄는 보정하지 않습니다.
        bossFollowProjectile = followLuminaProjectile && actor.PresentationIndex == "10002" && projectile != null &&
            projectile.Trajectory != ProjectileTrajectoryType.OverheadDrop &&
            projectile.DeliveryMode == ProjectileDeliveryMode.Single && !projectile.TargetFormationCenter;
        if (presentation.PresentationArchetype == PresentationArchetype.Melee && (presentation.Move?.Enabled ?? true))
        {
            manager.Presentation.ResolveSkillMovement(actor, target, skill, out var movement, out var shouldMove,
                out _, out var origin, out _);
            bossFollowActor = shouldMove && movement != null;
            if (bossFollowActor)
            {
                bossFollowStart = origin;
                var travel = Vector3.ProjectOnPlane(target.transform.position - origin, Vector3.up);
                // 기존 접근 정지 거리와 동일하게 계산하므로 타격 지점에서 100%, 귀환 시 0%가 됩니다.
                bossFollowTravel = travel.normalized * Mathf.Max(0f, travel.magnitude - movement.ApproachDistance);
                bossFollowPathReady = bossFollowTravel.sqrMagnitude > .0001f;
            }
        }
        // JC | 2026-11-16 | 적 열 0/1/2는 왼쪽/중앙/오른쪽입니다. 진형 회전도 반영하고 카메라 회전축과 혼용하지 않습니다.
        // JC | 2026-11-17 | 중앙1.8/오른쪽3.6 기본값은 보스 프로필로 이동합니다. 시전 중에도 크기 편집이 적용되도록 캐시하지 않습니다.
        bossFollowDirection = Vector3.ProjectOnPlane(-playerPlace.right, Vector3.up).normalized;
        bossFollowColumn = Mathf.Clamp(target.OccupiedCell.Coords.y, 0, 2);
    }

    private Vector3 ResolveBossFollowFocus(Vector3 raw)
    {
        // JC | 2026-11-17 | 일반 직선 돌진의 진행률 대신 크래시 전용 곡선 구간과 실제 접근/귀환 위치를 사용합니다.
        if (bossJusticeReady) return ResolveBossJusticeFocus(raw);
        // JC | 2026-11-17 | 광역 대상 열의 평균값을 보간해 선택한 대표 적에 따라 전체공격 구도가 달라지지 않게 합니다.
        if (bossFollowArea && bossAreaTargets.Count > 0)
        {
            if (!bossAreaFlightStarted && bossAreaFocusLive)
            {
                bossFollowStart = raw;
                bossFollowTravel = Vector3.ProjectOnPlane(bossAreaCenter - raw, Vector3.up);
                bossAreaFlightStarted = true;
            }
            if (!bossAreaFlightStarted) return raw;
            float travelSquared = bossFollowTravel.sqrMagnitude;
            float areaProgress = !bossAreaFocusLive || travelSquared < .0001f ? 1f :
                Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(raw - bossFollowStart, Vector3.up), bossFollowTravel) / travelSquared);
            bossAreaProgress = Mathf.Max(bossAreaProgress, areaProgress);
            var profile = BossCamera.Draft;
            float areaOffset = bossAreaColumn <= 1f ? Mathf.Lerp(0f, profile.FollowOffsetForColumn(1), bossAreaColumn) :
                Mathf.Lerp(profile.FollowOffsetForColumn(1), profile.FollowOffsetForColumn(2), bossAreaColumn - 1f);
            return raw + bossFollowDirection * (areaOffset * Mathf.SmoothStep(0f, 1f, bossAreaProgress));
        }
        if (!bossFollowPathReady || (!bossFollowActor && !(bossFollowProjectile && projectileFocusLatched))) return raw;
        var point = bossFollowActor && attackFocusActor ? attackFocusActor.transform.position : raw;
        float progress = Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(point - bossFollowStart, Vector3.up),
            bossFollowTravel) / bossFollowTravel.sqrMagnitude);
        // JC | 2026-11-16 | 출발/도착의 변화율이 0인 보간으로 강제 정지 없이 줄여 추적하며 돌진 귀환도 역순으로 풀립니다.
        // JC | 2026-11-17 | 기존 카메라 보간 전에 초점만 갱신하며, 부모 기본뷰 ApplyDraft를 호출해 스킬 카메라를 덮어쓰지 않습니다.
        float offset = BossCamera && BossCamera.Draft ? BossCamera.Draft.FollowOffsetForColumn(bossFollowColumn) : 0f;
        return raw + bossFollowDirection * (offset * Mathf.SmoothStep(0f, 1f, progress));
    }

    private void HandleBossProjectileDestination(BattleCharactor actor, Transform projectile, Vector3 destination)
    {
        if (!attackZoomActive || !bossFollowProjectile || actor != attackFocusActor || !projectile) return;
        // JC | 2026-11-16 | 실제 발사 위치/목적지 이벤트를 받아 별도 가상 궤적이나 투사체 Transform 수정 없이 진행률만 구합니다.
        bossFollowStart = projectile.position;
        bossFollowTravel = Vector3.ProjectOnPlane(destination - bossFollowStart, Vector3.up);
        bossFollowPathReady = bossFollowTravel.sqrMagnitude > .0001f;
    }

    private void ResetBossFollow()
    {
        // JC | 2026-11-17 | 정상 종료·인터럽트·다음 스킬에서 크래시의 경로와 관찰 참조를 함께 해제합니다.
        bossJusticeSkill = bossJusticeReady = bossJusticeReturning = false;
        bossJusticeReturnCaptured = false;
        bossJusticeReturnPose = default;
        bossJusticeReturnFov = bossJusticeReturnProgress = 0f;
        // JC | 2026-11-17 | 다음 시전의 배치와 기본 카메라로 준비 줌을 다시 계산합니다.
        bossJusticeCastPosition = bossJusticeAttackPosition = Vector3.zero;
        bossJusticeCastFov = 0f;
        bossJusticePlan = null;
        bossJusticeActorRenderers = null;
        bossJusticeTimeline = null;
        bossJusticeStartTime = bossJusticeEndTime = 0d;
        bossJusticeProgress = bossJusticeColumn = 0f;
        bossJusticeTargets.Clear();
        bossJusticeProtection.Clear();
        bossJusticeLivePoints.Clear();
        // JC | 2026-11-17 | 정상 복귀·중단·다음 시전 모두 광역 보정의 기준과 대상 참조를 정리합니다.
        bossFollowArea = bossAreaFocusLive = bossAreaFlightStarted = false;
        bossAreaCenter = bossBarrageBasePosition = bossBarrageOffset = Vector3.zero;
        bossAreaColumn = bossAreaProgress = 0f;
        bossAreaTargets.Clear();
        bossAreaProtection.Clear();
        bossAreaLivePoints.Clear();
        bossBarrageTargets.Clear();
        bossBarrageProtection.Clear();
        bossBarrageLivePoints.Clear();
        bossFollowProjectile = bossFollowActor = bossFollowPathReady = false;
        // JC | 2026-11-17 | 다음 스킬에 이전 대상 열이 남지 않도록 방향과 함께 초기화합니다.
        bossFollowStart = bossFollowTravel = bossFollowDirection = Vector3.zero;
        bossFollowColumn = 0;
    }

    // JC | 2026-11-17 | ASB 실행기가 이미 확정한 plan을 읽기만 합니다. 대표 적으로 경로를 재구성하거나 사망 때 중심을 다시 잡지 않습니다.
    public void PrepareBossJusticeCamera(BattleCharactor actor, SkillPresentationDirector.MovingAttackPlan plan)
    {
        if (!bossJusticeSkill || actor != attackFocusActor || plan == null || plan.Targets == null ||
            plan.Targets.Count == 0 || bossJusticeReady) return;
        bossJusticePlan = plan;
        foreach (var unit in plan.Targets)
        {
            if (!unit || bossJusticeTargets.Contains(unit)) continue;
            bossJusticeTargets.Add(unit);
            bossJusticeColumn += unit.OccupiedCell ? Mathf.Clamp(unit.OccupiedCell.Coords.y, 0, 2) : bossFollowColumn;
            foreach (var renderer in unit.GetComponentsInChildren<Renderer>())
                if (renderer.enabled && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    for (int i = 0; i < 8; i++) bossJusticeProtection.Add(JcBlackBulletCameraFraming.Corner(renderer.bounds, i));
        }
        if (bossJusticeTargets.Count == 0) return;
        bossJusticeColumn /= bossJusticeTargets.Count;
        bossJusticeActorRenderers = actor.GetComponentsInChildren<Renderer>();
        bossJusticeReady = true;
    }

    // JC | 2026-11-17 | 현재 1040 기본/강화판이 사용하는 AoE Timeline의 실제 곡선 시작/종료 시각을 받습니다. 재생기는 제어하지 않습니다.
    public void BindBossJusticeTimeline(BattleCharactor actor, UnityEngine.Playables.PlayableDirector director, double start, double end)
    {
        if (!bossJusticeReady || actor != attackFocusActor || !director || end <= start) return;
        bossJusticeTimeline = director;
        bossJusticeStartTime = start;
        bossJusticeEndTime = end;
    }

    private Vector3 ResolveBossJusticeFocus(Vector3 raw)
    {
        UpdateBossJusticePhase();
        var origin = bossJusticePlan.OriginPosition;
        var endpoint = bossJusticeReturning ? bossJusticePlan.PathPoints.Exit : bossJusticePlan.PathPoints.Entry;
        var travel = Vector3.ProjectOnPlane(endpoint - origin, Vector3.up);
        var current = Vector3.ProjectOnPlane(attackFocusActor.transform.position - origin, Vector3.up);
        float envelope = bossJusticeTimeline && bossJusticeTimeline.time >= bossJusticeStartTime && !bossJusticeReturning ? 1f :
            (travel.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector3.Dot(current, travel) / travel.sqrMagnitude) : 0f);
        var profile = BossCamera.Draft;
        float offset = bossJusticeColumn <= 1f ? Mathf.Lerp(0f, profile.FollowOffsetForColumn(1), bossJusticeColumn) :
            Mathf.Lerp(profile.FollowOffsetForColumn(1), profile.FollowOffsetForColumn(2), bossJusticeColumn - 1f);
        return ResolveBossJusticeFocusPoint(raw, bossJusticePlan.FormationCenter, bossFollowDirection,
            offset, envelope, bossJusticeProgress);
    }

    // JC | 2026-11-17 | LateUpdate의 화각 계산 전에 종료를 알 수 있도록 타격 단계 판정을 초점 계산에서 분리합니다.
    private void UpdateBossJusticePhase()
    {
        // JC | 2026-11-17 | Hold/배속은 Timeline 시각 그대로 따르며 역평가로 초점이 되튀지 않도록 이미 통과한 진행률을 유지합니다.
        if (bossJusticeTimeline)
        {
            bossJusticeProgress = Mathf.Max(bossJusticeProgress,
                Mathf.Clamp01((float)((bossJusticeTimeline.time - bossJusticeStartTime) / (bossJusticeEndTime - bossJusticeStartTime))));
            if (bossJusticeTimeline.time >= bossJusticeEndTime) bossJusticeReturning = true;
        }
    }

    private void ApplyBossJusticeReturn()
    {
        // JC | 2026-11-17 | 프레임마다 출발 카메라를 다시 잡지 않아 이동/화각이 누적되거나 귀환 끝에 지연되지 않습니다.
        if (!bossJusticeReturnCaptured)
        {
            bossJusticeReturnPose = new Pose(previewCamera.transform.position, previewCamera.transform.rotation);
            bossJusticeReturnFov = previewCamera.fieldOfView;
            bossJusticeReturnCaptured = true;
        }
        bossJusticeReturnProgress = Mathf.Max(bossJusticeReturnProgress, ResolveBossJusticeReturnProgress(
            bossJusticePlan.OriginPosition, bossJusticePlan.PathPoints.Exit, attackFocusActor.transform.position));
        float blend = Mathf.SmoothStep(0f, 1f, bossJusticeReturnProgress);
        ApplyCameraPose(Vector3.Lerp(bossJusticeReturnPose.position, attackCameraPosition, blend),
            Quaternion.Slerp(bossJusticeReturnPose.rotation, attackCameraRotation, blend),
            Mathf.Lerp(bossJusticeReturnFov, attackCameraFov, blend));
    }

    // JC | 2026-11-17 | 실제 시전 출발점/곡선 끝점을 쓰므로 아군 6칸·진형 회전/이동에도 동일하게 0→1로 복귀합니다.
    private static float ResolveBossJusticeReturnProgress(Vector3 origin, Vector3 exit, Vector3 current)
    {
        var travel = Vector3.ProjectOnPlane(exit - origin, Vector3.up);
        if (travel.sqrMagnitude <= .0001f) return 1f;
        return 1f - Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(current - origin, Vector3.up), travel) / travel.sqrMagnitude);
    }

    // JC | 2026-11-17 | 곡선 후반의 오른쪽 이탈 성분만 적 중심 쪽으로 줄입니다. 높이는 그대로 추적하고 귀환하면 보정은 0이 됩니다.
    private static Vector3 ResolveBossJusticeFocusPoint(Vector3 raw, Vector3 center, Vector3 left,
        float offset, float envelope, float sweepProgress)
    {
        float rightward = Mathf.Max(0f, Vector3.Dot(raw - center, -left));
        // JC | 2026-11-17 | 추가 이탈 억제도 기존 오프셋 크기 이내로 제한합니다. 오프셋 0은 원래 추적으로 돌아갑니다.
        return raw + left * ((offset + Mathf.Min(rightward, offset) * Mathf.SmoothStep(0f, 1f, sweepProgress)) *
            Mathf.SmoothStep(0f, 1f, envelope));
    }

    private void HandleHealPresented(BattleVisualDirector source, ASB.Work.Battle.Core.BattleHitResult result)
    {
        if (!isActiveAndEnabled || !attackZoomActive || !focusNekoHeal || attackFocusActor == null ||
            source == null || source.gameObject.scene != gameObject.scene || result?.Target == null ||
            result.Target.IsDead || !result.Target.IsPlayer) return;
        int family = HeroSkillRules.FamilyId(result.SkillIndex);
        if (family != 4010 && family != 4030) return;
        if (!nekoHealTargets.Contains(result.Target)) nekoHealTargets.Add(result.Target);
    }

    private void HandleSkillEffectSpawned(SkillEffectContext context, GameObject instance, string cueName)
    {
        if (!attackZoomActive || context == null || context.Caster != attackFocusActor ||
            context.Presentation == null || instance == null) return;
        if (focusShieldGuard && context.Presentation.SkillIndex == 200032)
            shieldGuardEffects.Add(instance);
        int family = HeroSkillRules.FamilyId(context.Presentation.SkillIndex);
        // JC | 2026-11-17 | VFX에 전달된 실제 광역 대상만 시전당 한 번 캡처합니다. 개별 수정의 착탄/소멸로 중심이 이동하지 않습니다.
        if (bossFollowArea && (family == 2030 || family == 2040) && bossAreaTargets.Count == 0 && context.Targets != null)
        {
            foreach (var unit in context.Targets)
            {
                if (!unit || unit.IsPlayer || unit.IsDead || !unit.OccupiedCell || bossAreaTargets.Contains(unit)) continue;
                bossAreaTargets.Add(unit);
                bossAreaCenter += unit.transform.position;
                bossAreaColumn += Mathf.Clamp(unit.OccupiedCell.Coords.y, 0, 2);
                foreach (var renderer in unit.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled && !(renderer is ParticleSystemRenderer) && !(renderer is TrailRenderer))
                        for (int i = 0; i < 8; i++) bossAreaProtection.Add(JcBlackBulletCameraFraming.Corner(renderer.bounds, i));
            }
            if (bossAreaTargets.Count > 0)
            {
                bossAreaCenter /= bossAreaTargets.Count;
                bossAreaColumn /= bossAreaTargets.Count;
            }
        }
        // HCS002 uses the same SolarPrismVfx flight as Lumina 2030.
        if (followLuminaProjectile && (family == 2030 || family == 2))
            luminaSolarPrism = instance.GetComponentInChildren<JC.VFX.SolarPrismVfx>(true);
        if (followLuminaProjectile && family == 2040)
            luminaPrismExplosion = instance.GetComponentInChildren<JC.VFX.PrismExplosionVfx>(true);
        if (focusNekoLightTarget && family == 4020 &&
            context.PrimaryTarget != null && !context.PrimaryTarget.IsPlayer)
        {
            ClearNekoPawSequence();
            nekoFallingPaw = instance.GetComponentInChildren<JC.VFX.PawForYouVfx>(true);
            if (nekoFallingPaw != null && nekoFallingPaw.IsFallingPaw)
            {
                nekoLightTarget = context.PrimaryTarget;
                return;
            }
            nekoPawSequence = instance.GetComponentInChildren<JC.VFX.JcVfxPartSequence>(true);
            if (nekoPawSequence != null && nekoPawSequence.TryGetTargetPartDelay(out float targetDelay))
            {
                nekoPawFocusRoutine = StartCoroutine(FocusNekoPawTarget(context.Caster,
                    context.PrimaryTarget, nekoPawSequence, Mathf.Max(0f, targetDelay + nekoPawFocusTimeOffset)));
            }
            else nekoLightTarget = context.PrimaryTarget;
        }
        if (focusNekoLightTarget && family == 4040 && cueName == "fire")
            FrameNekoBarrage(context, instance);
    }

    private void FrameNekoBarrage(SkillEffectContext context, GameObject instance)
    {
        // JC | 2026-11-17 | 상공과 지상의 빈 공간까지 큰 상자로 묶지 않고 실제 각 표시 영역을 보관합니다.
        bossBarrageTargets.Clear();
        bossBarrageProtection.Clear();
        bool hasBounds = false;
        Vector3 center = Vector3.zero;
        int count = 0;
        void Include(BattleCharactor unit)
        {
            if (unit == null || unit.IsDead || unit.IsPlayer == context.Caster.IsPlayer ||
                unit.gameObject.scene != context.Caster.gameObject.scene) return;
            Bounds unitBounds = new Bounds(unit.transform.position + Vector3.up * 1.1f,
                new Vector3(1.5f, 2.2f, 1.5f));
            if (BossCamera)
            {
                bossBarrageTargets.Add(unit);
                for (int i = 0; i < 8; i++) bossBarrageProtection.Add(JcBlackBulletCameraFraming.Corner(unitBounds, i));
            }
            if (!hasBounds) { nekoBarrageBounds = unitBounds; hasBounds = true; }
            else nekoBarrageBounds.Encapsulate(unitBounds);
            center += unit.transform.position;
            count++;
        }
        if (context.Targets != null)
            foreach (var unit in context.Targets) Include(unit);
        if (!hasBounds)
            foreach (var unit in FindObjectsOfType<BattleCharactor>()) Include(unit);
        if (!hasBounds) return;
        var barrage = instance.GetComponent<JC.VFX.TaoBarrageVfx>();
        nekoBarrageEffect = barrage;
        Vector3 top = barrage != null ? barrage.LightOrigin : center / count + Vector3.up * 7f;
        nekoBarrageBounds.Encapsulate(new Bounds(top, Vector3.one * 2f));
        if (BossCamera)
            for (int i = 0; i < 8; i++) bossBarrageProtection.Add(JcBlackBulletCameraFraming.Corner(new Bounds(top, Vector3.one * 2f), i));
        // JC | 2026-11-17 | 현재 스킬 구도를 한 번만 기준으로 잡아 위치 편집과 반복 시전의 누적을 방지합니다.
        if (BossCamera && !focusNekoBarrage)
        {
            bossBarrageBasePosition = previewCamera.transform.position;
            bossBarrageOffset = Vector3.zero;
        }
        focusNekoBarrage = true;
        nekoLightTarget = null;
    }

    private float RequiredNekoBarrageFov(Vector3 cameraPosition, Quaternion cameraRotation)
    {
        // JC | 2026-11-17 | 보스 위치 보정 후에는 실제 회전으로 범위를 검사합니다. 다른 전장의 기존 계산은 유지합니다.
        if (BossCamera)
        {
            bossBarrageLivePoints.Clear();
            bossBarrageLivePoints.AddRange(bossBarrageProtection);
            foreach (var unit in bossBarrageTargets)
                if (unit) DamagePopupEffect.AppendActiveCameraPoints(unit.transform.GetInstanceID(), bossBarrageLivePoints);
            return RequiredAreaPointsFov(bossBarrageLivePoints, cameraPosition, cameraRotation);
        }
        Quaternion inverse = Quaternion.Inverse(Quaternion.LookRotation(nekoBarrageBounds.center - cameraPosition));
        float tangent = 0f;
        Vector3 min = nekoBarrageBounds.min, max = nekoBarrageBounds.max;
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = new Vector3((i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            Vector3 local = inverse * (point - cameraPosition);
            float depth = Mathf.Max(.1f, local.z);
            tangent = Mathf.Max(tangent, Mathf.Abs(local.y) / depth,
                Mathf.Abs(local.x) / (depth * Mathf.Max(.1f, previewCamera.aspect)));
        }
        return Mathf.Clamp(2f * Mathf.Atan(tangent / .8f) * Mathf.Rad2Deg, 1f, 110f);
    }

    // JC | 2026-11-17 | 보스 광역 공격의 실제 표시점에 화면 가장자리 8% 여유를 둡니다. 스킬 판정이나 VFX 위치는 변경하지 않습니다.
    private float RequiredAreaPointsFov(List<Vector3> points, Vector3 position, Quaternion rotation)
    {
        float tangent = 0f;
        Quaternion inverse = Quaternion.Inverse(rotation);
        foreach (var point in points)
        {
            Vector3 local = inverse * (point - position);
            float depth = Mathf.Max(.1f, local.z);
            tangent = Mathf.Max(tangent, Mathf.Abs(local.y) / depth,
                Mathf.Abs(local.x) / (depth * Mathf.Max(.1f, previewCamera.aspect)));
        }
        return Mathf.Clamp(2f * Mathf.Atan(tangent / .84f) * Mathf.Rad2Deg, 1f, 110f);
    }

    private void ClearNekoPawSequence()
    {
        if (nekoPawFocusRoutine != null) StopCoroutine(nekoPawFocusRoutine);
        nekoPawFocusRoutine = null;
        nekoPawSequence = null;
    }

    private System.Collections.IEnumerator FocusNekoPawTarget(BattleCharactor caster, BattleCharactor target,
        JC.VFX.JcVfxPartSequence sequence, float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            if (!attackZoomActive || caster != attackFocusActor || sequence == null) yield break;
            yield return null;
            elapsed += Time.deltaTime * Mathf.Max(.01f, sequence.PlaybackSpeed);
        }
        if (focusNekoLightTarget && attackZoomActive && caster == attackFocusActor &&
            sequence == nekoPawSequence && target != null)
            nekoLightTarget = target;
        nekoPawFocusRoutine = null;
    }

    private void HandleProjectileStarted(BattleCharactor actor, Transform projectile)
    {
        if (attackZoomActive && focusNekoHeal && actor == attackFocusActor && projectile != null &&
            projectile.GetComponent<JC.VFX.NekoHealPawProjectile>() != null)
        {
            if (!nekoHealProjectiles.Contains(projectile)) nekoHealProjectiles.Add(projectile);
            return;
        }
        if (!attackZoomActive || !followLuminaProjectile || actor != attackFocusActor || actor.PresentationIndex != "10002") return;
        luminaProjectile = projectile;
        projectileFocusPoint = projectile.position;
        projectileFocusLatched = true;
    }

    private void HandleProjectileFinished(BattleCharactor actor, Transform projectile, Vector3 point, bool arrived)
    {
        if (nekoHealProjectiles.Remove(projectile))
        {
            return;
        }
        if (projectile != luminaProjectile) return;
        luminaProjectile = null;
        projectileFocusPoint = point;
        // JC | 2026-11-16 | 일반 전투와 동일하게 도착한 투사체만 마지막 지점을 유지합니다. 취소/회복 종료의 기존 초점 규칙을 복원합니다.
        projectileFocusLatched = arrived;
    }

    public void FollowLuminaLightning(BattleCharactor actor, JC.VFX.ChainLightningVfx effect)
    {
        if (attackZoomActive && followLuminaProjectile && actor == attackFocusActor && actor.PresentationIndex == "10002")
            luminaLightning = effect;
    }

    private System.Collections.IEnumerator WaitForLuminaCast(BattleCharactor actor, SkillData skill)
    {
        if (actor == null || !actor.IsPlayer || actor.PresentationIndex != "10002" || skill == null) yield break;
        int family = HeroSkillRules.FamilyId(skill.skillIndex);
        float delay = family == 2010 ? flareBombCastDelay : family == 2020 ? chainLightningCastDelay : 0f;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
    }

    private System.Collections.IEnumerator WaitAfterSkill(BattleCharactor actor, SkillData skill, bool useBattleTime = false)
    {
        if (skill != null && skill.skillIndex == 200032)
        {
            while (shieldGuardEffects.Exists(effect => effect != null && effect.activeInHierarchy))
                yield return null;
            yield break;
        }
        float delay = Mathf.Max(0f, postSkillDelay);
        if (actor != null && actor.IsPlayer && actor.PresentationIndex == "10002" && skill != null &&
            HeroSkillRules.FamilyId(skill.skillIndex) == 2020)
            delay = Mathf.Max(delay, chainLightningEndDelay);
        if (actor != null && !actor.IsPlayer && actor.PresentationIndex == "20003")
            delay = Mathf.Max(delay, shieldCameraReturnDelay);
        // JC | 2026-11-10 | 추가 확대가 적용된 블랙불릿은 정지 중 후딜이 끝나 복귀가 시작되지 않게 전투 시간으로 기다립니다.
        if (useBattleTime)
        {
            float elapsed = 0;
            while (elapsed < delay)
            {
                yield return null;
                elapsed += BlackBulletBattleDelta;
            }
        }
        else if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
    }

    private float RequiredPairFov(Vector3 cameraPosition, Quaternion cameraRotation)
    {
        if (focusNekoBarrage) return RequiredNekoBarrageFov(cameraPosition, cameraRotation);
        float tangent = 0f;
        Quaternion inverse = Quaternion.Inverse(cameraRotation);
        bool frameHealProjectiles = focusNekoHeal && nekoHealProjectiles.Count > 0;
        bool frameHealedUnits = focusNekoHeal && nekoHealTargets.Count > 0;
        int unitCount = rifleBarrageUnits.Count > 0 ? rifleBarrageUnits.Count
            : frameHealProjectiles ? nekoHealProjectiles.Count : frameHealedUnits ? nekoHealTargets.Count : 2;
        for (int unitIndex = 0; unitIndex < unitCount; unitIndex++)
        {
            Transform focus;
            if (frameHealProjectiles) focus = nekoHealProjectiles[unitIndex];
            else
            {
                var unit = rifleBarrageUnits.Count > 0 ? rifleBarrageUnits[unitIndex]
                    : frameHealedUnits ? nekoHealTargets[unitIndex]
                    : unitIndex == 0 ? attackFocusActor : attackFocusTarget;
                focus = unit != null ? unit.transform : null;
            }
            if (focus == null) continue;
            for (int i = 0; i < 2; i++)
            {
                float height = frameHealProjectiles ? (i == 0 ? -.55f : .55f) : (i == 0 ? 0f : 2.2f);
                Vector3 local = inverse * (focus.position + Vector3.up * height - cameraPosition);
                float depth = Mathf.Max(.1f, local.z);
                float vertical = (Mathf.Abs(local.y) + .25f) / depth;
                float horizontal = (Mathf.Abs(local.x) + .6f) / (depth * Mathf.Max(.1f, previewCamera.aspect));
                tangent = Mathf.Max(tangent, Mathf.Max(vertical, horizontal) / .76f);
            }
        }
        return Mathf.Clamp(2f * Mathf.Atan(tangent) * Mathf.Rad2Deg, 1f, 110f);
    }

    public void SelectView(int view)
    {
        if (previewCamera == null) return;
        // JC | 2026-11-10 | 다음 스킬(코어 포함)이 시작되면 이전 BB 숫자 보호의 카메라 소유권을 종료합니다.
        ReleaseBlackBulletResidual();
        CancelTurnPresentation();
        if (cameraMotion != null) StopCoroutine(cameraMotion);
        cameraMotion = null;
        turnCameraView = -1;
        if (view == 2)
        {
            ApplyCameraPose(savedPosition, savedRotation, savedFov);
            return;
        }
        if (cameraProfile == null || playerPlace == null || enemyPlace == null) return;
        var pose = view == 0 ? cameraProfile.viewA : cameraProfile.viewB;
        // JC | 2026-11-16 | 보스는 배치 이동 전의 원근 기준으로 저장한 기본뷰를 재현합니다. 은행/야외 계산은 유지합니다.
        pose.Resolve((playerPlace.position + enemyPlace.position) * .5f, out var position, out var rotation, out var fov);
        if (BossCamera) BossCamera.ResolveView(view, out position, out rotation, out fov);
        ApplyCameraPose(position, rotation, fov);
    }

    // 시작 직후에는 레이아웃 계산 전이라 패널 폭이 작게 잡힐 수 있어 충분한 최소 거리를 확보합니다.
    private Vector2 HiddenPanelPosition => panelHome - Vector2.right * (Mathf.Max(skillPanel.rect.width, 960f) + 64f);

    private void HandleTurnBannerShown(bool isPlayer)
    {
        // 턴 배너가 나타나는 프레임부터 UI를 숨깁니다.
        CancelTurnPresentation();
        if (cameraMotion != null)
        {
            StopCoroutine(cameraMotion);
            cameraMotion = null;
            turnCameraView = -1;
        }
        SetSkillPanelVisible(false);
    }

    private void HandleTurnStarted(int round, BattleCharactor actor)
    {
        // JC | 2026-11-12 | 종료 후 지연된 턴 알림으로 진입 코루틴이 다시 만들어지지 않도록 차단합니다.
        if (SkillPanelBattleEnded) { HideSkillPanelOnBattleEnd(); return; }
        if (entryHudPresented) { entryHudPresented = false; return; }
        CancelTurnPresentation();
        if (cameraMotion != null)
        {
            StopCoroutine(cameraMotion);
            turnCameraView = -1;
        }
        cameraMotion = null;
        // OnTurnStarted 직후 흐름의 대기 지점에서 적 AI 실행을 보류합니다.
        if (actor != null && !actor.IsPlayer && flow != null)
            turnFlowLock = flow.AcquireFlowLock(this, "Enemy camera transition and attack delay");
        turnPresentation = StartCoroutine(PresentTurn(actor));
    }

    private void CancelTurnPresentation()
    {
        if (turnPresentation != null) StopCoroutine(turnPresentation);
        turnPresentation = null;
        turnFlowLock?.Dispose();
        turnFlowLock = null;
    }

    private System.Collections.IEnumerator PresentTurn(BattleCharactor actor)
    {
        try
        {
            SetSkillPanelVisible(false);
            // 공유 Coroutine을 yield하면 대기자로 등록되는데, 대기자는 하나뿐이다. 앞서 멈춘 턴 연출이
            // 그 자리를 차지하고 있으면 이 연출이 영영 재개되지 않아 적 턴 잠금이 풀리지 않는다 → 조건으로 기다린다.
            if (panelMotion != null) yield return new WaitWhile(() => panelMotion != null);
            if (useTurnCamera && actor != null)
            {
                MoveCameraForTurn(actor.IsPlayer);
                if (cameraMotion != null) yield return new WaitWhile(() => cameraMotion != null);
            }
            if (actor != null && actor.IsPlayer) SetSkillPanelVisible(true);
            else if (actor != null && enemyAttackDelay > 0f)
                yield return new WaitForSecondsRealtime(enemyAttackDelay);
        }
        finally
        {
            turnFlowLock?.Dispose();
            turnFlowLock = null;
        }
        turnPresentation = null;
    }

    private void MoveCameraForTurn(bool playerTurn)
    {
        if (previewCamera == null || cameraProfile == null || playerPlace == null || enemyPlace == null) return;
        int view = playerTurn ? 0 : 1;
        // JC | 2026-11-10 | 다음 스킬(코어 포함)이 시작되면 이전 BB 숫자 보호의 카메라 소유권을 종료합니다.
        ReleaseBlackBulletResidual();
        if (turnCameraView == view) return;
        turnCameraView = view;
        var pose = playerTurn ? cameraProfile.viewA : cameraProfile.viewB;
        pose.Resolve((playerPlace.position + enemyPlace.position) * .5f,
            out var position, out var rotation, out var fov);
        // JC | 2026-11-16 | 턴 전환도 보스 기본뷰의 동일한 기준점을 사용합니다.
        if (BossCamera) BossCamera.ResolveView(view, out position, out rotation, out fov);
        if (cameraMotion != null) StopCoroutine(cameraMotion);
        cameraMotion = StartCoroutine(TransitionTurnCamera(position, rotation, fov));
    }

    private System.Collections.IEnumerator TransitionTurnCamera(Vector3 position, Quaternion rotation, float fov)
    {
        Vector3 startPosition = previewCamera.transform.position;
        Quaternion startRotation = previewCamera.transform.rotation;
        float startFov = previewCamera.fieldOfView;
        Vector3 arcCenter = (startPosition + position) * .5f;
        Vector3 arcRadius = startPosition - arcCenter;
        arcRadius.y = 0f;
        Vector3 battleCenter = playerPlace != null && enemyPlace != null
            ? (playerPlace.position + enemyPlace.position) * .5f : arcCenter;
        // 전투 중앙을 가로지르지 않고 바깥쪽으로 반원을 돕니다. 복귀도 같은 경로를 역으로 따릅니다.
        Vector3 outward = arcCenter - battleCenter;
        outward.y = 0f;
        float sweepSign = Vector3.Dot(Quaternion.AngleAxis(90f, Vector3.up) * arcRadius, outward) >= 0f ? 1f : -1f;
        Vector3 startForward = startRotation * Vector3.forward;
        Vector3 endForward = rotation * Vector3.forward;
        Vector3 startFocus = startPosition + startForward * Mathf.Max(.01f, Vector3.Dot(battleCenter - startPosition, startForward));
        Vector3 endFocus = position + endForward * Mathf.Max(.01f, Vector3.Dot(battleCenter - position, endForward));
        Quaternion startFraming = Quaternion.Inverse(Quaternion.LookRotation(startFocus - startPosition)) * startRotation;
        Quaternion endFraming = Quaternion.Inverse(Quaternion.LookRotation(endFocus - position)) * rotation;
        float duration = Mathf.Max(.01f, turnCameraTransitionSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float t = turnCameraMoveCurve != null && turnCameraMoveCurve.length > 0
                ? Mathf.Clamp01(turnCameraMoveCurve.Evaluate(progress)) : progress;
            Vector3 arcPosition = arcCenter + Quaternion.AngleAxis(180f * sweepSign * t, Vector3.up) * arcRadius;
            arcPosition.y = Mathf.Lerp(startPosition.y, position.y, t);
            Vector3 focus = Vector3.Lerp(startFocus, endFocus, t);
            Quaternion arcRotation = Quaternion.LookRotation(focus - arcPosition) *
                Quaternion.Slerp(startFraming, endFraming, t);
            ApplyCameraPose(arcPosition, arcRotation, Mathf.Lerp(startFov, fov, t));
            yield return null;
        }
        ApplyCameraPose(position, rotation, fov);
        cameraMotion = null;
    }

    public void RestoreSkillPanelAfterFailedAction()
    {
        SetSkillPanelVisible(flow != null && flow.CurrentUnit != null && flow.CurrentUnit.IsPlayer);
    }

    public System.Collections.IEnumerator RunAttackWithZoom(BattleCharactor actor, BattleCharactor target,
        SkillData skill, System.Collections.IEnumerator attack, bool counter = false)
    {
        // JC | 2026-11-10 | 다음 스킬(코어 포함)이 시작되면 이전 BB 숫자 보호의 카메라 소유권을 종료합니다.
        ReleaseBlackBulletResidual();
        bool shouldFocusDebuffTarget = skill != null && skill.skillKey == "HCS005" &&
            target != null && !target.IsDead;
        int family = skill != null ? HeroSkillRules.FamilyId(skill.skillIndex) : 0;
        bool shouldFocusNekoHeal = actor != null && actor.PresentationIndex == "10004" &&
            (family == 4010 || family == 4030);
        bool shouldFocusNekoPaw = actor != null && actor.PresentationIndex == "10004" && family == 4020;
        bool shouldFocusNekoBarrage = actor != null && actor.PresentationIndex == "10004" && family == 4040;
        // JC | 2026-11-11 | 은행/보스 여부보다 공통 정본 선택을 우선합니다. HCS 코어는 반드시 기존 공통 연출에 남깁니다.
        bool canonicalSkill = actor && actor.IsPlayer && actor.PresentationIndex == "10003" && target && !target.IsPlayer &&
            JcBlackBulletCameraProfile.Supports(family) && !(skill.skillKey?.StartsWith("HCS", StringComparison.Ordinal) ?? false) &&
            blackBulletCameraControls && blackBulletCameraControls.isActiveAndEnabled && blackBulletCameraControls.Draft &&
            previewCamera && !previewCamera.orthographic;
        bool shouldFocusShieldGuard = actor != null && skill != null && skill.skillIndex == 200032;
        bool shouldFrameRifleBarrage = actor != null && skill != null && skill.skillIndex == 200022;
        if ((useTurnCamera && !canonicalSkill && !shouldFocusDebuffTarget && !shouldFocusNekoHeal && !shouldFocusNekoPaw && !shouldFocusNekoBarrage && !shouldFrameRifleBarrage && !shouldFocusShieldGuard) || (!useAttackZoom && !canonicalSkill && !shouldFocusShieldGuard) || previewCamera == null ||
            actor == null || (attackZoomAmount <= 0f && !canonicalSkill && !shouldFocusShieldGuard) || attackZoomActive)
        {
            yield return ShowSkillSpeech(actor, skill, counter);
            yield return WaitForLuminaCast(actor, skill);
            yield return attack;
            yield return WaitAfterSkill(actor, skill);
            yield break;
        }
        attackCameraPosition = previewCamera.transform.position;
        attackCameraRotation = previewCamera.transform.rotation;
        attackCameraFov = previewCamera.fieldOfView;
        attackCameraOrthoSize = previewCamera.orthographicSize;
        // JC | 2026-11-16 | 스킬 시작마다 고정 기준을 새로 잡습니다. 레이아웃 이동은 이미 이 기본뷰에 반영되어 있습니다.
        focusShieldGuard = shouldFocusShieldGuard;
        shieldGuardEffects.Clear();
        attackFocusActor = actor;
        attackFocusTarget = target;
        // JC | 2026-11-16 | 보스 4020 계열에만 적용하고 시전마다 보간/보호 상태를 새로 만듭니다.
        bossNekoPawFrame = BossCamera && shouldFocusNekoPaw && focusNekoLightTarget;
        bossNekoPawDescending = false;
        bossNekoPawBlend = 0f;
        bossNekoPawProtection.Clear();
        if (bossNekoPawFrame) CaptureBossNekoPawProtection();
        rifleBarrageUnits.Clear();
        if (shouldFrameRifleBarrage)
            foreach (var unit in FindObjectsOfType<BattleCharactor>())
                if (unit.gameObject.scene == actor.gameObject.scene && !unit.IsDead)
                    rifleBarrageUnits.Add(unit);
        focusDebuffTarget = shouldFocusDebuffTarget;
        focusNekoHeal = shouldFocusNekoHeal;
        nekoLightTarget = null;
        focusNekoBarrage = false;
        nekoBarrageEffect = null;
        nekoBarrageBounds = default;
        nekoFallingPaw = null;
        nekoHealProjectiles.Clear();
        nekoHealTargets.Clear();
        // JC | 2026-11-13 | ASB 공통 코어 추적을 복원합니다. JC의 블랙불릿 시작점 고정은 일반 스킬에만 적용합니다.
        bool blackBulletClassSkill = !(skill?.skillKey?.StartsWith("HCS", StringComparison.Ordinal) ?? false);
        frameRangedPair = !focusShieldGuard && !focusDebuffTarget && target != null && skill != null &&
            ((frameEnemyRangedTarget && !actor.IsPlayer && target.IsPlayer &&
                !BattleManager.IsMeleeSkillRange(actor, skill)) ||
             (frameBlackBulletTarget && blackBulletClassSkill && actor.IsPlayer && actor.PresentationIndex == "10003" && !target.IsPlayer));
        if (shouldFrameRifleBarrage) frameRangedPair = true;
        // JC | 2026-11-06 | ASB 공용 연출 안의 기존 JC 블랙불릿 구도만 시작 시 확정합니다.
        // 대상 사망·파괴 이후에도 이 상태를 유지하고 RestoreAttackCamera에서 해제합니다.
        blackBulletPairActive = blackBulletClassSkill && (frameRangedPair || canonicalSkill) && actor.IsPlayer &&
            actor.PresentationIndex == "10003" && target != null && rifleBarrageUnits.Count == 0;
        if (blackBulletPairActive)
        {
            blackBulletActorAnchor = actor.transform.position;
            blackBulletTargetAnchor = target.transform.position;
        }
        // JC | 2026-11-10 | 시작 때 실제 전신/점유 셀 범위를 보존합니다. 대상 사망·근접 이동 뒤에도 확대 중심과 그리드 범위가 흔들리지 않습니다.
        bool useBlackBulletCloseUp = canonicalSkill;
        // JC | 2026-11-16 | 보스 기본뷰가 이미 배치를 따라 이동했으므로 스킬에서 오프셋을 다시 더하지 않습니다. 복귀도 이 실제 시작 구도를 사용합니다.
        blackBulletCalculationPosition = attackCameraPosition;
        if (useBlackBulletCloseUp)
        {
            // JC | 2026-11-16 | 모든 전장은 같은 정본 선택과 스킬별 계산을 사용합니다. 보스 차이는 기준 좌표 변환뿐입니다.
            blackBulletCanon = blackBulletCameraControls.Draft.canon;
            blackBulletTargetSlot = target.OccupiedCell ? JcBankSkillCameraProfile.TargetIndex(target.OccupiedCell.Coords) : -1;
            blackBulletCloseUpFamily = family;
            blackBulletPopupOwner = target.transform.GetInstanceID();
            blackBulletGroundHeight = actor.OccupiedCell ? actor.OccupiedCell.transform.position.y : actor.transform.position.y;
            blackBulletColumnBlend = actor.OccupiedCell && target.OccupiedCell ?
                Mathf.Clamp01(actor.OccupiedCell.Coords.y * .5f) * Mathf.Clamp01(target.OccupiedCell.Coords.y * .5f) : 0;
            var settings = blackBulletCameraControls.Draft;
            blackBulletBasisPitchOffset = null;
            blackBulletReferenceRotation = playerPlace ? playerPlace.rotation : Quaternion.identity;
            blackBulletWorldFromReference = playerPlace ? Matrix4x4.TRS(playerPlace.position, playerPlace.rotation, Vector3.one) *
                Matrix4x4.Translate(-settings.referencePlayerOrigin) : Matrix4x4.identity;
            // JC | 2026-11-16 | 은행 촬영 기준은 은행에만 남깁니다. 보스는 실제 기본뷰/배치 좌표가 유일한 기준입니다.
            if (BankBackground && !BossCamera && settings.referenceCamera)
            {
                settings.referenceCamera.viewA.Resolve(new Vector3(0, .3f, 0), out var referencePosition, out _, out _);
                blackBulletCalculationPosition = blackBulletWorldFromReference.MultiplyPoint3x4(referencePosition);
            }
            // JC | 2026-11-16 | 보스 기본뷰의 이동·방향 차이를 공통 촬영 계산에 전달합니다.
            // 배치 이동량은 기본뷰에 이미 포함되며, 정본2의 열별 구도는 공통 탐색 목표로 함께 계산합니다.
            if (BossCamera && settings.referenceCamera)
            {
                settings.referenceCamera.viewA.Resolve(new Vector3(0,.3f,0),out var referencePosition,out var referenceRotation,out _);
                // JC | 2026-11-16 | full 회전으로 진형까지 기울이면 평면 기반 높이/열 계산이 깨집니다.
                // Y 회전만 좌표계에 반영하고 X 차이는 공통 구도 탐색의 촬영 목표로 전달합니다.
                blackBulletReferenceRotation=Quaternion.Euler(0,Mathf.DeltaAngle(referenceRotation.eulerAngles.y,attackCameraRotation.eulerAngles.y),0);
                blackBulletBasisPitchOffset=Mathf.DeltaAngle(referenceRotation.eulerAngles.x,attackCameraRotation.eulerAngles.x);
                // JC | 2026-11-16 | 직접 촬영한 정본1은 배치 탐색을 하지 않으므로 기존 저장 구도의 상대 회전을 보존합니다.
                if (blackBulletCanon == JcBlackBulletCameraCanon.Authored)
                {
                    blackBulletReferenceRotation=attackCameraRotation*Quaternion.Inverse(referenceRotation);
                    blackBulletBasisPitchOffset=null;
                }
                blackBulletWorldFromReference=Matrix4x4.TRS(attackCameraPosition,blackBulletReferenceRotation,Vector3.one)*Matrix4x4.Translate(-referencePosition);
            }
        }
        // JC | 2026-11-16 | 보스 추적 오프셋은 이 공격의 실제 대상과 이동 설정으로만 초기화합니다.
        PrepareBossFollow(actor, target, skill);
        attackZoomActive = true;
        skillRearBlur?.BeginAttack(actor, target, skill);
        try
        {
            float scale = 1f - Mathf.Clamp(attackZoomAmount, 0f, .4f);
            Vector3 corner = blackBulletCalculationPosition + Vector3.down * Mathf.Max(0f, attackZoomDrop);
            Vector3 approach = AttackFocusPosition - corner;
            approach.y = 0f;
            Vector3 zoomPosition = corner + approach.normalized * Mathf.Min(attackZoomApproach, approach.magnitude * .5f);
            attackZoomBaseFov = Mathf.Max(1f, attackCameraFov * scale);
            float zoomFov = attackZoomBaseFov;
            Quaternion zoomRotation = attackCameraRotation;
            if (UseProjectedPairFraming)
            {
                ResolveProjectedPair(zoomPosition, out zoomRotation, out zoomFov);
            }
            else if (frameRangedPair)
                zoomFov = Mathf.Max(zoomFov, RequiredPairFov(zoomPosition,
                    Quaternion.LookRotation(AttackFocusPosition - zoomPosition)));
            // JC | 2026-11-11 | 두 정본 모두 중간 카메라 이동·준비 대기 없이 스킬 시작에서 최종 앵글로 바로 전환합니다.
            // 이전 초기 앵글은 최종 구도를 유지하는 계산 기준으로만 쓰며 카메라를 그곳으로 이동시키지 않습니다.
            if (useBlackBulletCloseUp)
                // JC | 2026-11-16 | 보스 전용 고정/재탐색 없이 일반전과 같은 스킬별 구도 계산을 사용합니다.
                BeginBlackBulletCloseUp(family,zoomPosition,zoomRotation,zoomFov);
            else
            {
                // JC | 2026-11-17 | 기존 공격 위치는 보존하고 준비 줌만 시전자의 실제 위치 기준으로 계산합니다.
                bossJusticeAttackPosition = zoomPosition;
                if (bossJusticeSkill)
                {
                    // JC | 2026-11-17 | 화각 축소에 맞춰 화면 평면에서만 이동하여 저스티스를 줌 중심으로 유지합니다.
                    // 후열에서도 카메라 하강·단독 회전으로 무대 밖을 보는 문제 없이 다른 스킬과 같은 확대량/시간을 씁니다.
                    Vector3 castFocus = actor.transform.position + Vector3.up * attackFocusHeight;
                    Vector3 screenOffset = Vector3.ProjectOnPlane(castFocus - attackCameraPosition,
                        attackCameraRotation * Vector3.forward);
                    float viewScale = previewCamera.orthographic ? scale :
                        Mathf.Tan(zoomFov * Mathf.Deg2Rad * .5f) / Mathf.Tan(attackCameraFov * Mathf.Deg2Rad * .5f);
                    bossJusticeCastPosition = attackCameraPosition + screenOffset * (1f - viewScale);
                    bossJusticeCastFov = zoomFov;
                    yield return AnimateAttackZoom(bossJusticeCastPosition,
                        Vector3.Lerp(attackCameraPosition, bossJusticeCastPosition, .5f), attackCameraRotation,
                        bossJusticeCastFov, attackCameraOrthoSize * scale, attackZoomInSeconds, false);
                }
                else
                    yield return AnimateAttackZoom(zoomPosition, corner, zoomRotation,
                        zoomFov, attackCameraOrthoSize * scale, attackZoomInSeconds, true);
                followAttackFocus = true;
            }
            yield return ShowSkillSpeech(actor, skill, counter);
            yield return WaitForLuminaCast(actor, skill);
            if (useBlackBulletCloseUp) blackBulletCloseUpAttackRunning = true;
            yield return attack;
            // JC | 2026-11-17 | 귀환 완료와 코루틴 종료가 같은 프레임이어도 마지막 기본뷰를 반영한 뒤 후딜로 넘어갑니다.
            if (bossJusticeReady && bossJusticeReturning) ApplyBossJusticeReturn();
            // JC | 2026-11-10 | 공격이 끝나면 시간 동기화만 해제하고 기존 공통 후딜·복귀를 유지합니다.
            EndBlackBulletCloseUpAttack();
            if (shouldFocusNekoBarrage)
                while (nekoBarrageEffect != null && nekoBarrageEffect.IsPlaying) yield return null;
            if (!focusNekoHeal && !focusShieldGuard) followAttackFocus = false;
            yield return WaitAfterSkill(actor, skill, useBlackBulletCloseUp);
            // JC | 2026-11-10 | 복귀 중 LateUpdate의 추가 확대가 복귀 카메라를 덮어쓰지 않도록 먼저 해제합니다.
            blackBulletCloseUpActive = false;
            blackBulletReturning = useBlackBulletCloseUp;
            followAttackFocus = false;
            focusNekoBarrage = false;
            skillRearBlur?.BeginReturn();
            // JC | 2026-11-16 | 복귀 곡선/시간도 일반 전투와 동일하게 공격 시작 기본뷰를 사용합니다.
            // JC | 2026-11-17 | 크래시는 이동 귀환 중 기본뷰에 수렴합니다. 마지막 공통 복귀가 옛 진입 코너를 다시 경유하지 않게 합니다.
            Vector3 returnCorner = bossJusticeReady ? Vector3.Lerp(previewCamera.transform.position, attackCameraPosition, .5f) : corner;
            yield return AnimateAttackZoom(attackCameraPosition, returnCorner, attackCameraRotation,
                attackCameraFov, attackCameraOrthoSize, attackZoomOutSeconds, false, useBlackBulletCloseUp);
        }
        finally
        {
            RestoreAttackCamera();
        }
    }

    // JC | 2026-11-10 | 인물 신체와 실제 점유 셀 렌더러를 담고 왕복 운동 여유를 더합니다. 다른 셀/피해 판정은 변경하지 않습니다.
    private Vector3[] CaptureBlackBulletCloseUpPoints(BattleCharactor actor)
    {
        // JC | 2026-11-10 | 실제 신체/그리드 꼭짓점으로 맞춥니다. 빈 AABB 모서리와 임의 전신 박스는 4K에서 과도한 여백을 만들었습니다.
        // 신체 자세 변화는 중심 기준 8%와 위아래 5cm, 그리드 상하 운동은 별도로 여유를 확보합니다.
        var points = new List<Vector3>();
        var center = blackBulletActorAnchor + Vector3.up * 1.1f;
        void AddBodyPoint(Vector3 point)
        {
            point = center + (point - center) * 1.08f;
            points.Add(point + Vector3.up * .05f);
            points.Add(point - Vector3.up * .05f);
        }
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            if (renderer is SkinnedMeshRenderer skin)
            {
                var mesh = new Mesh();
                // JC | 2026-11-10 | 계층 스케일 보정을 포함해 베이크한 로컬 정점을 월드로 변환합니다. 임시 메시만 해제합니다.
                skin.BakeMesh(mesh, true);
                foreach (var vertex in mesh.vertices) AddBodyPoint(skin.transform.TransformPoint(vertex));
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            }
            else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
            {
                // JC | 2026-11-10 | 읽기 불가 총 메시를 매 공격마다 읽던 오류를 제거합니다. 해당 메시의 로컬 bounds는 읽기 가능하며 외형 전체를 포함합니다.
                if (filter.sharedMesh.isReadable)
                    foreach (var vertex in filter.sharedMesh.vertices) AddBodyPoint(filter.transform.TransformPoint(vertex));
                else
                    for (int i = 0; i < 8; i++) AddBodyPoint(filter.transform.TransformPoint(JcBlackBulletCameraFraming.Corner(filter.sharedMesh.bounds, i)));
            }
        }
        // JC | 2026-11-10 | 신체의 1만여 보호점을 보수적 외피로 줄여 진입·확대·복귀 모든 프레임의 부하를 낮춥니다.
        points = new List<Vector3>(JcCameraProtectionHull.Enclose(points));
        // JC | 2026-11-10 | 활성 테두리는 약 9천 정점이므로 신체와 별도의 외피로 축약해 구도 탐색의 순간 지연을 방지합니다.
        var gridPoints = new List<Vector3>();
        if (actor.OccupiedCell)
        {
            var visuals = FindSceneComponent<BattleCellVisualSettings>();
            // JC | 2026-11-10 | 셀 하위에 캐릭터도 있으므로 무기를 그리드로 중복 계산하지 않습니다.
            var filters = actor.OccupiedCell.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter ground = null;
            foreach (var filter in filters)
                if (filter.sharedMesh && !filter.GetComponentInParent<BattleCharactor>(true) && !filter.GetComponentInParent<BattleCellBorderVisual>(true))
                { ground = filter; break; }
            // JC | 2026-11-10 | 초기 이동을 기다리지 않으므로 아직 생성 전인 현재 턴 그리드도 실제 프로필 메시로 예약합니다.
            // 시각 오브젝트와 동일하게 원본 Plane의 스케일을 복사하지 않고 부모 공간의 위치/회전만 사용합니다.
            if (ground && visuals && visuals.profile)
            {
                var config = visuals.profile.settings;
                var shape = config.For(BattleCellVisualState.CurrentTurn).shape;
                var size = new Vector2(Mathf.Max(.1f, shape.size.x - 2 * Mathf.Max(0, shape.padding)),
                    Mathf.Max(.1f, shape.size.y - 2 * Mathf.Max(0, shape.padding)));
                var mesh = new Mesh();
                try
                {
                    BattleCellBorderMesh.Build(mesh, size, shape.cornerRadius, .001f, shape.depth, shape.curveSegments, false);
                    var parentMatrix = ground.transform.parent ? ground.transform.parent.localToWorldMatrix : Matrix4x4.identity;
                    var matrix = parentMatrix * Matrix4x4.TRS(ground.transform.localPosition, ground.transform.localRotation, Vector3.one);
                    var axis = parentMatrix.MultiplyVector(Vector3.up);
                    float height = Mathf.Max(0, shape.floatHeight);
                    float amplitude = Mathf.Min(Mathf.Max(0, config.bobAmplitude), height);
                    foreach (var vertex in mesh.vertices)
                    {
                        var point = matrix.MultiplyPoint3x4(vertex);
                        gridPoints.Add(point + axis * (height - amplitude - .02f));
                        gridPoints.Add(point + axis * (height + amplitude + .02f));
                    }
                }
                finally { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            }
            foreach (var filter in filters)
            {
                if (!filter.sharedMesh || filter.GetComponentInParent<BattleCharactor>(true)) continue;
                // JC | 2026-11-10 | 진형 변경 직후에는 다음 Update에서 테두리가 켜질 수 있어 비활성 테두리도 예약합니다.
                // 숨긴 원본 Plane은 계속 제외하며, inactive 부모를 명시 조회해 이를 바닥 메시로 오인하지 않습니다.
                var gridRenderer = filter.GetComponent<Renderer>();
                var border = filter.GetComponentInParent<BattleCellBorderVisual>(true);
                if (!gridRenderer || (!border && (!gridRenderer.enabled || !filter.gameObject.activeInHierarchy))) continue;
                var lower = Vector3.zero;
                var upper = Vector3.zero;
                if (border && ground && visuals && visuals.profile)
                {
                    // 실제 왕복 범위(높이 ± 높이로 제한된 진폭)를 기준으로 합니다. 현재 파형에 진폭을 중복 가산하지 않습니다.
                    var config = visuals.profile.settings;
                    float height = Mathf.Max(0, config.For(border.presentationState).shape.floatHeight);
                    float amplitude = Mathf.Min(Mathf.Max(0, config.bobAmplitude), height);
                    var origin = ground.transform.position - border.transform.position;
                    var axis = border.transform.parent ? border.transform.parent.TransformVector(Vector3.up) : Vector3.up;
                    lower = origin + axis * (height - amplitude - .02f);
                    upper = origin + axis * (height + amplitude + .02f);
                }
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    gridPoints.Add(point + lower);
                    gridPoints.Add(point + upper);
                }
            }
        }
        points.AddRange(JcCameraProtectionHull.Enclose(gridPoints));
        // JC | 2026-11-10 | 타깃 전신을 시작 자세로 보존합니다. 피격 여유 변경은 아래 조립 단계에서 즉시 반영합니다.
        blackBulletHasTargetBody = false;
        if (attackFocusTarget)
            foreach (var renderer in attackFocusTarget.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !(renderer is SkinnedMeshRenderer || renderer is MeshRenderer)) continue;
                if (!blackBulletHasTargetBody) { blackBulletTargetBody = renderer.bounds; blackBulletHasTargetBody = true; }
                else blackBulletTargetBody.Encapsulate(renderer.bounds);
            }
        return points.ToArray();
    }

    // JC | 2026-11-10 | 스킬별 효과 범위와 숫자 프리팹의 실제 설정을 카메라 전용 초안에 결합합니다. 피해 판정/효과 자산은 수정하지 않습니다.
    private void RefreshBlackBulletProtection()
    {
        if (!blackBulletCameraControls || !blackBulletCameraControls.Draft || blackBulletBodyPoints == null) return;
        var settings = blackBulletCameraControls.Draft;
        int hash = JsonUtility.ToJson(settings).GetHashCode();
        if (hash == blackBulletSettingsHash && blackBulletCloseUpPoints != null) return;
        blackBulletSettingsHash = hash; blackBulletFrameResolved = false;
        var points = new List<Vector3>(blackBulletBodyPoints);
        // JC | 2026-11-10 | 시전자 보호점은 고정하고 적 신체·탄착·숫자만 유지할 끝 셀로 옮겨 계산합니다. 게임 오브젝트는 건드리지 않습니다.
        var retained = blackBulletColumnReference.HasValue ? new List<Vector3>(blackBulletBodyPoints) : null;
        var targetOffset = blackBulletColumnReference.HasValue ? blackBulletColumnReference.Value.target - blackBulletTargetAnchor - Vector3.up * attackFocusHeight : Vector3.zero;
        void Add(Bounds bounds, bool atTarget = false)
        {
            for (int i = 0; i < 8; i++)
            {
                var point = JcBlackBulletCameraFraming.Corner(bounds, i);
                points.Add(point); retained?.Add(point + (atTarget ? targetOffset : Vector3.zero));
            }
        }
        if (blackBulletHasTargetBody) { var bounds = blackBulletTargetBody; bounds.Expand(2 * Mathf.Max(0, settings.targetMotionMargin)); Add(bounds, true); }
        var hit = blackBulletTargetAnchor + Vector3.up * attackFocusHeight;
        // JC | 2026-11-16 | 보스 전용 최대 반경 강제를 제거하고 일반 전투의 스킬별 효과 여유를 사용합니다.
        float impactRadius = settings.ImpactRadius(blackBulletCloseUpFamily);
        Add(new Bounds(hit, Vector3.one * (2 * Mathf.Max(0,impactRadius))), true);
        var socket = attackFocusActor ? attackFocusActor.GetComponent<UnitVisualProfile>() : null;
        var muzzle = socket && socket.AttackEffectSocket ? socket.AttackEffectSocket.position : blackBulletActorAnchor + Vector3.up * attackFocusHeight;
        // 직선 투사체 구간은 볼록이므로 두 끝의 여유 영역을 포함하면 중간 경로도 포함됩니다.
        float diameter = 2 * Mathf.Max(0, settings.projectileRadius);
        Add(new Bounds(muzzle, Vector3.one * diameter)); Add(new Bounds(hit, Vector3.one * diameter), true);
        var popup = FindSceneComponent<BattleVisualDirector>();
        if (popup && attackFocusTarget)
            blackBulletHasPopupBounds = popup.TryGetDamageCameraEnvelope(attackFocusTarget.transform, settings.damageDigits, settings.targetMotionMargin, blackBulletBasisRotation, out blackBulletPopupBounds);
        if (blackBulletHasPopupBounds)
        {
            for (int i = 0; i < 8; i++) points.Add(JcBlackBulletCameraFraming.Corner(blackBulletPopupBounds, i));
            if (retained != null && popup.TryGetDamageCameraEnvelope(attackFocusTarget.transform, settings.damageDigits,
                settings.targetMotionMargin, blackBulletColumnReference.Value.rotation, out var retainedPopup))
                for (int i = 0; i < 8; i++) retained.Add(JcBlackBulletCameraFraming.Corner(retainedPopup, i) + targetOffset);
        }
        if (retained != null)
        {
            var endpoint = blackBulletColumnReference.Value; endpoint.points = retained.ToArray(); blackBulletColumnReference = endpoint;
        }
        blackBulletCloseUpPoints = points.ToArray();
    }

    // JC | 2026-11-10 | 적 자체 우측열0/좌측열2를 같은 행에서 찾고, 우측열의 기존 카메라 계산 입력을 고정합니다.
    private void CaptureBlackBulletColumnReference()
    {
        blackBulletColumnReference = null;
        if (blackBulletColumnBlend <= 0 || !attackFocusTarget || !attackFocusTarget.OccupiedCell) return;
        ASB.Work.BattleGrid.GridCell right = null, left = null;
        int row = attackFocusTarget.OccupiedCell.Coords.x;
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var cell in root.GetComponentsInChildren<ASB.Work.BattleGrid.GridCell>(true))
                if (cell.Coords.x == row)
                {
                    if (cell.Coords.y == 0) right = cell;
                    if (cell.Coords.y == 2) left = cell;
                }
        // JC | 2026-11-10 | 열이 정의되지 않은 임시 프리뷰에서는 끝점 보간을 적용하지 않습니다. 실제 전투는 양 끝 셀을 갖습니다.
        if (!right || !left) { blackBulletColumnBlend = 0; return; }
        var targetOffset = blackBulletTargetAnchor - attackFocusTarget.OccupiedCell.transform.position;
        var endpoint = new JcBlackBulletCameraFraming.ColumnReference {
            target = right.transform.position + targetOffset + Vector3.up * attackFocusHeight,
            oppositeTarget = left.transform.position + targetOffset + Vector3.up * attackFocusHeight
        };
        var corner = blackBulletCalculationPosition + Vector3.down * Mathf.Max(0, attackZoomDrop);
        var approach = Vector3.Lerp(blackBulletActorAnchor, endpoint.target - Vector3.up * attackFocusHeight, rangedTargetFocusWeight) - corner;
        approach.y = 0;
        endpoint.position = corner + approach.normalized * Mathf.Min(attackZoomApproach, approach.magnitude * .5f);
        ResolvePairFraming(blackBulletActorAnchor, endpoint.target - Vector3.up * attackFocusHeight,
            endpoint.position, previewCamera.aspect, out endpoint.rotation, out endpoint.fov);
        blackBulletColumnReference = endpoint;
    }

    private void ReportBlackBulletConstraint()
    {
        if (blackBulletCameraControls) blackBulletCameraControls.SetPhase("표시 영역과 구도 제한 충돌 · FOV/예약 영역 확인");
        if (blackBulletConstraintWarning) return;
        blackBulletConstraintWarning = true;
        Debug.LogWarning("[JC 블랙불릿 카메라] 설정된 이동/FOV 범위에서 표시 영역을 담을 수 없습니다. 마지막 카메라 자세를 유지합니다.", this);
    }

    private void UpdateBlackBulletResidual()
    {
        if (!blackBulletResidual || !previewCamera || attackZoomActive || Time.timeScale <= 0) return;
        blackBulletLivePoints.Clear();
        DamagePopupEffect.AppendActiveCameraPoints(blackBulletPopupOwner, blackBulletLivePoints);
        if (blackBulletLivePoints.Count > 0)
        {
            ApplyCameraPose(previewCamera.transform.position, previewCamera.transform.rotation, blackBulletResidualFov);
            return;
        }
        float residualFov=Mathf.Lerp(previewCamera.fieldOfView, blackBulletResidualFov, 1 - Mathf.Exp(-15 * Time.deltaTime));
        WriteCameraPose(previewCamera,previewCamera.transform.position,previewCamera.transform.rotation,residualFov);
        if (Mathf.Abs(previewCamera.fieldOfView - blackBulletResidualFov) < .01f) ReleaseBlackBulletResidual();
    }

    private void ReleaseBlackBulletResidual()
    {
        if (blackBulletResidual && previewCamera)
        {
            WriteCameraPose(previewCamera,previewCamera.transform.position,previewCamera.transform.rotation,blackBulletResidualFov);
        }
        blackBulletResidual = false;
    }

    // JC | 2026-11-10 | 첫 yield 이전에 목적지를 확정하고 현재 전투 카메라에서 직접 전환합니다. 별도 준비 시계는 없습니다.
    private void BeginBlackBulletCloseUp(int family, Vector3 basisPosition, Quaternion basisRotation, float basisFov)
    {
        blackBulletBasisPosition = basisPosition;
        blackBulletBasisRotation = basisRotation;
        blackBulletReferenceFov = basisFov;
        blackBulletCloseUpPosition = previewCamera.transform.position;
        blackBulletCloseUpRotation = previewCamera.transform.rotation;
        blackBulletCloseUpFov = previewCamera.fieldOfView;
        blackBulletCloseUpFamily = family;
        if (blackBulletCanon == JcBlackBulletCameraCanon.Adaptive) CaptureBlackBulletColumnReference();
        else blackBulletColumnReference = null;
        var captureWatch = System.Diagnostics.Stopwatch.StartNew();
        blackBulletBodyPoints = CaptureBlackBulletCloseUpPoints(attackFocusActor);
        BlackBulletCaptureMilliseconds = captureWatch.Elapsed.TotalMilliseconds;
        blackBulletProtection = true;
        blackBulletReturning = blackBulletConstraintWarning = false;
        blackBulletSettingsHash = 0;
        blackBulletFrameResolved = false;
        RefreshBlackBulletProtection();
        blackBulletCloseUpActive = blackBulletCloseUpMoving = true;
        blackBulletCloseUpAttackRunning = false;
        followAttackFocus = false;
        blackBulletTransitionElapsed = 0;
        blackBulletClockBattle = FindSceneComponent<BattleManager>();
        blackBulletTimeline = null;
        blackBulletTimelineActive = false;
        blackBulletTimelineHighWater = 0;
        blackBulletClockFrame = Time.frameCount;
        // JC | 2026-11-10 | 메뉴 옆 고정 픽셀 목표를 제거합니다. UI 회피는 기존 화면 예약 영역 제약으로 유지합니다.
        ResolveBlackBulletCloseUpFrame();
        if (!blackBulletConstraintWarning) blackBulletCameraControls.SetPhase("정본 " + (int)blackBulletCanon + " · 공격 앵글 전환 · " + family);
        // JC | 2026-11-10 | 전환 시간 0은 첫 프레임에서 즉시 도착하며 양수이면 현재 자세에서 연속적으로 출발합니다.
        if (blackBulletCameraControls.Draft.transitionSeconds <= 0) TickBlackBulletCloseUp(0);
    }

    private void EndBlackBulletCloseUpAttack()
    {
        blackBulletCloseUpAttackRunning = false;
    }

    // JC | 2026-11-11 | 기존 ASB 재생기의 시작/종료 알림입니다. 두 정본의 현재 공격만 관찰하며 Director를 제어하지 않습니다.
    public void BeginSkillCameraTimeline(BattleCharactor actor, UnityEngine.Playables.PlayableDirector director, double startTime)
    {
        skillRearBlur?.BeginTimeline(actor, director, startTime);
        if (actor != attackFocusActor || !blackBulletCloseUpAttackRunning ||
            !blackBulletCloseUpActive) return;
        blackBulletTimeline = director;
        blackBulletTimelineActive = true;
        blackBulletTimelineHighWater = startTime;
        blackBulletClockFrame = -1;
    }

    public void EndSkillCameraTimeline(BattleCharactor actor, UnityEngine.Playables.PlayableDirector director)
    {
        skillRearBlur?.EndTimeline(actor, director);
        // JC | 2026-11-17 | Stop의 시간 초기화가 크래시 보정을 재진입시키지 않게 관찰을 끝내고 실제 귀환 위치로 감쇠합니다.
        if (bossJusticeReady && actor == attackFocusActor && director == bossJusticeTimeline)
        {
            bossJusticeReturning = true;
            bossJusticeTimeline = null;
        }
        if (!blackBulletTimelineActive || actor != attackFocusActor || director != blackBulletTimeline) return;
        // Stop()이 재생 시간을 초기화하기 전에 마지막 증가량을 소비합니다. 종료 알림에서 새 확대를 시작하지는 않습니다.
        float delta = ReadBlackBulletCloseUpDelta();
        if (Time.timeScale > 0f) AdvanceBlackBulletCloseUp(delta);
        blackBulletTimelineActive = false;
        blackBulletTimeline = null;
    }

    private float BlackBulletBattleSpeed => blackBulletClockBattle ? Mathf.Max(0f, blackBulletClockBattle.CurrentBattleSpeed) : 0f;
    private float BlackBulletBattleDelta => Mathf.Max(0f, Time.deltaTime) * BlackBulletBattleSpeed;

    private float ReadBlackBulletCloseUpDelta()
    {
        // JC | 2026-11-10 | Timeline 종료와 LateUpdate가 같은 프레임에 호출되어 시간을 이중 적립하지 않게 합니다.
        if (blackBulletClockFrame == Time.frameCount) return 0;
        blackBulletClockFrame = Time.frameCount;
        return SampleBlackBulletCloseUpDelta(Time.deltaTime, BlackBulletBattleSpeed, Time.timeScale > 0f);
    }

    private float SampleBlackBulletCloseUpDelta(float scaledDelta, float battleSpeed, bool running)
    {
        if (blackBulletTimelineActive)
        {
            double time = blackBulletTimeline ? blackBulletTimeline.time : blackBulletTimelineHighWater;
            float delta = (float)System.Math.Max(0d, time - blackBulletTimelineHighWater);
            // JC | 2026-11-10 | Hold의 시간 되감기/재평가 구간을 다시 세지 않습니다. 새 구간은 시작 알림에서 기준점을 재설정합니다.
            blackBulletTimelineHighWater = System.Math.Max(blackBulletTimelineHighWater, time);
            return running && battleSpeed > 0f ? delta : 0f;
        }
        // 대사·Timeline 전후의 이동/대기는 전투 시계를 사용합니다. 공격 안의 Animator 타격 정지도 함께 반영합니다.
        float hitStop = blackBulletCloseUpAttackRunning && attackFocusActor && attackFocusActor.Anim != null
            ? Mathf.Max(0f, attackFocusActor.Anim.HitStopSpeedMultiplier) : 1f;
        return running ? Mathf.Max(0f, scaledDelta) * Mathf.Max(0f, battleSpeed) * hitStop : 0f;
    }

    private void TickBlackBulletCloseUp(float delta) => AdvanceBlackBulletCloseUp(delta);

    private void AdvanceBlackBulletCloseUp(float delta)
    {
        // JC | 2026-11-10 | 진행 시간을 인자로 받아 플레이 없이 순서/경계도 검증합니다. 실제 호출은 기존 LateUpdate 한 곳입니다.
        if (!CanContinueBlackBulletCloseUp())
        {
            blackBulletCloseUpActive = blackBulletCloseUpMoving = false;
            return;
        }
        // JC | 2026-11-10 | 스킬 발동부터 전환 시간만 적립합니다. 준비 동작/대기 또는 공격 시작을 기다리는 조건을 두지 않습니다.
        if (blackBulletCloseUpMoving) blackBulletTransitionElapsed += Mathf.Max(0, delta);
        if (!blackBulletCloseUpActive) return;
        float duration = Mathf.Max(0, blackBulletCameraControls.Draft.transitionSeconds);
        float t = !blackBulletCloseUpMoving || duration <= 0 ? 1 : Mathf.Clamp01(blackBulletTransitionElapsed / duration);
        ApplyBlackBulletCloseUp(t * t * (3f - 2f * t));
        blackBulletCloseUpMoving = t < 1;
        if (!blackBulletCloseUpMoving && !blackBulletConstraintWarning) blackBulletCameraControls.SetPhase("정본 유지 · 기존 공격/복귀 진행");
    }

    private bool CanContinueBlackBulletCloseUp() => attackZoomActive && previewCamera && attackFocusActor &&
        !attackFocusActor.IsDead && blackBulletCameraControls && blackBulletCameraControls.isActiveAndEnabled &&
        blackBulletCameraControls.Draft;

    private void ApplyBlackBulletCloseUp(float blend)
    {
        // JC | 2026-11-10 | 배치별 목적지로 보간하되 출발 자세를 고정해 편집 시 이동/FOV 누적을 막습니다.
        if (!ResolveBlackBulletCloseUpFrame()) return;
        ApplyCameraPose(Vector3.Lerp(blackBulletCloseUpPosition, blackBulletFramePosition, blend), Quaternion.Slerp(blackBulletCloseUpRotation, blackBulletFrameRotation, blend),
            // JC | 2026-11-10 | 회전도 변화하므로 매 프레임 ApplyCameraPose에서 실제 경로의 가시 제약을 다시 확인합니다.
            Mathf.Atan(Mathf.Lerp(Mathf.Tan(blackBulletCloseUpFov * Mathf.Deg2Rad * .5f), Mathf.Tan(blackBulletFrameFov * Mathf.Deg2Rad * .5f), blend)) * 2 * Mathf.Rad2Deg,
            // JC | 2026-11-10 | 위치·회전과 같은 진행률로 예약 여백을 확보합니다. 복귀 등 기존 호출은 기본값 1로 종전 보호를 유지합니다.
            blend);
    }

    // JC | 2026-11-11 | 원근감 Inspector의 명시적인 편집만 현재 진행률에서 재적용합니다. 일시정지 시계/Timeline/전환 진행량은 바꾸지 않습니다.
    public void RefreshBlackBulletCameraPreview()
    {
        if (!blackBulletCloseUpActive || !CanContinueBlackBulletCloseUp()) return;
        blackBulletFrameResolved = false;
        float duration = Mathf.Max(0, blackBulletCameraControls.Draft.transitionSeconds);
        float t = !blackBulletCloseUpMoving || duration <= 0 ? 1 : Mathf.Clamp01(blackBulletTransitionElapsed / duration);
        ApplyBlackBulletCloseUp(t * t * (3f - 2f * t));
    }

    // JC | 2026-11-10 | 실제 카메라 쓰기와 목적지 계산을 분리하여 스킬 시작 시 선계산 및 프로필 실시간 갱신을 함께 지원합니다.
    private bool ResolveBlackBulletCloseUpFrame()
    {
        var projection = new Vector4(previewCamera.pixelWidth, previewCamera.pixelHeight, previewCamera.aspect, previewCamera.nearClipPlane);
        RefreshBlackBulletProtection();
        float padding = blackBulletCameraControls.Draft.edgeMarginPixels;
        if (!blackBulletFrameResolved || blackBulletFrameProjection != projection || blackBulletFramePadding != padding)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var settings = blackBulletCameraControls.Draft;
            var inverse = blackBulletWorldFromReference.inverse;
            var inverseRotation = Quaternion.Inverse(blackBulletReferenceRotation);
            Vector3[] ToReference(Vector3[] points)
            {
                var result = new Vector3[points.Length];
                for (int i = 0; i < points.Length; i++) result[i] = inverse.MultiplyPoint3x4(points[i]);
                return result;
            }
            bool resolved;
            if (blackBulletCanon == JcBlackBulletCameraCanon.Authored)
            {
                var source = blackBulletCameraControls.AuthoredDraft;
                var slot = source ? source.FindShot(blackBulletCloseUpFamily, blackBulletTargetSlot) : null;
                resolved = slot != null;
                if (resolved)
                {
                    slot.Resolve(out var p, out var q, out blackBulletFrameFov);
                    // 정본1 촬영값은 그대로 두고 은행 반 칸 이동을 제거한 후 현재 진형 원점으로 옮깁니다.
                    p += settings.referencePlayerOrigin - source.referencePlayerOrigin;
                    blackBulletFramePosition = blackBulletWorldFromReference.MultiplyPoint3x4(p);
                    blackBulletFrameRotation = blackBulletReferenceRotation * q;
                    float required = JcBlackBulletCameraFraming.RequiredFov(blackBulletCloseUpPoints, blackBulletFramePosition,
                        blackBulletFrameRotation, previewCamera.aspect, new Vector2(previewCamera.pixelWidth, previewCamera.pixelHeight),
                        settings, out resolved, previewCamera.nearClipPlane);
                    blackBulletFrameFov = Mathf.Max(blackBulletFrameFov, required);
                }
            }
            else
            {
                var endpoint = blackBulletColumnReference;
                if (endpoint.HasValue)
                {
                    var e = endpoint.Value; e.target = inverse.MultiplyPoint3x4(e.target); e.oppositeTarget = inverse.MultiplyPoint3x4(e.oppositeTarget);
                    e.position = inverse.MultiplyPoint3x4(e.position); e.rotation = inverseRotation * e.rotation; e.points = ToReference(e.points); endpoint = e;
                }
                resolved = JcBlackBulletCameraFraming.TryResolve(inverse.MultiplyPoint3x4(blackBulletActorAnchor + Vector3.up * attackFocusHeight),
                    inverse.MultiplyPoint3x4(blackBulletTargetAnchor + Vector3.up * attackFocusHeight), ToReference(blackBulletCloseUpPoints),
                    inverse.MultiplyPoint3x4(blackBulletBasisPosition), inverseRotation * blackBulletBasisRotation, blackBulletReferenceFov, previewCamera.aspect,
                    previewCamera.nearClipPlane, new Vector2(previewCamera.pixelWidth, previewCamera.pixelHeight), settings,
                    out var position, out var rotation, out blackBulletFrameFov,
                    inverse.MultiplyPoint3x4(new Vector3(blackBulletActorAnchor.x, blackBulletGroundHeight, blackBulletActorAnchor.z)).y,
                    // JC | 2026-11-17 | 보스 구도 기준은 시전자 열과 곱한 기존 가중치가 아닌 실제 적의 좌/중/우입니다.
                    // 공통 연출기의 기존 호출 한 곳에서 전달하며 은행·야외는 basisPitchOffset이 없어 적용되지 않습니다.
                    blackBulletColumnBlend, endpoint, blackBulletBasisPitchOffset,
                    attackFocusTarget && attackFocusTarget.OccupiedCell ? Mathf.Clamp01(attackFocusTarget.OccupiedCell.Coords.y * .5f) : 1f);
                blackBulletFramePosition = blackBulletWorldFromReference.MultiplyPoint3x4(position);
                blackBulletFrameRotation = blackBulletReferenceRotation * rotation;
            }
            BlackBulletSolveMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (!resolved) { ReportBlackBulletConstraint(); return false; }
            blackBulletFrameProjection = projection; blackBulletFramePadding = padding; blackBulletFrameResolved = true;
        }
        return true;
    }

    private System.Collections.IEnumerator ShowSkillSpeech(BattleCharactor actor, SkillData skill, bool counter)
    {
        var speech = FindSceneComponent<BattleSkillSpeechBubble>();
        if (speech != null && speech.gameObject.scene == gameObject.scene && speech.isActiveAndEnabled)
            yield return speech.ShowBeforeCast(actor, skill, counter);
    }

    private System.Collections.IEnumerator AnimateAttackZoom(Vector3 position, Vector3 corner, Quaternion rotation,
        float fov, float orthoSize, float duration, bool focusActor, bool useBattleTime = false)
    {
        Vector3 startPosition = previewCamera.transform.position;
        Quaternion startRotation = previewCamera.transform.rotation;
        float startFov = previewCamera.fieldOfView;
        float startSize = previewCamera.orthographicSize;
        float elapsed = 0f;
        while (elapsed < duration && previewCamera != null)
        {
            // JC | 2026-11-10 | 블랙불릿 추가 확대의 복귀도 같은 전투 시간으로 처리합니다. 기존 곡선과 나머지 캐릭터의 경로는 보존합니다.
            elapsed += useBattleTime ? BlackBulletBattleDelta : Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - (1f - t) * (1f - t);
            Vector3 cameraPosition = (1f - t) * (1f - t) * startPosition +
                2f * (1f - t) * t * corner + t * t * position;
            // JC | 2026-11-06 | 이동 경로에서도 같은 계산을 사용하여 도착 직후 구도가 튀지 않게 합니다.
            if (focusActor && UseProjectedPairFraming)
                ResolveProjectedPair(cameraPosition, out rotation, out fov);
            else if (focusActor && attackFocusActor != null)
                rotation = Quaternion.LookRotation(AttackFocusPosition - cameraPosition);
            ApplyCameraPose(cameraPosition, Quaternion.Slerp(startRotation, rotation, t), Mathf.Lerp(startFov, fov, t));
            WriteCameraPose(previewCamera,previewCamera.transform.position,previewCamera.transform.rotation,previewCamera.fieldOfView,Mathf.Lerp(startSize,orthoSize,t));
            yield return null;
        }
        if (previewCamera != null)
        {
            // JC | 2026-11-06 | 즉시 이동(진입 시간 0)도 같은 최종 구도로 수렴합니다.
            if (focusActor && UseProjectedPairFraming)
                ResolveProjectedPair(position, out rotation, out fov);
            else if (focusActor && attackFocusActor != null)
                rotation = Quaternion.LookRotation(AttackFocusPosition - position);
            ApplyCameraPose(position, rotation, fov);
            WriteCameraPose(previewCamera,previewCamera.transform.position,previewCamera.transform.rotation,previewCamera.fieldOfView,orthoSize);
        }
    }

    private void RestoreAttackCamera()
    {
        skillRearBlur?.Cancel();
        // JC | 2026-11-16 | 정상 종료뿐 아니라 비활성화·취소에도 보스 추적 기준을 즉시 해제합니다.
        ResetBossFollow();
        if (!attackZoomActive) return;
        // JC | 2026-11-16 | 모든 전장의 정상 종료/취소가 같은 복구 경로를 사용합니다.
        // JC | 2026-11-10 | 복귀 후 숫자의 표시 수명만 FOV 보호에 남깁니다. 원래 카메라 위치·회전과 턴 종료 시각은 그대로 복구합니다.
        blackBulletLivePoints.Clear();
        if (blackBulletProtection) DamagePopupEffect.AppendActiveCameraPoints(blackBulletPopupOwner, blackBulletLivePoints);
        blackBulletResidual = blackBulletLivePoints.Count > 0;
        blackBulletProtection = false;
        blackBulletResidualFov = attackCameraFov;
        // JC | 2026-11-16 | 정상 종료·중단 모두 스킬 시작 때의 기본 구도로 복귀합니다. 무대 경계 보정 없이 저장 프로필을 보존합니다.
        if (previewCamera != null)
        {
            ApplyCameraPose(attackCameraPosition, attackCameraRotation, attackCameraFov);
            WriteCameraPose(previewCamera,previewCamera.transform.position,previewCamera.transform.rotation,previewCamera.fieldOfView,attackCameraOrthoSize);
        }
        attackZoomActive = false;
        // JC | 2026-11-10 | 정상 복귀·비활성화·중단 시 추가 확대와 임시 범위도 기존 finally 정리에 포함합니다.
        blackBulletCloseUpActive = blackBulletCloseUpMoving = false;
        blackBulletCloseUpAttackRunning = false;
        blackBulletTransitionElapsed = 0;
        blackBulletCloseUpFamily = 0;
        // JC | 2026-11-10 | 다음 스킬/코어 연출에 이전 셀의 보간 기준이 남지 않도록 해제합니다.
        blackBulletColumnReference = null;
        blackBulletColumnBlend = 0;
        blackBulletTimeline = null;
        blackBulletTimelineActive = false;
        blackBulletTimelineHighWater = 0;
        blackBulletClockFrame = -1;
        blackBulletClockBattle = null;
        blackBulletCloseUpPoints = null; blackBulletBodyPoints = null; blackBulletHasPopupBounds = false;
        blackBulletReturning = false; blackBulletReferenceFov = 0; blackBulletHasTargetBody = false;
        blackBulletFrameResolved = false;
        if (blackBulletCameraControls) blackBulletCameraControls.SetPhase("대기 · 블랙불릿 공통 카메라");
        // JC | 2026-11-06 | 정상 복귀·중단 모두 같은 정리 경로에서 블랙불릿 구도를 해제합니다.
        blackBulletPairActive = false;
        blackBulletTargetSlot = -1;
        blackBulletActorAnchor = default;
        blackBulletTargetAnchor = default;
        if (BossCamera) BossCamera.EndAttack();
        followAttackFocus = false;
        focusShieldGuard = false;
        shieldGuardEffects.Clear();
        attackFocusActor = null;
        attackFocusTarget = null;
        rifleBarrageUnits.Clear();
        focusDebuffTarget = false;
        focusNekoHeal = false;
        nekoHealProjectiles.Clear();
        nekoHealTargets.Clear();
        frameRangedPair = false;
        luminaProjectile = null;
        luminaLightning = null;
        luminaSolarPrism = null;
        luminaPrismExplosion = null;
        nekoLightTarget = null;
        focusNekoBarrage = false;
        nekoBarrageEffect = null;
        nekoBarrageBounds = default;
        nekoFallingPaw = null;
        ClearNekoPawSequence();
        // JC | 2026-11-16 | 정상 복귀·중단 후 다음 캐릭터의 구도에 보스 낙하 보정이 남지 않게 정리합니다.
        bossNekoPawFrame = bossNekoPawDescending = false;
        bossNekoPawBlend = 0f;
        bossNekoPawProtection.Clear();
        bossNekoPawLivePoints.Clear();
        projectileFocusLatched = false;
    }

    // 레이아웃은 표시 기준 위치만 전달합니다. 실제 위치는 현재 연출 상태가 소유합니다.
    public void ApplySkillPanelLayoutPosition(Vector2 home)
    {
        if (skillPanel == null) return;
        // JC | 2026-11-12 | 레이아웃 재적용은 숨김 기준 위치만 갱신하며 종료 상태를 해제하지 않습니다.
        if (SkillPanelBattleEnded) { panelHome = home; HideSkillPanelOnBattleEnd(); return; }
        if (!panelPositionInitialized)
        {
            skillPanel.anchoredPosition = home;
            return;
        }
        panelHome = home;
        if (panelMotion != null) StopCoroutine(panelMotion);
        panelMotion = null;
        panelDestination = panelRequestedVisible ? panelHome : HiddenPanelPosition;
        skillPanel.anchoredPosition = panelDestination;
    }

    // JC | 2026-11-12 | 내용 초기화보다 늦은 결과창까지 기다리지 않고 즉시 숨깁니다.
    // 진입 게이트가 위치/CanvasGroup을 복구해도 보이지 않도록 좌측 패널만 비활성화합니다.
    private void HandleBattleEnded(BattleResult result) => HideSkillPanelOnBattleEnd();

    private void HideSkillPanelOnBattleEnd()
    {
        skillPanelBattleEnded = true;
        CancelTurnPresentation();
        if (panelMotion != null) StopCoroutine(panelMotion);
        panelMotion = null;
        panelRequestedVisible = false;
        entryHudPresented = false;
        if (skillPanel == null) return;
        panelDestination = HiddenPanelPosition;
        skillPanel.anchoredPosition = panelDestination;
        skillPanel.gameObject.SetActive(false);
    }

    private void HandleAutoBattleChanged(bool automatic)
    {
        SetSkillPanelVisible(!automatic && flow != null && flow.CurrentUnit != null &&
            flow.CurrentUnit.IsPlayer && !flow.IsActionInProgress &&
            turnPresentation == null && !attackZoomActive);
    }

    private void SetSkillPanelVisible(bool visible)
    {
        visible &= !BattleRuntimeSettings.IsAutoBattle;
        // JC | 2026-11-12 | 실패 행동 복구·턴 연출의 늦은 표시 요청에도 종료 상태가 우선합니다.
        if (SkillPanelBattleEnded) { HideSkillPanelOnBattleEnd(); return; }
        panelRequestedVisible = visible;
        if (skillPanel == null) return;
        Vector2 destination = visible ? panelHome : HiddenPanelPosition;
        if (panelMotion != null && panelDestination == destination) return;
        if (panelMotion != null) StopCoroutine(panelMotion);
        panelMotion = null;
        if (Vector2.Distance(skillPanel.anchoredPosition, destination) < 0.01f) return;
        panelDestination = destination;
        panelMotion = StartCoroutine(SlideSkillPanel(destination, visible));
    }

    public System.Collections.IEnumerator HideSkillPanelBeforeCast()
    {
        SetSkillPanelVisible(false);
        if (panelMotion != null) yield return new WaitWhile(() => panelMotion != null);
        if (skillCastDelay > 0f) yield return new WaitForSecondsRealtime(skillCastDelay);
    }

    private System.Collections.IEnumerator SlideSkillPanel(Vector2 destination, bool visible)
    {
        if (skillPanel != null)
        {
            Vector2 start = skillPanel.anchoredPosition;
            float elapsed = 0f;
            float fullDistance = Mathf.Max(1f, Vector2.Distance(panelHome, HiddenPanelPosition));
            float remainingFraction = Vector2.Distance(start, destination) / fullDistance;
            float duration = Mathf.Max(0.01f, skillPanelEntranceSeconds * remainingFraction);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 진입은 감속, 퇴장은 같은 곡선을 역방향으로 재생해 화면 안의 이동 속도를 맞춥니다.
                t = visible ? 1f - (1f - t) * (1f - t) : t * t;
                skillPanel.anchoredPosition = Vector2.LerpUnclamped(start, destination, t);
                yield return null;
            }
            skillPanel.anchoredPosition = destination;
        }
        panelMotion = null;
    }

    private void OnDestroy()
    {
        BattleRuntimeSettings.AutoBattleChanged -= HandleAutoBattleChanged;
        UnitEffectPresenter.EffectSpawned -= HandleSkillEffectSpawned;
        BattleVisualDirector.HealPresented -= HandleHealPresented;
        // JC | 2026-11-16 | 재활성화 시 목적지 이벤트가 중복되지 않도록 대칭 해제합니다.
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightDestinationResolved -= HandleBossProjectileDestination;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightStarted -= HandleProjectileStarted;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightFinished -= HandleProjectileFinished;
        RestoreAttackCamera();
        ReleaseBlackBulletResidual();
        CancelTurnPresentation();
        // JC | 2026-11-12 | 씬 재시작 때 이전 종료 수신자가 남지 않도록 함께 해제합니다.
        if (flow != null)
        {
            flow.OnTurnStarted -= HandleTurnStarted;
            flow.OnBattleEnded -= HandleBattleEnded;
        }
        if (turnBanner != null) turnBanner.TurnBannerShown -= HandleTurnBannerShown;
        // JC | 2026-11-16 | 종료 경로의 복구가 끝난 뒤 카메라 담당과 렌더 구독을 해제합니다.
        OnDisable();
    }
}
