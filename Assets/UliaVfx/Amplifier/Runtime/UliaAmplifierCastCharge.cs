using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace UliaVfx
{
    // Shared visual-only charge cue for the two amplifier recording previews.
    public static class UliaAmplifierCastCharge
    {
        public static IEnumerator Play(Transform parent, Transform core, GameObject orbitPrefab,
            GameObject glowPrefab, float seconds, System.Action onReleased = null)
        {
            GameObject orbit = orbitPrefab ? Object.Instantiate(orbitPrefab, core.position, Quaternion.identity, parent) : null;
            GameObject glow = glowPrefab ? Object.Instantiate(glowPrefab, core.position, Quaternion.identity, parent) : null;
            if (orbit)
            {
                orbit.name = "Amplifier_Charge_Large_Fast_Orbs";
                orbit.transform.SetPositionAndRotation(core.position, Quaternion.identity);
                orbit.transform.localScale = Vector3.one * .28f;
                foreach (var loop in orbit.GetComponentsInChildren<UliaEnergyGapLoop>(true)) loop.enabled = false;
                foreach (var ps in orbit.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = ps.main;
                    main.useUnscaledTime = true;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    // Larger satellites, with 3.5x the authored orbital velocity.
                    if (ps.name == "orbs")
                    {
                        main.startSizeMultiplier *= 1.7f;
                        var velocity = ps.velocityOverLifetime;
                        velocity.orbitalZMultiplier *= 3.5f;
                    }
                    if (ps.name == "flash_electric")
                    {
                        main.startSizeMultiplier *= 1.3f;
                        var velocity = ps.velocityOverLifetime;
                        velocity.orbitalZMultiplier *= 3.5f;
                    }
                    if (ps.gameObject.activeInHierarchy) ps.Play(false);
                }
            }
            if (glow)
            {
                glow.name = "Amplifier_Core_MagicOrb01_Strong_Glow";
                glow.transform.SetPositionAndRotation(core.position, Quaternion.identity);
                glow.transform.localScale = Vector3.one * .3f;
                foreach (var vfx in glow.GetComponentsInChildren<VisualEffect>(true))
                {
                    if (vfx.HasGradient("Color"))
                    {
                        var gradient = vfx.GetGradient("Color");
                        var keys = gradient.colorKeys;
                        for (int i = 0; i < keys.Length; i++)
                        {
                            var color = keys[i].color;
                            color.r *= 2.5f; color.g *= 2.5f; color.b *= 2.5f;
                            keys[i].color = color;
                        }
                        gradient.SetKeys(keys, gradient.alphaKeys);
                        vfx.SetGradient("Color", gradient);
                    }
                    vfx.Reinit();
                    vfx.Play();
                }
            }
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, seconds));
                if (orbit)
                {
                    orbit.transform.position = core.position;
                    orbit.transform.localScale = Vector3.one * Mathf.Lerp(.28f, .45f, t);
                    orbit.transform.rotation = Quaternion.AngleAxis(elapsed * 900f, Vector3.up);
                }
                if (glow)
                {
                    glow.transform.position = core.position;
                    glow.transform.localScale = Vector3.one * Mathf.Lerp(.3f, .5f, t);
                }
                yield return null;
            }
            onReleased?.Invoke();
            if (orbit) orbit.AddComponent<UliaAmplifierCastRelease>().Begin(glow, core.position);
            else if (glow) glow.AddComponent<UliaAmplifierCastRelease>().Begin(glow, core.position);
        }
    }
}


