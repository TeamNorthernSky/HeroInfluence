namespace ASB.Work.Battle.Core
{
    /// <summary>
    /// The outcome of a visual projectile delivery. Fallback preserves the normal
    /// immediate-hit path; Cancelled suppresses all delivery-dependent effects.
    /// </summary>
    public enum ProjectileDeliveryResult
    {
        Arrived,
        Fallback,
        Cancelled
    }

    /// <summary>
    /// Per-command mutable delivery state. It deliberately defaults to Arrived so
    /// non-projectile combat paths retain their existing behavior.
    /// </summary>
    public sealed class HitDeliveryGate
    {
        public ProjectileDeliveryResult Result { get; private set; } = ProjectileDeliveryResult.Arrived;

        public bool CanApplyEffects => Result != ProjectileDeliveryResult.Cancelled;

        public void SetResult(ProjectileDeliveryResult result)
        {
            // A cancelled delivery must not be revived by a later combo beat.
            if (Result == ProjectileDeliveryResult.Cancelled)
            {
                return;
            }

            Result = result;
        }
    }
}
