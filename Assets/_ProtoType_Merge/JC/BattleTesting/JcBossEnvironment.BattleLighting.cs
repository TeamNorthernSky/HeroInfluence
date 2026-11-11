using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting
{
    public sealed partial class JcBossEnvironment
    {
        // JC | 2026-11-09 | 실제/테스트 공통 전투 이벤트를 읽기 구독합니다. ASB 턴·페이즈·이동 판정에는 쓰지 않습니다.
        BattleFlowManager lightingFlow;
        BossController lightingBoss;
        BattleCharactor lightingBossUnit, movingWheel;
        Vector3 wheelOrigin;
        bool wheelArmed, lightingBattleEnded;
        float nextLightingLookup;
        double pulseStarted = double.NegativeInfinity, nextPulseAllowed = double.NegativeInfinity;

        // JC | 2026-11-10 | 테스트도 TmpBattleScene으로 이동하므로 씬 이름 대신 기존 보스 테스트 진입 정보를 읽습니다.
        public bool IsBossLightingTest
        {
            get
            {
                var context = CombatContext.Instance;
                return context && context.IsSimulation && context.SimulationPartyId == "JC_BOSS_PREVIEW";
            }
        }

        // JC | 2026-11-10 | 행동에 따른 색 변화만 분리합니다. 기본 조명과 보스 강조 스폿의 조준은 유지합니다.
        public bool BattlePulseEnabled => Draft &&
            (IsBossLightingTest ? Draft.battlePulseInBossTestEnabled : Draft.battlePulseEnabled);

        void LateUpdate()
        {
            if (!Application.isPlaying || !initialized || !Draft || !Stage || SceneManager.GetActiveScene() != gameObject.scene) return;
            if (!lightingBattleEnded) BindBattleLighting();
            ObserveWheelMotion(Time.unscaledTimeAsDouble);
            ApplyBattleLighting(Time.unscaledTimeAsDouble);
        }

        // JC | 2026-11-09 | 색 변화 시작 판정을 렌더 갱신과 분리하여 전투 로직을 실행하지 않고도 검증합니다.
        void ObserveWheelMotion(double now)
        {
            // JC | 2026-11-09 | 턴 시작/충전이 아니라 굴렁쇠 루트의 실제 수평 이동 시작에 한 번만 반응합니다.
            if (wheelArmed && movingWheel && !movingWheel.IsDead && movingWheel.gameObject.activeInHierarchy)
            {
                var delta = movingWheel.transform.position - wheelOrigin; delta.y = 0;
                if (delta.sqrMagnitude > .0004f) { wheelArmed = false; TriggerBattlePulse(now); }
            }
        }

        void BindBattleLighting()
        {
            if (Time.unscaledTime < nextLightingLookup) return;
            nextLightingLookup = Time.unscaledTime + .25f;
            if (!lightingFlow)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    lightingFlow = root.GetComponentInChildren<BattleFlowManager>();
                    if (lightingFlow) break;
                }
                if (!lightingFlow) return;
                lightingFlow.OnParticipantRegistered += BindLightingActor;
                lightingFlow.OnTurnStarted += LightingTurnStarted;
                lightingFlow.OnTurnResolved += LightingTurnResolved;
                lightingFlow.OnBattleEnded += LightingBattleEnded;
            }
            if (!lightingBoss)
                foreach (var actor in lightingFlow.Participants) BindLightingActor(actor);
        }

        void BindLightingActor(BattleCharactor actor)
        {
            if (!actor || actor.IsPlayer || actor.IsDead || actor.gameObject.scene != gameObject.scene ||
                BattleScenarioUnitKey.Normalize(actor.TemplateIndex) != "40001") return;
            var controller = actor.GetComponent<BossController>();
            if (!controller || controller == lightingBoss) return;
            if (lightingBoss) lightingBoss.ImmediatePhaseEntered -= LightingPhaseEntered;
            lightingBoss = controller; lightingBossUnit = actor;
            lightingBoss.ImmediatePhaseEntered += LightingPhaseEntered;
        }

        void LightingPhaseEntered(int phase)
        {
            if (phase == 2 && lightingBossUnit && !lightingBossUnit.IsDead) TriggerBattlePulse(Time.unscaledTimeAsDouble);
        }

        void LightingTurnStarted(int round, BattleCharactor actor)
        {
            movingWheel = null; wheelArmed = false;
            BindLightingActor(actor);
            if (!actor || actor.IsPlayer || actor.IsDead || actor.gameObject.scene != gameObject.scene || !actor.GetComponent<HoopWheelVisual>()) return;
            movingWheel = actor; wheelOrigin = actor.transform.position; wheelArmed = true;
        }

        void LightingTurnResolved(TurnResolutionContext result)
        {
            if (result != null && result.Actor == movingWheel) { wheelArmed = false; movingWheel = null; }
        }

        void LightingBattleEnded(BattleResult result)
        {
            StopBattleLighting(); lightingBattleEnded = true;
        }

        // JC | 2026-11-09 | 배속과 독립된 실제 시간, 겹침 무시, 최소 4초 간격으로 반복 점멸을 방지합니다.
        void TriggerBattlePulse(double now)
        {
            if (!isActiveAndEnabled || !BattlePulseEnabled || lightingBattleEnded || now < nextPulseAllowed) return;
            pulseStarted = now;
            nextPulseAllowed = now + Mathf.Max(4, Draft.battlePulseInterval, Draft.battlePulseSeconds);
        }

        internal static float BattlePulseEnvelope(double elapsed, float seconds)
        {
            float t = (float)(elapsed / Mathf.Max(1.2f, seconds));
            if (t <= 0 || t >= 1) return 0;
            return t < .35f ? Mathf.SmoothStep(0,1,t/.35f) : Mathf.SmoothStep(1,0,(t-.35f)/.65f);
        }

        void ApplyBattleLighting(double now)
        {
            if (!Draft || Draft.lights == null) return;
            if (!BattlePulseEnabled) pulseStarted = double.NegativeInfinity;
            float mix = BattlePulseEnvelope(now-pulseStarted,Draft.battlePulseSeconds) * Mathf.Clamp(Draft.battlePulseMix,0,.3f);
            var violet = Draft.battlePulseColor;
            violet = new Color(Mathf.Clamp01(violet.r),Mathf.Clamp01(violet.g),Mathf.Clamp01(violet.b),1);
            for (int i=0;i<localLights.Count && i<Draft.lights.Count;i++)
            {
                var spec = Draft.lights[i]; var light = localLights[i];
                if (spec == null || !light || spec.playerBackFill) continue;
                // JC | 2026-11-09 | 밝기는 증폭하지 않습니다. 색 변화도 원래 색보다 채널별 밝아지지 않게 제한합니다.
                light.color = spec.battlePulse ? Color.Lerp(spec.color, spec.color * violet, mix) : spec.color;
                if (spec.aimAtBoss && spec.type == JcBossEnvironmentProfile.LocalLightType.Spot && lightingBossUnit && !lightingBossUnit.IsDead)
                {
                    var direction = lightingBossUnit.transform.position + Vector3.up * 1.2f - light.transform.position;
                    if (direction.sqrMagnitude > .001f) light.transform.rotation = Quaternion.LookRotation(direction, Stage.up);
                }
            }
        }

        void StopBattleLighting()
        {
            if (lightingFlow)
            {
                lightingFlow.OnParticipantRegistered -= BindLightingActor;
                lightingFlow.OnTurnStarted -= LightingTurnStarted;
                lightingFlow.OnTurnResolved -= LightingTurnResolved;
                lightingFlow.OnBattleEnded -= LightingBattleEnded;
            }
            if (lightingBoss) lightingBoss.ImmediatePhaseEntered -= LightingPhaseEntered;
            lightingFlow = null; lightingBoss = null; lightingBossUnit = null; movingWheel = null;
            wheelArmed = false; pulseStarted = double.NegativeInfinity; nextPulseAllowed = double.NegativeInfinity;
            nextLightingLookup = 0;
            ApplyBattleLighting(Time.unscaledTimeAsDouble);
        }
    }
}
