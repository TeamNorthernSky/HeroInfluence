using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 궁수 전용: 포물선 라인을 즉시 그린 뒤 페이드아웃하고, 끝점 기울기에 맞춰 화살을 꽂습니다.
    /// WaitHitAction과 ResolveHitAction 사이에 삽입됩니다.
    /// </summary>
    public class ArrowImpactAction : BattleSequenceAction
    {
        private readonly BattleCharactor _actor;
        private readonly BattleCharactor _target;
        private readonly MonoBehaviour _host;

        private readonly Gradient _gradient = new Gradient();
        private readonly GradientColorKey[] _colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        };
        private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[2];

        public ArrowImpactAction(BattleCharactor actor, BattleCharactor target, MonoBehaviour host)
        {
            _actor = actor;
            _target = target;
            _host = host;
        }

        public override IEnumerator ExecuteRoutine()
        {
            if (_target == null || _target.IsDead)
                yield break;

            UnitVisualProfile actorProfile = _actor?.GetComponent<UnitVisualProfile>();
            UnitVisualProfile targetProfile = _target.GetComponent<UnitVisualProfile>();

            actorProfile?.HoldArrow?.SetActive(false);

            Transform hitPoint = targetProfile?.ArrowHitPoint ?? _target.transform;

            // 라인 렌더러가 있으면 포물선 궤적 연출 (비동기)
            if (actorProfile?.ArrowTrailLine != null && _actor != null)
            {
                _host.StartCoroutine(RunTrailAndSpawn(actorProfile, hitPoint));
            }
            else
            {
                SpawnArrow(actorProfile, hitPoint.position, hitPoint.rotation);
            }

            // 데미지 딜레이 대기 후 ResolveHitAction으로 넘어감
            float damageDelay = actorProfile?.DamageDelay ?? 0f;
            if (damageDelay > 0f)
                yield return new WaitForSeconds(damageDelay);
            else
                yield break;
        }

        private IEnumerator RunTrailAndSpawn(UnitVisualProfile profile, Transform hitPoint)
        {
            LineRenderer lr = profile.ArrowTrailLine;
            Vector3 start = _actor.transform.position;
            Vector3 end   = hitPoint.position;

            // 포물선 그리기
            DrawArc(lr, start, end, profile.ArcHeight);
            lr.enabled = true;

            // 페이드아웃
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, profile.TrailFadeDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(lr, Mathf.Lerp(1f, 0f, elapsed / duration));
                yield return null;
            }

            lr.enabled = false;

            // 끝점 기울기로 화살 회전 계산
            Vector3 lastPos = lr.GetPosition(lr.positionCount - 1);
            Vector3 prevPos = lr.GetPosition(lr.positionCount - 2);
            Vector3 dir     = (lastPos - prevPos).normalized;
            Quaternion rot  = dir != Vector3.zero ? Quaternion.LookRotation(dir) : hitPoint.rotation;

            SpawnArrow(profile, lastPos, rot);
        }

        private static void DrawArc(LineRenderer lr, Vector3 start, Vector3 end, float arcHeight)
        {
            int count = lr.positionCount;
            if (count < 2) return;

            for (int i = 0; i < count; i++)
            {
                float t   = i / (float)(count - 1);
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

        private static void SpawnArrow(UnitVisualProfile profile, Vector3 position, Quaternion rotation)
        {
            if (profile?.TargetArrowPrefab == null) return;
            GameObject arrow = Object.Instantiate(profile.TargetArrowPrefab, position, rotation);
            Object.Destroy(arrow, profile.ArrowLifetime);
        }
    }
}
