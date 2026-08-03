using System;
using System.Collections;
using ASB.Work.Battle.Core;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    public sealed class CustomEffectImpactAction : BattleSequenceAction
    {
        private readonly SkillPresentationData _presentation;
        private readonly int _actionInstanceId;
        private readonly HitDeliveryGate _deliveryGate;
        private readonly BattleSequenceAction _innerAction;

        public CustomEffectImpactAction(SkillPresentationData presentation, int actionInstanceId, HitDeliveryGate deliveryGate, BattleSequenceAction innerAction)
        {
            _presentation = presentation; _actionInstanceId = actionInstanceId; _deliveryGate = deliveryGate; _innerAction = innerAction;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            yield return WaitForImpactRoutine(_presentation, _actionInstanceId, _deliveryGate);
            if (_deliveryGate == null || !_deliveryGate.ShouldPlayImpactPresentation || _innerAction == null) yield break;
            yield return _innerAction.ExecuteRoutine(host);
        }

        public static IEnumerator WaitForImpactRoutine(SkillPresentationData presentation, int actionInstanceId, HitDeliveryGate deliveryGate)
        {
            yield return WaitForImpactKeyRoutine(presentation, new ImpactKey(actionInstanceId), deliveryGate, true);
        }

        public static IEnumerator WaitForImpactKeyRoutine(SkillPresentationData presentation, ImpactKey key, HitDeliveryGate deliveryGate, bool requireCustomDeliveryMode)
        {
            if (presentation == null || key.ActionInstanceId <= 0 || (requireCustomDeliveryMode && presentation.ImpactDeliveryMode != SkillImpactDeliveryMode.CustomEffectImpact)) yield break;
            bool received = false;
            bool cancelled = false;
            if (SkillImpactSignalBus.TryConsume(key, out SkillImpactSignal pending))
            {
                received = true; cancelled = pending.Cancelled;
            }
            else
            {
                Action<SkillImpactSignal> onImpact = signal => { if (signal.Key.Equals(key)) { received = true; cancelled = signal.Cancelled; } };
                SkillImpactSignalBus.Raised += onImpact;
                try
                {
                    if (SkillImpactSignalBus.TryConsume(key, out pending)) { received = true; cancelled = pending.Cancelled; }
                    float timeout = Mathf.Max(0.1f, presentation.CustomImpactTimeoutSeconds);
                    float elapsed = 0f;
                    while (!received && elapsed < timeout) { elapsed += Time.deltaTime; yield return null; }
                }
                finally { SkillImpactSignalBus.Raised -= onImpact; }
            }
            SkillImpactSignalBus.Clear(key);
            if (cancelled) { deliveryGate?.SetResult(ProjectileDeliveryResult.Cancelled); yield break; }
            if (!received) Debug.LogWarning("[SkillImpact] Timed out for impact " + key + "; applying damage at normal timing.");
        }
    }
}