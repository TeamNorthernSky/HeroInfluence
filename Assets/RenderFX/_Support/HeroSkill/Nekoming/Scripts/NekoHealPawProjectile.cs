using ASB.Work.Battle.Sequence;
using UnityEngine;

namespace JC.VFX
{
    // The battle sequence owns flight; this component only displays the shared paw shape.
    [DefaultExecutionOrder(-50)]
    public sealed class NekoHealPawProjectile : MonoBehaviour
    {
        public PawSprite paw;
        private float elapsed;
        private float playbackSpeed = 1f;
        private bool arrived;

        private void OnEnable()
        {
            elapsed = 0f;
            arrived = false;
            ProjectileImpactAction.FlightStarted += HandleStarted;
            ProjectileImpactAction.FlightFinished += HandleFinished;
            if (paw == null) return;
            paw.SetBasePosition(transform.position);
            paw.SetScaleMul(1f);
            paw.SetEnvelope(0f);
            paw.Show();
        }

        private void OnDisable()
        {
            ProjectileImpactAction.FlightStarted -= HandleStarted;
            ProjectileImpactAction.FlightFinished -= HandleFinished;
        }

        private void HandleStarted(BattleCharactor actor, Transform projectile)
        {
            if (projectile != transform) return;
            actor?.GetComponent<PresentationRuntimeContext>()?.StopAndRemoveHandle("neko_heal_charge");
            playbackSpeed = actor != null && actor.Anim != null ? actor.Anim.CurrentAnimSpeed : 1f;
        }

        private void HandleFinished(BattleCharactor actor, Transform projectile, Vector3 point, bool hit)
        {
            if (projectile != transform) return;
            arrived = true;
            elapsed = 0f;
        }

        private void Update()
        {
            if (paw == null) return;
            elapsed += Time.deltaTime * Mathf.Max(.01f, playbackSpeed);
            paw.SetBasePosition(transform.position);
            float t = Mathf.Clamp01(elapsed / (arrived ? .2f : .07f));
            paw.SetEnvelope(arrived ? 1f - Mathf.SmoothStep(0f, 1f, t) : Mathf.SmoothStep(0f, 1f, t));
            paw.SetScaleMul(arrived ? Mathf.Lerp(1f, 1.15f, t) : 1f);
        }
    }
}
