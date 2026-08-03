using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 씬-공간(lossyScale 1) 트레일/반짝임 홀더. 오브의 월드 위치를 매 프레임 따라간다.
    /// 소켓 lossyScale 100 하에서 파티클/트레일 크기가 뒤틀리는 것을 피하려 씬 루트에 두고 위치만 추종.
    /// - bandTrail: 연속 띠(TrailRenderer). 팔로워가 손 경로를 따라가며 리본을 그린다.
    /// - systems: 반짝임 등 부수 파티클.
    /// </summary>
    public class ChargeTrailFollower : MonoBehaviour
    {
        [SerializeField] private TrailRenderer bandTrail;
        [SerializeField] private ParticleSystem[] systems;

        public void Begin(Vector3 worldPos)
        {
            transform.position = worldPos;
            if (bandTrail) { bandTrail.Clear(); bandTrail.emitting = true; }
            foreach (var ps in systems)
            {
                if (!ps) continue;
                ps.Clear();
                ps.Play();
            }
        }

        public void Follow(Vector3 worldPos) => transform.position = worldPos;

        public void EndEmit()
        {
            if (bandTrail) bandTrail.emitting = false;                 // 꼬리 자연 소멸
            foreach (var ps in systems)
            {
                if (!ps) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
