using System.Collections;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// Version A 대조군: ASB가 "오늘 제공하는 API"(EffectManager.SpawnById)만으로 힐을 그린다.
    /// SpawnById(id, pos, rot)에는 타깃 인자가 없어 발사체(손→타깃)·궤도를 실을 수 없다.
    /// 결과: 차징(시전자 소켓) + 힐 오라(타깃 소켓)만 렌더 → "반쪽 힐"이 현 규약 한계의 증거.
    /// </summary>
    public class AsbContractHealDriver : MonoBehaviour
    {
        [Header("Actors (ASB 리프)")]
        [SerializeField] private CharactorAnimationController casterAnim;
        [SerializeField] private CharactorAnimationController targetAnim;
        [SerializeField] private SkillData healSkill;

        [Header("Sockets")]
        [Tooltip("차징 이펙트 생성 위치(시전 손).")]
        [SerializeField] private Transform castSocket;
        [Tooltip("힐 오라 생성 위치(타깃 발밑).")]
        [SerializeField] private Transform targetHitSocket;

        [Header("EffectRegistry ids")]
        [SerializeField] private int chargeEffectId = 1;
        [SerializeField] private int healAuraEffectId = 4;

        [Tooltip("UseAnimEvent=false일 때 히트까지 대기(초).")]
        [SerializeField] private float animEventTimeout = 2f;

        public HealStageFlags RenderedStages { get; private set; }

        public IEnumerator Run()
        {
            RenderedStages = HealStageFlags.None;

            if (casterAnim != null)
            {
                casterAnim.ResetHitEvent();
                casterAnim.PlaySkillAnimation(healSkill); // ClassSkill_4 CrossFade
            }

            // 차징 = ASB 공격 이펙트 유사(시전자 소켓에 SpawnById)
            if (EffectManager.Instance != null && castSocket != null)
            {
                EffectManager.Instance.SpawnById(chargeEffectId, castSocket.position, castSocket.rotation);
                RenderedStages |= HealStageFlags.Charge;
            }

            yield return WaitHit();

            // 힐 오라 = ASB 히트 이펙트 유사(타깃 소켓에 SpawnById)
            if (EffectManager.Instance != null && targetHitSocket != null)
            {
                EffectManager.Instance.SpawnById(healAuraEffectId, targetHitSocket.position, targetHitSocket.rotation);
                RenderedStages |= HealStageFlags.Heal;
            }
            if (targetAnim != null)
            {
                targetAnim.PlayGenericAnimation(healSkill != null ? healSkill.ResolvedTargetAnimationTrigger : "Hit");
            }

            // ── 갭: SpawnById에 타깃 인자가 없어 Launch/Orbit 불가. RenderedStages에 미포함. ──
        }

        private IEnumerator WaitHit()
        {
            if (healSkill != null && healSkill.UseAnimEvent && casterAnim != null)
            {
                float elapsed = 0f;
                while (!casterAnim.IsHitEventReached && elapsed < animEventTimeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                float delay = healSkill != null ? Mathf.Max(0f, healSkill.HitDelay) : 0.25f;
                float elapsed = 0f;
                while (elapsed < delay)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }
    }
}
