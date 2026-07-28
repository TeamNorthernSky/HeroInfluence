using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 투사체 아트 프리팹에 붙일 수 있는 "선택적" 연출 훅.
    /// 없어도 정상 발사다(부재는 실패가 아님) — 비행/트레일/파티클은 ProjectileImpactAction이 담당한다.
    /// 이 컴포넌트는 프리팹이 발사/착탄 순간에 추가 연출로 반응하고 싶을 때만 사용한다.
    /// 전투 타입(피해/상태이상/DamageContext)은 절대 참조하지 않는다.
    /// </summary>
    public sealed class BattleProjectileVisual : MonoBehaviour
    {
        /// <summary>발사 순간 1회. 커스텀 연출 시작용.</summary>
        public void OnLaunched()
        {
        }

        /// <summary>도착 순간 1회. impactPoint는 위치 정보만 담는다.</summary>
        public void OnImpact(Vector3 impactPoint)
        {
        }
    }
}
