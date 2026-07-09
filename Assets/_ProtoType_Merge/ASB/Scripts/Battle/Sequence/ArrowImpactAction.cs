using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 궁수 전용: 포물선 라인과 화살을 동시에 표시한 뒤 라인만 페이드아웃합니다.
    /// 트레일 시작(또는 트레일 없을 때 화살 스폰) 시점에 타겟 Hit 애니를 재생합니다.
    /// WaitHitAction과 ResolveHitAction 사이에 삽입됩니다.
    /// </summary>
    public class ArrowImpactAction : BattleSequenceAction
    {
        private readonly BattleCharactor _actor;
        private readonly BattleCharactor _target;
        private readonly float _battleSpeed;
        private readonly string _targetAnimTrigger;

        private readonly Gradient _gradient = new Gradient();
        private readonly GradientColorKey[] _colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        };
        private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[2];

        public ArrowImpactAction(
            BattleCharactor actor,
            BattleCharactor target,
            float battleSpeed,
            string targetAnimTrigger = null)
        {
            _actor = actor;
            _target = target;
            _battleSpeed = Mathf.Max(0.01f, battleSpeed);
            _targetAnimTrigger = targetAnimTrigger;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_target == null || _target.IsDead)
            {
                yield break;
            }

            UnitVisualProfile actorProfile = _actor?.GetComponent<UnitVisualProfile>();
            UnitVisualProfile targetProfile = _target.GetComponent<UnitVisualProfile>();

            actorProfile?.HoldArrow?.SetActive(false);

            Transform hitPoint = ResolveArrowHitPoint(_target, targetProfile);

            if (actorProfile?.ArrowTrailPrefab != null && _actor != null)
            {
                yield return RunTrailAndSpawn(actorProfile, hitPoint);
            }
            else
            {
                SpawnArrow(actorProfile, hitPoint.position, hitPoint.rotation);
                yield return WaitAndPlayTargetHitAnimation(actorProfile);
            }
        }

        private IEnumerator RunTrailAndSpawn(UnitVisualProfile profile, Transform hitPoint)
        {
            if (!TryInstantiateTrailLine(profile, out LineRenderer lr, out GameObject trailInstance))
            {
                SpawnArrow(profile, hitPoint.position, hitPoint.rotation);
                yield return WaitAndPlayTargetHitAnimation(profile);
                yield break;
            }

            Transform firePoint = ResolveArrowFirePoint(_actor);
            Vector3 start = firePoint != null ? firePoint.position : _actor.transform.position;
            Vector3 end = hitPoint.position;

            DrawArc(lr, start, end, profile.ArcHeight);
            lr.enabled = true;

            int lastIndex = lr.positionCount - 1;
            Vector3 lastPos = lr.GetPosition(lastIndex);
            Vector3 prevPos = lr.GetPosition(lastIndex - 1);
            Vector3 dir = (lastPos - prevPos).normalized;
            if (dir == Vector3.zero)
            {
                dir = (end - start).normalized;
            }

            Quaternion rot = dir != Vector3.zero ? Quaternion.LookRotation(dir) : hitPoint.rotation;
            SpawnArrow(profile, lastPos, rot);

            yield return WaitAndPlayTargetHitAnimation(profile);

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, profile.TrailFadeDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime * _battleSpeed;
                SetAlpha(lr, Mathf.Lerp(1f, 0f, elapsed / duration));
                yield return null;
            }

            Object.Destroy(trailInstance);
        }

        private IEnumerator WaitAndPlayTargetHitAnimation(UnitVisualProfile actorProfile)
        {
            float delay = Mathf.Max(0f, actorProfile?.ArrowHitAnimationDelay ?? 0f);
            if (delay > 0f)
            {
                float waited = 0f;
                while (waited < delay)
                {
                    waited += Time.deltaTime * _battleSpeed;
                    yield return null;
                }
            }

            PlayTargetHitAnimation();
        }

        private void PlayTargetHitAnimation()
        {
            if (_target == null || _target.IsDead || string.IsNullOrEmpty(_targetAnimTrigger))
            {
                return;
            }

            _target.EnsureAnimationController();
            _target.Anim?.SetAnimationSpeed(_battleSpeed);
            _target.Anim?.PlayGenericAnimation(_targetAnimTrigger);
        }

        private static bool TryInstantiateTrailLine(
            UnitVisualProfile profile,
            out LineRenderer lineRenderer,
            out GameObject trailInstance)
        {
            lineRenderer = null;
            trailInstance = null;

            if (profile?.ArrowTrailPrefab == null)
            {
                return false;
            }

            trailInstance = Object.Instantiate(profile.ArrowTrailPrefab);
            lineRenderer = trailInstance.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                Object.Destroy(trailInstance);
                trailInstance = null;
                return false;
            }

            lineRenderer.useWorldSpace = true;
            return true;
        }

        private static void DrawArc(LineRenderer lr, Vector3 start, Vector3 end, float arcHeight)
        {
            int count = lr.positionCount;
            if (count < 2)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector3 p = Vector3.Lerp(start, end, t);
                p.y += arcHeight * (1f - (2f * t - 1f) * (2f * t - 1f));
                lr.SetPosition(i, p);
            }
        }

        private void SetAlpha(LineRenderer lr, float alpha)
        {
            _alphaKeys[0] = new GradientAlphaKey(alpha, 0f);
            _alphaKeys[1] = new GradientAlphaKey(alpha, 1f);
            _gradient.SetKeys(_colorKeys, _alphaKeys);
            lr.colorGradient = _gradient;
        }

        private static Transform ResolveArrowFirePoint(BattleCharactor actor)
        {
            UnitSocketHolder socketHolder = actor?.GetComponentInChildren<UnitSocketHolder>();
            if (socketHolder?.LeftWeaponSocket != null)
            {
                return socketHolder.LeftWeaponSocket;
            }

            return actor != null ? actor.transform : null;
        }

        private static Transform ResolveArrowHitPoint(BattleCharactor target, UnitVisualProfile profile)
        {
            if (profile?.ArrowHitPoint != null)
            {
                return profile.ArrowHitPoint;
            }

            UnitSocketHolder socketHolder = target?.GetComponentInChildren<UnitSocketHolder>();
            if (socketHolder?.HitSocket != null)
            {
                return socketHolder.HitSocket;
            }

            return target != null ? target.transform : null;
        }

        private static void SpawnArrow(UnitVisualProfile profile, Vector3 position, Quaternion rotation)
        {
            if (profile?.TargetArrowPrefab == null)
            {
                return;
            }

            GameObject arrow = Object.Instantiate(profile.TargetArrowPrefab, position, rotation);
            Object.Destroy(arrow, profile.ArrowLifetime);
        }
    }
}
