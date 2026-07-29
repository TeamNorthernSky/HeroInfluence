using System;
        using System.Collections.Generic;
        using UnityEngine;
        
        public readonly struct ImpactKey : IEquatable<ImpactKey>
        {
            public readonly int ActionInstanceId;
            public readonly int TargetRuntimeId;
            public readonly int HitIndex;
        
            public ImpactKey(int actionInstanceId, int targetRuntimeId = 0, int hitIndex = 0)
            {
                ActionInstanceId = actionInstanceId;
                TargetRuntimeId = targetRuntimeId;
                HitIndex = hitIndex;
            }
        
            public bool Equals(ImpactKey other) => ActionInstanceId == other.ActionInstanceId && TargetRuntimeId == other.TargetRuntimeId && HitIndex == other.HitIndex;
            public override bool Equals(object obj) => obj is ImpactKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(ActionInstanceId, TargetRuntimeId, HitIndex);
            public override string ToString() => $"{ActionInstanceId}:{TargetRuntimeId}:{HitIndex}";
        }
        
        public readonly struct SkillImpactSignal
        {
            public readonly ImpactKey Key;
            public readonly IReadOnlyList<Transform> Targets;
            public readonly Vector3 Position;
            public readonly bool Cancelled;
            public int ActionInstanceId => Key.ActionInstanceId;
        
            public SkillImpactSignal(ImpactKey key, IReadOnlyList<Transform> targets, Vector3 position, bool cancelled)
            {
                Key = key;
                Targets = targets;
                Position = position;
                Cancelled = cancelled;
            }
        }
        
        public static class SkillImpactSignalBus
        {
            private const float ClosedSignalRetentionSeconds = 30f;
            private static readonly Dictionary<ImpactKey, SkillImpactSignal> PendingSignals = new Dictionary<ImpactKey, SkillImpactSignal>();
            private static readonly Dictionary<ImpactKey, float> ClosedSignalExpiry = new Dictionary<ImpactKey, float>();
            private static readonly List<ImpactKey> ExpiredClosedKeys = new List<ImpactKey>();
        
            public static event Action<SkillImpactSignal> Raised;
        
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ResetStaticState()
            {
                PendingSignals.Clear();
                ClosedSignalExpiry.Clear();
                ExpiredClosedKeys.Clear();
                Raised = null;
            }
        
            public static void PublishImpact(ImpactKey key, Vector3 position, IReadOnlyList<Transform> targets = null) => Publish(new SkillImpactSignal(key, targets, position, false));
            public static void PublishCancelled(ImpactKey key, Vector3 position, IReadOnlyList<Transform> targets = null) => Publish(new SkillImpactSignal(key, targets, position, true));
            public static bool TryConsume(ImpactKey key, out SkillImpactSignal signal)
            {
                if (PendingSignals.TryGetValue(key, out signal)) { PendingSignals.Remove(key); return true; }
                return false;
            }
            public static void Clear(ImpactKey key)
            {
                if (key.ActionInstanceId <= 0) return;
                PendingSignals.Remove(key);
                PruneClosedSignals();
                ClosedSignalExpiry[key] = Time.realtimeSinceStartup + ClosedSignalRetentionSeconds;
            }
        
            // Compatibility overloads: an untargeted impact means the whole cast/area impact (target=0, hit=0).
            public static void Publish(int actionInstanceId, IReadOnlyList<Transform> targets, Vector3 position) => PublishImpact(new ImpactKey(actionInstanceId), position, targets);
            public static void PublishCancelled(int actionInstanceId, IReadOnlyList<Transform> targets, Vector3 position) => PublishCancelled(new ImpactKey(actionInstanceId), position, targets);
            public static bool TryConsume(int actionInstanceId, out SkillImpactSignal signal) => TryConsume(new ImpactKey(actionInstanceId), out signal);
            public static void Clear(int actionInstanceId) => Clear(new ImpactKey(actionInstanceId));
        
            private static void Publish(SkillImpactSignal signal)
            {
                if (signal.Key.ActionInstanceId <= 0) return;
                PruneClosedSignals();
                if (ClosedSignalExpiry.ContainsKey(signal.Key)) return;
                PendingSignals[signal.Key] = signal;
                Raised?.Invoke(signal);
            }
        
            private static void PruneClosedSignals()
            {
                if (ClosedSignalExpiry.Count == 0) return;
                float now = Time.realtimeSinceStartup;
                ExpiredClosedKeys.Clear();
                foreach (KeyValuePair<ImpactKey, float> pair in ClosedSignalExpiry) if (pair.Value <= now) ExpiredClosedKeys.Add(pair.Key);
                foreach (ImpactKey key in ExpiredClosedKeys) ClosedSignalExpiry.Remove(key);
            }
        }