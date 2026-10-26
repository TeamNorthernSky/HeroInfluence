using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UliaVfx
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class UliaEnergyGapLoop : MonoBehaviour
    {
        [SerializeField, Min(0f), InspectorName("반복 사이 대기 시간 (초)")]
        private float gapSeconds = 1f;

        private ParticleSystem[] systems;
        private readonly List<ParticleSystem> playing = new List<ParticleSystem>();

        private void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void OnEnable()
        {
            StartCoroutine(Replay());
        }

        private IEnumerator Replay()
        {
            while (true)
            {
                playing.Clear();
                foreach (ParticleSystem system in systems)
                {
                    if (system == null || !system.gameObject.activeInHierarchy)
                        continue;

                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.Play(false);
                    playing.Add(system);
                }

                // Allow emission to advance before checking delayed systems.
                yield return null;
                while (HasLivingParticles())
                    yield return null;

                yield return new WaitForSeconds(Mathf.Max(0f, gapSeconds));
            }
        }

        private bool HasLivingParticles()
        {
            foreach (ParticleSystem system in playing)
            {
                if (system != null && system.gameObject.activeInHierarchy && system.IsAlive(false))
                    return true;
            }
            return false;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (systems == null)
                return;

            foreach (ParticleSystem system in systems)
            {
                if (system != null)
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            playing.Clear();
        }
    }
}
