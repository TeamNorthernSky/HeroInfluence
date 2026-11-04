using System;
using UnityEngine;

namespace JC.Environment
{
    public sealed class JcOutdoorBattleLightingProfile : ScriptableObject
    {
        public const string ResourceName = "JC_OutdoorBattleLighting";
        [Serializable]
        public sealed class Adjustment
        {
            [Min(0)] public float sunlight = 1;
            [Min(0), Tooltip("주 방향광 그림자의 기존 강도에 곱합니다.")] public float sunShadow = 1;
            // JC | 2026-11-04 | 시간대별 전장 조명을 조절하고 가로등의 중심광과 주변광을 배치에 맞게 적용합니다.
            [Min(0), Tooltip("주 방향광 이외의 방향광에 적용하는 밝기 배율입니다.")] public float secondarySun = 1;
            public Color sunlightTint = Color.white;
            [Min(0)] public float ambient = 1;
            [Min(0)] public float characterBrightness = 1;
            [Range(-5, 5)] public float exposure;
            public Color screenTint = Color.white;
        }
        public Adjustment[] segments = { new Adjustment(), new Adjustment(), new Adjustment(), new Adjustment() };

        public float SecondarySun(JcDayNightProfile source, double hours)
        {
            source.SegmentPair(hours, out int first, out int next, out float progress);
            return Mathf.Lerp(segments[first].secondarySun, segments[next].secondarySun, Mathf.SmoothStep(0, 1, progress));
        }

        public float Apply(JcDayNightProfile source, double hours, JcDayNightProfile.Segment look)
        {
            source.SegmentPair(hours, out int first, out int next, out float progress);
            float t = Mathf.SmoothStep(0, 1, progress);
            var a = segments[first];
            var b = segments[next];
            look.lightIntensity *= Mathf.Lerp(a.sunlight, b.sunlight, t);
            look.shadowMultiplier *= Mathf.Lerp(a.sunShadow, b.sunShadow, t);
            look.lightColor *= Color.Lerp(a.sunlightTint, b.sunlightTint, t);
            look.ambientMultiplier *= Mathf.Lerp(a.ambient, b.ambient, t);
            float brightness = Mathf.Lerp(a.characterBrightness, b.characterBrightness, t);
            float offset = Mathf.Lerp(a.exposure, b.exposure, t);
            var tint = Color.Lerp(a.screenTint, b.screenTint, t);
            if (offset == 0 && tint == Color.white) return brightness;
            // Fold the inherited screen weight into the values before adding battle grading.
            float weight = look.screenColorStrength;
            look.screenColorFilter = Color.Lerp(Color.white, look.screenColorFilter, weight) * tint;
            look.screenExposure = look.screenExposure * weight + offset;
            look.screenContrast *= weight;
            look.screenSaturation *= weight;
            look.screenColorStrength = 1;
            return brightness;
        }
    }
}
