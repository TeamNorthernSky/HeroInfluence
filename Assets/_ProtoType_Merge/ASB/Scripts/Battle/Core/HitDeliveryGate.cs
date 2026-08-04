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
    ///
    /// 이 게이트는 <b>연출 전용</b>이다. 연출은 피해 확정의 '시점'만 정하고 '여부'는 정하지 못한다.
    /// Cancelled여도 규칙 계층(ApplySkillExecutionResultRoutine)이 루틴 종료 전에 미확정 히트를
    /// 일괄 확정하므로, 이 값은 "임팩트 연출을 재생할지"만 의미한다.
    /// </summary>
    public sealed class HitDeliveryGate
    {
        public ProjectileDeliveryResult Result { get; private set; } = ProjectileDeliveryResult.Arrived;

        /// <summary>임팩트 연출(피격 이펙트/데미지 팝업/피격 애니)을 재생해도 되는가. 피해 적용 여부와 무관하다.</summary>
        public bool ShouldPlayImpactPresentation => Result != ProjectileDeliveryResult.Cancelled;

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
