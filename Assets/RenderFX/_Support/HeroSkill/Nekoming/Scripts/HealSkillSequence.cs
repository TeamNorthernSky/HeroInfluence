using System;
using System.Collections;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// Version B 확장 seam의 핵심: 힐 4단계를 시전자→타깃 좌표로 오케스트레이션.
    /// 좌표(시전 소켓/타깃)와 히트 타이밍은 호출자가 주입, 단계 체이닝·거동은 이 컴포넌트가 소유.
    /// 체인: 차징(손) → [hit 신호] → 발사체(손→타깃, OnFinished 착지) → 힐 오라(타깃 발밑).
    /// </summary>
    public class HealSkillSequence : MonoBehaviour
    {
        [Header("Stage VFX (모두 씬 배치 인스턴스)")]
        [Tooltip("차징 구체(손 소켓 자식으로 재부모됨). Play() 호출.")]
        [SerializeField] private VfxEffect chargeVfx;
        [Tooltip("발사체. Play(origin,target)로 손→타깃 비행, 도착 시 OnFinished.")]
        [SerializeField] private ProjectileVfx projectileVfx;
        [Tooltip("힐 오라(궤도+아크링). Play(center)로 타깃 발밑에서 재생.")]
        [SerializeField] private HealOrbitVfx healOrbitVfx;

        [Header("Placement")]
        [Tooltip("힐 오라 중심 = 타깃 위치 + (0,이 값,0). 발밑=0 권장.")]
        [SerializeField] private float targetFeetYOffset = 0f;

        public HealStageFlags RenderedStages { get; private set; }

        /// <param name="castSocket">시전 손 소켓(차징 부모 + 발사 시작점).</param>
        /// <param name="target">타깃 Transform(발사 도착 + 힐 오라 중심 기준).</param>
        /// <param name="hitReached">히트 타이밍 도달 여부 폴링. null이면 즉시 발사.</param>
        public IEnumerator Play(Transform castSocket, Transform target, System.Func<bool> hitReached)
        {
            RenderedStages = HealStageFlags.None;
            if (castSocket == null || target == null)
            {
                Debug.LogWarning("[HealSkillSequence] castSocket/target가 null입니다.", this);
                yield break;
            }

            // 1. 차징 (손 소켓에 부모 결합)
            if (chargeVfx != null)
            {
                chargeVfx.transform.SetParent(castSocket, false);
                chargeVfx.transform.localPosition = Vector3.zero;
                chargeVfx.Play();
                RenderedStages |= HealStageFlags.Charge;
            }

            // 2. 히트 타이밍 대기
            while (hitReached != null && !hitReached())
            {
                yield return null;
            }

            // 3. 발사체 (손→타깃), 도착까지 대기
            if (projectileVfx != null)
            {
                if (chargeVfx != null) chargeVfx.Stop();

                bool arrived = false;
                Action<VfxEffect> onFinished = _ => arrived = true;
                projectileVfx.OnFinished += onFinished;
                projectileVfx.Play(castSocket, target);
                RenderedStages |= HealStageFlags.Launch;

                while (!arrived) yield return null;
                projectileVfx.OnFinished -= onFinished;
            }

            // 4. 힐 오라 (타깃 발밑)
            if (healOrbitVfx != null)
            {
                Vector3 center = target.position + Vector3.up * targetFeetYOffset;
                healOrbitVfx.Play(center);
                RenderedStages |= HealStageFlags.Orbit | HealStageFlags.Heal;
            }
        }
    }
}
