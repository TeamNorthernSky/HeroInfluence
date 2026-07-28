using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 투사체 "경로 전략". "어떻게 나는가"와 "언제 도착했는가"만 소유하고 전투는 모른다.
    /// ProjectileImpactAction이 배속 반영 dt로 매 프레임 Step을 호출한다.
    /// 새 움직임 추가 = 새 구현 클래스 하나. 전투/게이트 코드는 절대 바뀌지 않는다.
    /// </summary>
    public interface IProjectileTrajectory
    {
        void Init(Vector3 start, Vector3 end, ProjectileVisualData data);

        /// <summary>배속 반영 dt로 한 프레임 전진. 도착이면 true, 현재 위치를 out.</summary>
        bool Step(float dtBattle, Transform trackedTarget, out Vector3 position);
    }

    /// <summary>ProjectileVisualData.Trajectory로 궤적 구현을 선택한다.</summary>
    public static class ProjectileTrajectoryFactory
    {
        public static IProjectileTrajectory Create(ProjectileTrajectoryType type)
        {
            switch (type)
            {
                case ProjectileTrajectoryType.Straight:
                    return new StraightTrajectory();
                case ProjectileTrajectoryType.Arc:
                default:
                    return new ArcTrajectory();
            }
        }
    }

    /// <summary>포물선. 도착 = 정해진 비행 거리 완료(t &gt;= 1). ArrivalRadius는 사용하지 않는다.</summary>
    public sealed class ArcTrajectory : IProjectileTrajectory
    {
        private Vector3 _start;
        private Vector3 _end;
        private float _arcHeight;
        private float _flightDur;
        private float _t;

        public void Init(Vector3 start, Vector3 end, ProjectileVisualData data)
        {
            _start = start;
            _end = end;
            _arcHeight = data != null ? Mathf.Max(0f, data.ArcHeight) : 0f;
            float speed = data != null ? Mathf.Max(0.01f, data.Speed) : 6f;
            float dist = Vector3.Distance(start, end);
            _flightDur = dist > 0.001f ? dist / speed : 0.0001f;
            _t = 0f;
        }

        public bool Step(float dtBattle, Transform trackedTarget, out Vector3 position)
        {
            // 추적형: 끝점만 대상 현재 위치를 따라간다. 도착 판정은 여전히 t >= 1.
            if (trackedTarget != null)
            {
                _end = trackedTarget.position;
            }

            _t += dtBattle / _flightDur;
            float t = Mathf.Clamp01(_t);
            Vector3 p = Vector3.Lerp(_start, _end, t);
            p.y += _arcHeight * 4f * t * (1f - t);
            position = p;

            if (t >= 1f)
            {
                position = _end;
                return true;
            }

            return false;
        }
    }

    /// <summary>직선. 도착 = 정해진 비행 거리 완료(t &gt;= 1). ArrivalRadius는 사용하지 않는다.</summary>
    public sealed class StraightTrajectory : IProjectileTrajectory
    {
        private Vector3 _start;
        private Vector3 _end;
        private float _flightDur;
        private float _t;

        public void Init(Vector3 start, Vector3 end, ProjectileVisualData data)
        {
            _start = start;
            _end = end;
            float speed = data != null ? Mathf.Max(0.01f, data.Speed) : 6f;
            float dist = Vector3.Distance(start, end);
            _flightDur = dist > 0.001f ? dist / speed : 0.0001f;
            _t = 0f;
        }

        public bool Step(float dtBattle, Transform trackedTarget, out Vector3 position)
        {
            if (trackedTarget != null)
            {
                _end = trackedTarget.position;
            }

            _t += dtBattle / _flightDur;
            float t = Mathf.Clamp01(_t);
            position = Vector3.Lerp(_start, _end, t);

            if (t >= 1f)
            {
                position = _end;
                return true;
            }

            return false;
        }
    }

    // 후속 과제(이번 범위 아님): HomingTrajectory 등은 이 인터페이스만 구현하면 된다.
    // Homing 도착 판정은 "대상까지 거리 <= ProjectileVisualData.ArrivalRadius", 등속은 매 프레임
    // 남은 방향으로 Speed*dtBattle 전진으로 구현한다. 전투/게이트 코드는 건드리지 않는다.
}
