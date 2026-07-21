using System;
using System.Collections;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// Version B 어댑터: ASB CharactorAnimationController로 시전 애니 + 히트 타이밍을 잡고(대조군과 동일 소스),
    /// 시각 연출만 EffectManager 대신 JC HealSkillSequence로 라우팅한다.
    /// 이것이 ASB가 향후 채택하면 좋을 확장 계약 Play(caster, target, timing)의 프로토타입.
    /// </summary>
    public class AsbSeamHealDriver : MonoBehaviour
    {
        [Header("Actors (ASB 리프)")]
        [SerializeField] private CharactorAnimationController casterAnim;
        [SerializeField] private CharactorAnimationController targetAnim;
        [SerializeField] private SkillData healSkill;

        [Header("Seam")]
        [SerializeField] private HealSkillSequence sequence;
        [Tooltip("차징 부모 + 발사 시작점(시전 손 소켓).")]
        [SerializeField] private Transform castSocket;
        [Tooltip("발사 도착 + 힐 오라 중심 기준.")]
        [SerializeField] private Transform target;

        [SerializeField] private float animEventTimeout = 2f;

        public HealStageFlags RenderedStages => sequence != null ? sequence.RenderedStages : HealStageFlags.None;

        public IEnumerator Run()
        {
            if (casterAnim != null)
            {
                casterAnim.ResetHitEvent();
                casterAnim.PlaySkillAnimation(healSkill); // ClassSkill_4
            }

            Func<bool> hitReached = BuildHitReached();

            if (sequence != null)
            {
                yield return StartCoroutine(sequence.Play(castSocket, target, hitReached));
            }

            if (targetAnim != null)
            {
                targetAnim.PlayGenericAnimation(healSkill != null ? healSkill.ResolvedTargetAnimationTrigger : "Hit");
            }
        }

        private Func<bool> BuildHitReached()
        {
            if (healSkill != null && healSkill.UseAnimEvent && casterAnim != null)
            {
                float start = Time.time;
                return () => casterAnim.IsHitEventReached || (Time.time - start) >= animEventTimeout;
            }
            else
            {
                float delay = healSkill != null ? Mathf.Max(0f, healSkill.HitDelay) : 0.25f;
                float start = Time.time;
                return () => (Time.time - start) >= delay;
            }
        }
    }
}
