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
    [InspectorName("블랙불릿 공격 시 대상도 함께 표시"), Tooltip("블랙불릿과 피격 대상을 함께 담고, 후열 대상이 잘리지 않도록 화각을 조절합니다.")]
    public bool frameBlackBulletTarget = true;
    [InspectorName("원거리 공격 초점의 피격자 비중"), Range(.5f, .8f)]
    [Tooltip("원거리 구도의 피격 대상 비중입니다. 값이 클수록 대상을 향합니다.")]
    public float rangedTargetFocusWeight = 0.65f;
    [Header("루미나 투사체 연출")]
    [Tooltip("루미나 투사체와 연쇄 효과를 카메라 초점이 따라갑니다.")]
    public bool followLuminaProjectile = true;
    [Tooltip("투사체 방향으로 카메라가 회전하는 추적 속도입니다.")]
    [InspectorName("투사체 초점 추적 속도"), Min(1f)] public float projectileFocusSpeed = 24f;
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
    private BattleCharactor attackFocusActor;
    private BattleCharactor attackFocusTarget;
    private bool frameRangedPair;
    private float attackZoomBaseFov;
    private Transform luminaProjectile;
    private JC.VFX.ChainLightningVfx luminaLightning;
    private JC.VFX.SolarPrismVfx luminaSolarPrism;
    private JC.VFX.PrismExplosionVfx luminaPrismExplosion;
    private BattleCharactor nekoLightTarget;
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
        }
        flow = battle != null ? battle.GetComponent<BattleFlowManager>() : null;
        if (flow != null) flow.OnTurnStarted += HandleTurnStarted;
        turnBanner = FindSceneComponent<BattleTurnBanner>();
        if (turnBanner != null) turnBanner.TurnBannerShown += HandleTurnBannerShown;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightStarted += HandleProjectileStarted;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightFinished += HandleProjectileFinished;
        UnitEffectPresenter.EffectSpawned += HandleSkillEffectSpawned;
    }

    private void LateUpdate()
    {
        if (!attackZoomActive || !followAttackFocus || previewCamera == null || attackFocusActor == null) return;
        Vector3 direction = AttackFocusPosition - previewCamera.transform.position;
        if (direction.sqrMagnitude > .001f)
            previewCamera.transform.rotation = Quaternion.Slerp(previewCamera.transform.rotation,
                Quaternion.LookRotation(direction), 1f - Mathf.Exp(-(projectileFocusLatched ? projectileFocusSpeed : 12f) * Time.unscaledDeltaTime));
        if (frameRangedPair && attackFocusTarget != null)
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

    private void HandleSkillEffectSpawned(SkillEffectContext context, GameObject instance, string cueName)
    {
        if (!attackZoomActive || context == null || context.Caster != attackFocusActor ||
            context.Presentation == null || instance == null) return;
        int family = HeroSkillRules.FamilyId(context.Presentation.SkillIndex);
        if (followLuminaProjectile && family == 2030)
            luminaSolarPrism = instance.GetComponentInChildren<JC.VFX.SolarPrismVfx>(true);
        if (followLuminaProjectile && family == 2040)
            luminaPrismExplosion = instance.GetComponentInChildren<JC.VFX.PrismExplosionVfx>(true);
        if (focusNekoLightTarget && family == 4020 &&
            context.PrimaryTarget != null && !context.PrimaryTarget.IsPlayer)
        {
            ClearNekoPawSequence();
            nekoPawSequence = instance.GetComponentInChildren<JC.VFX.JcVfxPartSequence>(true);
            if (nekoPawSequence != null && nekoPawSequence.TryGetTargetPartDelay(out float targetDelay))
            {
                nekoPawFocusRoutine = StartCoroutine(FocusNekoPawTarget(context.Caster,
                    context.PrimaryTarget, nekoPawSequence, Mathf.Max(0f, targetDelay + nekoPawFocusTimeOffset)));
            }
            else nekoLightTarget = context.PrimaryTarget;
        }
        if (focusNekoLightTarget && family == 4040 && cueName == "fire" &&
            context.PrimaryTarget != null && !context.PrimaryTarget.IsPlayer)
            nekoLightTarget = context.PrimaryTarget;
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
        if (!attackZoomActive || !followLuminaProjectile || actor != attackFocusActor || actor.PresentationIndex != "10002") return;
        luminaProjectile = projectile;
        projectileFocusPoint = projectile.position;
        projectileFocusLatched = true;
    }

    private void HandleProjectileFinished(BattleCharactor actor, Transform projectile, Vector3 point, bool arrived)
    {
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
        float tangent = 0f;
        Quaternion inverse = Quaternion.Inverse(cameraRotation);
        for (int unitIndex = 0; unitIndex < 2; unitIndex++)
        {
            var unit = unitIndex == 0 ? attackFocusActor : attackFocusTarget;
            if (unit == null) continue;
            for (int i = 0; i < 2; i++)
            {
                Vector3 local = inverse * (unit.transform.position + Vector3.up * (i == 0 ? 0f : 2.2f) - cameraPosition);
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
            if (panelMotion != null) yield return panelMotion;
            if (useTurnCamera && actor != null)
            {
                MoveCameraForTurn(actor.IsPlayer);
                if (cameraMotion != null) yield return cameraMotion;
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
        if (useTurnCamera || !useAttackZoom || previewCamera == null || actor == null || attackZoomAmount <= 0f || attackZoomActive)
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
        attackFocusActor = actor;
        attackFocusTarget = target;
        frameRangedPair = target != null && skill != null &&
            ((frameEnemyRangedTarget && !actor.IsPlayer && target.IsPlayer &&
                !BattleManager.IsMeleeSkillRange(actor, skill)) ||
             (frameBlackBulletTarget && actor.IsPlayer && actor.PresentationIndex == "10003" && !target.IsPlayer));
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
            followAttackFocus = false;
            yield return WaitAfterSkill(actor, skill);
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
        var speech = FindSceneComponent<BattleSkillSpeechBubble>();
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
        attackFocusActor = null;
        attackFocusTarget = null;
        frameRangedPair = false;
        luminaProjectile = null;
        luminaLightning = null;
        luminaSolarPrism = null;
        luminaPrismExplosion = null;
        nekoLightTarget = null;
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
        if (panelMotion != null) yield return panelMotion;
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
        UnitEffectPresenter.EffectSpawned -= HandleSkillEffectSpawned;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightStarted -= HandleProjectileStarted;
        ASB.Work.Battle.Sequence.ProjectileImpactAction.FlightFinished -= HandleProjectileFinished;
        RestoreAttackCamera();
        CancelTurnPresentation();
        if (flow != null) flow.OnTurnStarted -= HandleTurnStarted;
        if (turnBanner != null) turnBanner.TurnBannerShown -= HandleTurnBannerShown;
    }
}
