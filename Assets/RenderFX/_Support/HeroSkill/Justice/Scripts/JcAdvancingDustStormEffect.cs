using System.Collections;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>타격한 줄의 전열과 후열 사이에서 후열 방향으로 이동하는 저스티스 돌풍.</summary>
    public sealed class JcAdvancingDustStormEffect : MonoBehaviour, ISkillEffectBehaviour, ISelfManagedEffectLifetime
    {
        [SerializeField, Min(0f)] private float travelDistance = 1.8f;
        [SerializeField, Min(0.01f)] private float travelSeconds = 0.75f;

        public Vector3 Origin { get; private set; }
        public Vector3 TravelDirection { get; private set; }

        public void Play(SkillEffectContext context)
        {
            Vector3 target = context.PrimaryTarget != null
                ? context.PrimaryTarget.transform.position : context.TargetPosition;
            Vector3 forward = target - context.Caster.transform.position;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : context.Caster.transform.forward;

            Origin = target;
            var grid = ASB.Work.BattleGrid.BattleGridManager.Instance;
            var targetCell = context.PrimaryTarget != null ? context.PrimaryTarget.OccupiedCell : null;
            if (grid != null)
            {
                // 타격으로 점유가 해제돼도 선택한 줄은 원래 대상 위치에서 찾는다.
                if (targetCell == null)
                {
                    float nearest = float.PositiveInfinity;
                    foreach (var cell in grid.AllCells)
                    {
                        float distance = (cell.transform.position - target).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; targetCell = cell; }
                    }
                }
                if (targetCell != null)
                {
                    bool playerSide = targetCell.Coords.x < 2;
                    int lane = targetCell.Coords.y;
                    if (grid.TryGetCell(new Vector2Int(playerSide ? 1 : 2, lane), out var front)
                        && grid.TryGetCell(new Vector2Int(playerSide ? 0 : 3, lane), out var back))
                    {
                        Origin = (front.transform.position + back.transform.position) * 0.5f;
                        Origin = new Vector3(Origin.x, target.y, Origin.z);
                        forward = back.transform.position - front.transform.position;
                        forward.y = 0f;
                        forward.Normalize();
                    }
                }
            }
            TravelDirection = forward;
            transform.SetPositionAndRotation(Origin, Quaternion.LookRotation(TravelDirection));
            var particles = GetComponentsInChildren<ParticleSystem>(true);
            float speed = Mathf.Max(0.01f, context.PlaybackSpeed);
            foreach (var particle in particles)
            {
                var main = particle.main;
                main.simulationSpeed = speed;
                particle.Clear(true);
                particle.Play(true);
            }
            StartCoroutine(Advance(particles, speed));
        }

        private IEnumerator Advance(ParticleSystem[] particles, float speed)
        {
            float elapsed = 0f;
            while (elapsed < travelSeconds)
            {
                elapsed += Time.deltaTime * speed;
                transform.position = Origin + TravelDirection * (travelDistance * Mathf.Clamp01(elapsed / travelSeconds));
                yield return null;
            }
            foreach (var particle in particles)
                if (particle != null) particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            float remaining = 2f;
            while (remaining > 0f && System.Array.Exists(particles, p => p != null && p.IsAlive(false)))
            {
                remaining -= Time.deltaTime * speed;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
