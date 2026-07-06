using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 투사체 컨테이너(빈 오브젝트) 역할. 실제 파티클/모델 이펙트는 자식으로 붙습니다.
    /// 궤적 계산만 담당하고, 데미지/이펙트 판정은 호출부(SpawnProjectileAction)가 담당합니다.
    /// </summary>
    public class ProjectileController : MonoBehaviour
    {
        private static readonly List<ProjectileController> Active = new List<ProjectileController>();

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        /// <summary>전투 종료 등으로 남아있는 투사체를 전부 정리합니다.</summary>
        public static void DestroyAllActive()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i] != null)
                {
                    Destroy(Active[i].gameObject);
                }
            }

            Active.Clear();
        }

        /// <summary>
        /// startPosition에서 targetPosition까지 날아갑니다. 도착하면 반환됩니다(코루틴 완료).
        /// battleSpeed가 클수록 빨리 도착합니다.
        /// </summary>
        public IEnumerator Fly(
            Vector3 startPosition,
            Vector3 targetPosition,
            float flightTime,
            ProjectileTrajectoryType trajectoryType,
            float arcHeight,
            float battleSpeed)
        {
            transform.position = startPosition;
            LookAtSafe(targetPosition);

            float duration = Mathf.Max(0.01f, flightTime);
            float safeBattleSpeed = Mathf.Max(0.01f, battleSpeed);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime * safeBattleSpeed;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 pos = Vector3.Lerp(startPosition, targetPosition, t);
                if (trajectoryType == ProjectileTrajectoryType.Arc)
                {
                    pos.y += arcHeight * 4f * t * (1f - t);
                }

                transform.position = pos;
                LookAtSafe(pos + (targetPosition - startPosition).normalized);

                yield return null;
            }

            transform.position = targetPosition;
        }

        private void LookAtSafe(Vector3 lookTarget)
        {
            Vector3 dir = lookTarget - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }
    }
}
