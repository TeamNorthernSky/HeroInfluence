using System;
using System.Collections.Generic;
using UnityEngine;
using JC.BattleTesting;

/// <summary>연출용 복사 씬에서만 사용하는 모의 전투 진입점. 저장 데이터와 공용 UI 배치를 수정하지 않습니다.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class BattlePresentationPreview : MonoBehaviour
{
    [Tooltip("연출용 독립 전투 설정입니다. Play 전에 아군, 레벨, 스킬, 적 그룹을 지정하세요.")]
    public SimulationBattleConfig config;
    public BattleSceneManager battle;
    public Camera previewCamera;
    public Transform playerPlace;
    public Transform enemyPlace;
    [Tooltip("연출용 복사 카메라 프로필. F6/F7로 구도를 전환하고 F8로 씬에 저장한 구도로 돌아갑니다.")]
    public JcBattleCameraProfile cameraProfile;
    public BattleResultPanel resultPanel;
    [Header("연출 반복 테스트")]
    [InspectorName("적 사망 방지"), Tooltip("연출용 씬에서 적 HP가 1 아래로 내려가지 않도록 합니다.")]
    public bool enemiesCannotDie = true;
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
    [InspectorName("줌인 시간 (초)"), Min(0f)] public float attackZoomInSeconds = 0.18f;
    [InspectorName("줌 복귀 시간 (초)"), Min(0f)] public float attackZoomOutSeconds = 0.25f;
    [InspectorName("줌 하강 거리"), Min(0f)] public float attackZoomDrop = 0.6f;
    [InspectorName("캐릭터 쪽 접근 거리"), Min(0f)] public float attackZoomApproach = 0.6f;
    [InspectorName("캐릭터 초점 높이"), Min(0f), Tooltip("캐릭터 발 위치에서 초점을 올릴 높이입니다.")]
    public float attackFocusHeight = 1.1f;
    [InspectorName("방패병 복귀 후 카메라 대기 (초)"), Min(0f)]
    public float shieldCameraReturnDelay = 0.35f;
    [InspectorName("원거리 적 공격 시 대상도 함께 표시")]
    public bool frameEnemyRangedTarget = true;
    [InspectorName("블랙불릿 공격 시 대상도 함께 표시"), Tooltip("블랙불릿과 피격 대상을 함께 담고, 후열 대상이 잘리지 않도록 화각을 조절합니다.")]
    public bool frameBlackBulletTarget = true;
    [InspectorName("원거리 공격 초점의 피격자 비중"), Range(.5f, .8f)]
    public float rangedTargetFocusWeight = 0.65f;
    [Header("루미나 투사체 연출")]
    public bool followLuminaProjectile = true;
    [Tooltip("루미나 투사체를 향해 일정하게 회전하는 속도입니다. 낮출수록 천천히 전환합니다.")]
    [InspectorName("투사체 초점 회전 속도 (도/초)"), Min(1f)] public float projectileFocusSpeed = 24f;
    [InspectorName("플레어 봄 준비 시간 (초)"), Min(0f)] public float flareBombCastDelay = 0.35f;
    [InspectorName("체인 라이트닝 준비 시간 (초)"), Min(0f)] public float chainLightningCastDelay = 0.35f;
    [InspectorName("체인 라이트닝 종료 후 대기 (초)"), Min(0f)] public float chainLightningEndDelay = 0.4f;
    [InspectorName("네코밍 빛 이펙트 시 적에게 초점 이동")]
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
    private bool started;
    private BattleFlowManager flow;
    private BattleTurnBanner turnBanner;
    private Vector2 panelHome;
    private Coroutine panelMotion;
    private Vector2 panelDestination;
    private Coroutine cameraMotion;
    private Coroutine turnPresentation;
    private IDisposable turnFlowLock;
    private int turnCameraView = -1;
    private bool attackZoomActive;
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
    private bool focusNekoHeal;
    private readonly List<Transform> nekoHealProjectiles = new List<Transform>();
    private readonly List<BattleCharactor> nekoHealTargets = new List<BattleCharactor>();
    private JC.VFX.JcVfxPartSequence nekoPawSequence;
    private Coroutine nekoPawFocusRoutine;
    private readonly Dictionary<BattleCharactor, IDisposable> enemyImmortality = new Dictionary<BattleCharactor, IDisposable>();
    private bool projectileFocusLatched;
    private Vector3 projectileFocusPoint;
    private bool followAttackFocus;

    private void Awake()
    {
        // 데이터 검증 실패 시 일반 게임 저장소로 폴백하지 않도록 시작을 보류합니다.
        if (battle != null) battle.enabled = false;
        if (previewCamera != null)
        {
            savedPosition = previewCamera.transform.position;
            savedRotation = previewCamera.transform.rotation;
            savedFov = previewCamera.fieldOfView;
        }
        if (resultPanel != null) resultPanel.OnAccepted += FinishPreview;
        if (skillPanel != null)
        {
            panelHome = skillPanel.anchoredPosition;
            skillPanel.anchoredPosition = HiddenPanelPosition;
        }
        flow = battle != null ? battle.GetComponent<BattleFlowManager>() : null;
        if (flow != null) flow.OnTurnStarted += HandleTurnStarted;
        turnBanner = FindFirstObjectByType<BattleTurnBanner>(FindObjectsInactive.Include);
        if (turnBanner != null) turnBanner.TurnBannerShown += HandleTurnBannerShown;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightStarted += HandleProjectileStarted;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightFinished += HandleProjectileFinished;
        UnitEffectPresenter.EffectSpawned += HandleSkillEffectSpawned;
        BattleVisualDirector.HealPresented += HandleHealPresented;
        if (flow != null) flow.OnParticipantRegistered += ProtectEnemy;
    }

    private void Start()
    {
        try
        {
            if (config == null || battle == null || DHCsvTemplateCatalog.Instance == null)
                throw new InvalidOperationException("전투 설정, BattleSceneManager 또는 데이터 카탈로그가 없습니다.");
            if (Array.Exists(FindObjectsOfType<YT.Preview.YtSkillPreviewController>(), preview => preview.gameObject.scene == gameObject.scene))
            {
#if UNITY_EDITOR
                // Load the saved configuration: play-mode scene reload can retain the previous transient clone.
                var savedPreviewConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationBattleConfig>("Assets/_ProtoType_Merge/YT/SkillPreviewConfig.asset");
                if (savedPreviewConfig != null) config = savedPreviewConfig;
#endif
                if (YT.Preview.YtSkillPreviewController.OriginalEnemyGroup == null)
                {
                    YT.Preview.YtSkillPreviewController.OriginalEnemyGroup = config.enemyGroupKey;
                    YT.Preview.YtSkillPreviewController.OriginalEnemyZone = config.eventZoneId;
                    YT.Preview.YtSkillPreviewController.OriginalEnemyLevel = config.enemyLevel;
                }
                config = Instantiate(config);
                config.enemyGroupKey = YT.Preview.YtSkillPreviewController.ArnoldEncounter ? "BE161" : YT.Preview.YtSkillPreviewController.OriginalEnemyGroup;
                config.eventZoneId = YT.Preview.YtSkillPreviewController.ArnoldEncounter ? 1 : YT.Preview.YtSkillPreviewController.OriginalEnemyZone;
                config.enemyLevel = YT.Preview.YtSkillPreviewController.ArnoldEncounter ? 2 : YT.Preview.YtSkillPreviewController.OriginalEnemyLevel;
            }
            var allies = new List<SimulationAllyRuntimeData>();
            foreach (var input in config.allies)
            {
                if (input == null || !input.Enabled) continue;
                if (!SimulationUnitFactory.TryCreateRuntimeData(input, allies.Count, out var ally, out var error))
                    throw new InvalidOperationException(error);
                allies.Add(ally);
            }
            if (allies.Count < 1 || allies.Count > 6)
                throw new InvalidOperationException("활성 아군은 1~6명이어야 합니다.");
            int enemyLevel = Mathf.Clamp(config.enemyLevel, 1, SimulationUnitFactory.MaxSimulationLevel);
            int eventZoneId = Mathf.Max(0, config.eventZoneId);
            if (!SimulationUnitFactory.ValidateEnemy(config.enemyGroupKey, eventZoneId, enemyLevel, out var enemyError))
                throw new InvalidOperationException(enemyError);
            var context = CombatContext.EnsureInstance();
            if (!context.BeginSimulation("PRESENTATION_PREVIEW", allies, config.enemyGroupKey,
                    enemyLevel, gameObject.scene.name, eventZoneId))
                throw new InvalidOperationException("모의 전투 초기화에 실패했습니다.");
            started = true;
            battle.enabled = true;
        }
        catch (Exception error)
        {
            Debug.LogError("[BattlePresentationPreview] " + error.Message, this);
        }
    }

    private void Update()
    {
        if (battle != null)
            foreach (var enemy in battle.EnemyBattleCharactors) ProtectEnemy(enemy);
        if (!enemiesCannotDie && enemyImmortality.Count > 0)
        {
            foreach (var handle in enemyImmortality.Values) handle.Dispose();
            enemyImmortality.Clear();
        }
        if (Input.GetKeyDown(KeyCode.F6)) SelectView(0);
        if (Input.GetKeyDown(KeyCode.F7)) SelectView(1);
        if (Input.GetKeyDown(KeyCode.F8)) SelectView(2);
    }

    private void ProtectEnemy(BattleCharactor enemy)
    {
        if (enemiesCannotDie && enemy != null && !enemy.IsPlayer && !enemy.IsDead && enemy.MaxHp > 0f &&
            !enemyImmortality.ContainsKey(enemy))
            enemyImmortality[enemy] = enemy.AddMinimumHpConstraint(1f, this);
    }

    private void LateUpdate()
    {
        if (!attackZoomActive || !followAttackFocus || previewCamera == null || attackFocusActor == null) return;
        nekoHealProjectiles.RemoveAll(projectile => projectile == null);
        Vector3 direction = AttackFocusPosition - previewCamera.transform.position;
        if (direction.sqrMagnitude > .001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            previewCamera.transform.rotation = projectileFocusLatched
                ? Quaternion.RotateTowards(previewCamera.transform.rotation, targetRotation,
                    projectileFocusSpeed * Time.unscaledDeltaTime)
                : Quaternion.Slerp(previewCamera.transform.rotation, targetRotation,
                    1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        }
        if (focusNekoBarrage || (frameRangedPair && attackFocusTarget != null) || (focusNekoHeal && (nekoHealProjectiles.Count > 0 || nekoHealTargets.Count > 0)))
        {
            float neededFov = Mathf.Max(attackZoomBaseFov,
                RequiredPairFov(previewCamera.transform.position, previewCamera.transform.rotation));
            previewCamera.fieldOfView = Mathf.Lerp(previewCamera.fieldOfView, neededFov,
                1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        }
    }

    private Vector3 AttackFocusPosition
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
                if (count > 0) return center / count;
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
                return nekoFallingPaw.FallingFocusPosition;
            if (focusNekoLightTarget && nekoLightTarget != null)
                return nekoLightTarget.transform.position + Vector3.up * attackFocusHeight;
            if (followLuminaProjectile)
            {
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
                    projectileFocusPoint = solarPoint;
                    projectileFocusLatched = true;
                }
                else if (luminaPrismExplosion != null && luminaPrismExplosion.TryGetCameraFocus(out var prismPoint))
                {
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
        if (followLuminaProjectile && family == 2030)
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
        bool hasBounds = false;
        Vector3 center = Vector3.zero;
        int count = 0;
        void Include(BattleCharactor unit)
        {
            if (unit == null || unit.IsDead || unit.IsPlayer == context.Caster.IsPlayer ||
                unit.gameObject.scene != context.Caster.gameObject.scene) return;
            Bounds unitBounds = new Bounds(unit.transform.position + Vector3.up * 1.1f,
                new Vector3(1.5f, 2.2f, 1.5f));
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
        focusNekoBarrage = true;
        nekoLightTarget = null;
    }

    private float RequiredNekoBarrageFov(Vector3 cameraPosition)
    {
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
        if (nekoHealProjectiles.Remove(projectile)) return;
        if (projectile != luminaProjectile) return;
        luminaProjectile = null;
        projectileFocusPoint = point;
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

    private System.Collections.IEnumerator WaitAfterSkill(BattleCharactor actor, SkillData skill)
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
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
    }

    private float RequiredPairFov(Vector3 cameraPosition, Quaternion cameraRotation)
    {
        if (focusNekoBarrage) return RequiredNekoBarrageFov(cameraPosition);
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
        CancelTurnPresentation();
        if (cameraMotion != null) StopCoroutine(cameraMotion);
        cameraMotion = null;
        turnCameraView = -1;
        if (view == 2)
        {
            previewCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            previewCamera.fieldOfView = savedFov;
            return;
        }
        if (cameraProfile == null || playerPlace == null || enemyPlace == null) return;
        var pose = view == 0 ? cameraProfile.viewA : cameraProfile.viewB;
        pose.Resolve((playerPlace.position + enemyPlace.position) * .5f, out var position, out var rotation, out var fov);
        previewCamera.transform.SetPositionAndRotation(position, rotation);
        previewCamera.fieldOfView = fov;
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
        if (battle != null)
            foreach (var enemy in battle.EnemyBattleCharactors) ProtectEnemy(enemy);
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
        if (turnCameraView == view) return;
        turnCameraView = view;
        var pose = playerTurn ? cameraProfile.viewA : cameraProfile.viewB;
        pose.Resolve((playerPlace.position + enemyPlace.position) * .5f,
            out var position, out var rotation, out var fov);
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
            previewCamera.transform.SetPositionAndRotation(arcPosition, arcRotation);
            previewCamera.fieldOfView = Mathf.Lerp(startFov, fov, t);
            yield return null;
        }
        previewCamera.transform.SetPositionAndRotation(position, rotation);
        previewCamera.fieldOfView = fov;
        cameraMotion = null;
    }

    public void RestoreSkillPanelAfterFailedAction()
    {
        SetSkillPanelVisible(flow != null && flow.CurrentUnit != null && flow.CurrentUnit.IsPlayer);
    }

    public System.Collections.IEnumerator RunAttackWithZoom(BattleCharactor actor, BattleCharactor target,
        SkillData skill, System.Collections.IEnumerator attack)
    {
        // Immortal preview targets must still lose HP on every cast so damage-based heals can repeat.
        if (enemiesCannotDie && actor != null && actor.IsPlayer && battle != null)
            foreach (var enemy in battle.EnemyBattleCharactors)
                if (enemy != null && !enemy.IsDead)
                    enemy.SetHp(enemy.MaxHp);

        bool shouldFocusDebuffTarget = skill != null && skill.skillKey == "HCS005" &&
            target != null && !target.IsDead;
        int family = skill != null ? HeroSkillRules.FamilyId(skill.skillIndex) : 0;
        bool shouldFocusNekoHeal = actor != null && actor.PresentationIndex == "10004" &&
            (family == 4010 || family == 4030);
        bool shouldFocusNekoPaw = actor != null && actor.PresentationIndex == "10004" && family == 4020;
        bool shouldFocusNekoBarrage = actor != null && actor.PresentationIndex == "10004" && family == 4040;
        bool shouldFocusShieldGuard = actor != null && skill != null && skill.skillIndex == 200032;
        bool shouldFrameRifleBarrage = actor != null && skill != null && skill.skillIndex == 200022;
        if ((useTurnCamera && !shouldFocusDebuffTarget && !shouldFocusNekoHeal && !shouldFocusNekoPaw && !shouldFocusNekoBarrage && !shouldFrameRifleBarrage && !shouldFocusShieldGuard) || (!useAttackZoom && !shouldFocusShieldGuard) || previewCamera == null ||
            actor == null || (attackZoomAmount <= 0f && !shouldFocusShieldGuard) || attackZoomActive)
        {
            yield return ShowSkillSpeech(actor, skill);
            yield return WaitForLuminaCast(actor, skill);
            yield return attack;
            yield return WaitAfterSkill(actor, skill);
            yield break;
        }
        attackCameraPosition = previewCamera.transform.position;
        attackCameraRotation = previewCamera.transform.rotation;
        attackCameraFov = previewCamera.fieldOfView;
        attackCameraOrthoSize = previewCamera.orthographicSize;
        focusShieldGuard = shouldFocusShieldGuard;
        shieldGuardEffects.Clear();
        attackFocusActor = actor;
        attackFocusTarget = target;
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
        frameRangedPair = !focusShieldGuard && !focusDebuffTarget && target != null && skill != null &&
            ((frameEnemyRangedTarget && !actor.IsPlayer && target.IsPlayer &&
                !BattleManager.IsMeleeSkillRange(actor, skill)) ||
             (frameBlackBulletTarget && actor.IsPlayer && actor.PresentationIndex == "10003" && !target.IsPlayer));
        if (shouldFrameRifleBarrage) frameRangedPair = true;
        attackZoomActive = true;
        try
        {
            float scale = 1f - Mathf.Clamp(attackZoomAmount, 0f, .4f);
            Vector3 corner = attackCameraPosition + Vector3.down * Mathf.Max(0f, attackZoomDrop);
            Vector3 approach = AttackFocusPosition - corner;
            approach.y = 0f;
            Vector3 zoomPosition = corner + approach.normalized * Mathf.Min(attackZoomApproach, approach.magnitude * .5f);
            attackZoomBaseFov = Mathf.Max(1f, attackCameraFov * scale);
            float zoomFov = attackZoomBaseFov;
            if (frameRangedPair)
                zoomFov = Mathf.Max(zoomFov, RequiredPairFov(zoomPosition,
                    Quaternion.LookRotation(AttackFocusPosition - zoomPosition)));
            // 아래로 내려간 뒤 캐릭터 쪽으로 접근하며, ㄴ자 모서리를 곡선으로 부드럽게 연결합니다.
            yield return AnimateAttackZoom(zoomPosition, corner, attackCameraRotation,
                zoomFov, attackCameraOrthoSize * scale, attackZoomInSeconds, true);
            followAttackFocus = true;
            yield return ShowSkillSpeech(actor, skill);
            yield return WaitForLuminaCast(actor, skill);
            yield return attack;
            if (shouldFocusNekoBarrage)
                while (nekoBarrageEffect != null && nekoBarrageEffect.IsPlaying) yield return null;
            if (!focusNekoHeal && !focusShieldGuard) followAttackFocus = false;
            yield return WaitAfterSkill(actor, skill);
            followAttackFocus = false;
            focusNekoBarrage = false;
            yield return AnimateAttackZoom(attackCameraPosition, corner, attackCameraRotation,
                attackCameraFov, attackCameraOrthoSize, attackZoomOutSeconds, false);
        }
        finally
        {
            RestoreAttackCamera();
        }
    }

    private System.Collections.IEnumerator ShowSkillSpeech(BattleCharactor actor, SkillData skill)
    {
        var speech = FindFirstObjectByType<BattleSkillSpeechBubble>();
        if (speech != null && speech.gameObject.scene == gameObject.scene && speech.isActiveAndEnabled)
            yield return speech.ShowBeforeCast(actor, skill);
    }

    private System.Collections.IEnumerator AnimateAttackZoom(Vector3 position, Vector3 corner, Quaternion rotation,
        float fov, float orthoSize, float duration, bool focusActor)
    {
        Vector3 startPosition = previewCamera.transform.position;
        Quaternion startRotation = previewCamera.transform.rotation;
        float startFov = previewCamera.fieldOfView;
        float startSize = previewCamera.orthographicSize;
        float elapsed = 0f;
        while (elapsed < duration && previewCamera != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - (1f - t) * (1f - t);
            Vector3 cameraPosition = (1f - t) * (1f - t) * startPosition +
                2f * (1f - t) * t * corner + t * t * position;
            if (focusActor && attackFocusActor != null)
                rotation = Quaternion.LookRotation(AttackFocusPosition - cameraPosition);
            previewCamera.transform.SetPositionAndRotation(cameraPosition, Quaternion.Slerp(startRotation, rotation, t));
            previewCamera.fieldOfView = Mathf.Lerp(startFov, fov, t);
            previewCamera.orthographicSize = Mathf.Lerp(startSize, orthoSize, t);
            yield return null;
        }
        if (previewCamera != null)
        {
            if (focusActor && attackFocusActor != null)
                rotation = Quaternion.LookRotation(AttackFocusPosition - position);
            previewCamera.transform.SetPositionAndRotation(position, rotation);
            previewCamera.fieldOfView = fov;
            previewCamera.orthographicSize = orthoSize;
        }
    }

    private void RestoreAttackCamera()
    {
        if (!attackZoomActive) return;
        if (previewCamera != null)
        {
            previewCamera.transform.SetPositionAndRotation(attackCameraPosition, attackCameraRotation);
            previewCamera.fieldOfView = attackCameraFov;
            previewCamera.orthographicSize = attackCameraOrthoSize;
        }
        attackZoomActive = false;
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
        projectileFocusLatched = false;
    }

    private void SetSkillPanelVisible(bool visible)
    {
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

    private void FinishPreview()
    {
        // 결과 연출 확인 후 본게임 씬으로 이동하지 않습니다.
        if (battle != null) battle.StopAllCoroutines();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnDestroy()
    {
        UnitEffectPresenter.EffectSpawned -= HandleSkillEffectSpawned;
        BattleVisualDirector.HealPresented -= HandleHealPresented;
        if (flow != null) flow.OnParticipantRegistered -= ProtectEnemy;
        foreach (var handle in enemyImmortality.Values) handle.Dispose();
        enemyImmortality.Clear();
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightStarted -= HandleProjectileStarted;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightFinished -= HandleProjectileFinished;
        RestoreAttackCamera();
        CancelTurnPresentation();
        if (flow != null) flow.OnTurnStarted -= HandleTurnStarted;
        if (turnBanner != null) turnBanner.TurnBannerShown -= HandleTurnBannerShown;
        if (resultPanel != null) resultPanel.OnAccepted -= FinishPreview;
        if (started && CombatContext.Instance != null && CombatContext.Instance.IsSimulation)
            CombatContext.Instance.ClearSimulation();
    }
}
