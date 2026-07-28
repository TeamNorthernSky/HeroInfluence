using UnityEngine;

namespace ASB.Work.Battle.Core
{
    /// <summary>
    /// 캐스트 1회 동안의 체인 투사체 상태. 주 타깃 도착 지점을 저장해 추가 타깃 투사체의 "원점"으로 사용한다.
    /// 원점은 타깃 Transform이 아니라 저장된 월드 좌표라서, 1차 명중으로 대상이 사망·제거되어도 2차 시작점이 유지된다.
    /// </summary>
    public sealed class ProjectileChainState
    {
        /// <summary>직전(주 타깃) 투사체의 실제 도착 좌표. 다음 단계 투사체의 발사 원점.</summary>
        public Vector3 LastImpactPoint;

        /// <summary>직전 단계가 도착(또는 폴백)해 유효한 LastImpactPoint가 있는지.</summary>
        public bool HasPreviousImpact;

        /// <summary>1차가 취소되어 체인 전체를 중단해야 하는지.</summary>
        public bool IsCancelled;
    }
}
