using System.Collections;
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class HitStopPlayModeTests
{
    [UnityTest]
    public IEnumerator ProjectileImpactParticles_SlowThenRestoreInRealTime()
    {
        var visualObject = new GameObject("HitStopVisualTest");
        var impactObject = new GameObject("HitStopProjectileImpactTest");
        try
        {
            Type visualType = FindType("BattleVisualDirector");
            Type settingsType = FindType("HitStopSettings");
            var visual = visualObject.AddComponent(visualType);
            var particles = impactObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.simulationSpeed = 1f;

            object settings = Activator.CreateInstance(settingsType);
            settingsType.GetField("Enabled").SetValue(settings, true);
            settingsType.GetField("DurationSeconds").SetValue(settings, 0.06f);
            settingsType.GetField("CharacterSpeed").SetValue(settings, 0.05f);
            settingsType.GetField("EffectSpeed").SetValue(settings, 0.25f);
            visualType.GetMethod("RegisterProjectileImpact").Invoke(visual, new object[] { impactObject });
            visualType.GetMethod("RequestHitStop").Invoke(visual, new[] { null, null, settings });
            Assert.That(particles.main.simulationSpeed, Is.EqualTo(0.25f).Within(0.001f));

            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(particles.main.simulationSpeed, Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.Destroy(impactObject);
            UnityEngine.Object.Destroy(visualObject);
        }
    }

    private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name))
        .First(type => type != null);
}
