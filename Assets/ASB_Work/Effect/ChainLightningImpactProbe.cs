using System.Collections.Generic;
        using JC.VFX;
        using UnityEngine;
        
        [DisallowMultipleComponent]
        public sealed class ChainLightningImpactProbe : MonoBehaviour
        {
            private readonly HashSet<int> _observedShockIds = new HashSet<int>();
            private readonly List<Transform> _targets = new List<Transform>();
            private int _actionInstanceId;
        
            public void Configure(int actionInstanceId, Transform primaryTarget, IReadOnlyList<Transform> chainTargets)
            {
                _actionInstanceId = actionInstanceId;
                _observedShockIds.Clear();
                _targets.Clear();
                AddTarget(primaryTarget);
                if (chainTargets != null) foreach (Transform target in chainTargets) AddTarget(target);
            }
        
            private void Update()
            {
                if (_actionInstanceId <= 0) return;
                LightningShock[] shocks = GetComponentsInChildren<LightningShock>(false);
                foreach (LightningShock shock in shocks)
                {
                    if (shock == null || !_observedShockIds.Add(shock.GetInstanceID())) continue;
                    Transform target = FindNearestTarget(shock.transform.position);
                    if (target != null) SkillImpactSignalBus.PublishImpact(new ImpactKey(_actionInstanceId, target.GetInstanceID()), shock.transform.position, new[] { target });
                }
            }
        
            private void AddTarget(Transform target)
            {
                if (target != null && !_targets.Contains(target)) _targets.Add(target);
            }
        
            private Transform FindNearestTarget(Vector3 position)
            {
                Transform nearest = null; float best = float.MaxValue;
                foreach (Transform target in _targets)
                {
                    if (target == null) continue;
                    float distance = (target.position - position).sqrMagnitude;
                    if (distance < best) { best = distance; nearest = target; }
                }
                return nearest;
            }
        }