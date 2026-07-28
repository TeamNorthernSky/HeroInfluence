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
                case ProjectileTrajectoryType.OverheadDrop:
                    return new OverheadDropTrajectory();
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

    /// <summary>
    /// 낙하형: start에서 "대상 바로 위(end + up*height)"로 로프트한 뒤, 그 지점에서 end로 수직 하강한다.
    /// height는 ProjectileVisualData.ArcHeight를 대상 위 체공 높이로 재사용한다. 도착 = 하강 완료.
    /// </summary>
    public sealed class OverheadDropTrajectory : IProjectileTrajectory
    {
        private Vector3 _start;
        private Vector3 _end;
        private float _height;
        private float _speed;
        private float _loftDur;
        private float _dropDur;
        private float _elapsed;

        public void Init(Vector3 start, Vector3 end, ProjectileVisualData data)
        {
            _start = start;
            _end = end;
            _height = data != null ? Mathf.Max(0.5f, data.ArcHeight) : 2f;
            _speed = data != null ? Mathf.Max(0.01f, data.Speed) : 6f;
            _elapsed = 0f;
            RecomputeDurations();
        }

        private void RecomputeDurations()
        {
            Vector3 overhead = _end + Vector3.up * _height;
            float loftDist = Vector3.Distance(_start, overhead);
            _loftDur = loftDist > 0.001f ? loftDist / _speed : 0.0001f;
            _dropDur = _height > 0.001f ? _height / _speed : 0.0001f;
        }

        public bool Step(float dtBattle, Transform trackedTarget, out Vector3 position)
        {
            // 추적형: 대상 현재 위치를 끝점으로 갱신(그 위 체공점도 함께 이동).
            if (trackedTarget != null)
            {
                _end = trackedTarget.position;
                RecomputeDurations();
            }

            _elapsed += dtBattle;
            Vector3 overhead = _end + Vector3.up * _height;

            // 1단계: 대상 바로 위로 로프트(살짝 포물선).
            if (_elapsed < _loftDur)
            {
                float t = Mathf.Clamp01(_elapsed / _loftDur);
                Vector3 p = Vector3.Lerp(_start, overhead, t);
                p.y += _height * 0.35f * 4f * t * (1f - t);
                position = p;
                return false;
            }

            // 2단계: 체공점 → 대상으로 수직 하강.
            float dropT = Mathf.Clamp01((_elapsed - _loftDur) / _dropDur);
            position = Vector3.Lerp(overhead, _end, dropT);
            if (dropT >= 1f)
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
