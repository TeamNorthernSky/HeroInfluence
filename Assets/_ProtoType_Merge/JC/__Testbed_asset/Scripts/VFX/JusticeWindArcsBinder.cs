using UnityEngine;

namespace JC.VFX
{
    /// <summary>바람무늬 원호 프리셋 → 이펙트 컴포넌트 반영 (정적 적용 로직).</summary>
    public static class JusticeWindArcsRuntime
    {
        public static void ApplyWindArcs(GameObject root, JusticeWindArcsPreset p, Material ribbonMat = null)
        {
            if (root == null || p == null) return;

            var fx = root.GetComponent<Seam.JcWindArcsEffect>();
            if (fx == null) return;

            fx.ColorA = p.colorA;
            fx.ColorB = p.colorB;
            fx.Emission = p.emission;
            fx.Radius = p.radius;
            fx.RadiusNoise = p.radiusNoise;
            fx.OrbitHeight = p.orbitHeight;
            fx.HeightNoise = p.heightNoise;
            fx.ArcSpanMin = p.arcSpanMin;
            fx.ArcSpanMax = p.arcSpanMax;
            fx.TiltMax = p.tiltMax;
            fx.Bidirectional = p.bidirectional;
            fx.SweepDurMin = p.sweepDurMin;
            fx.SweepDurMax = p.sweepDurMax;
            fx.SpawnIntMin = p.spawnIntMin;
            fx.SpawnIntMax = p.spawnIntMax;
            fx.MaxArcs = p.maxArcs;
            fx.SpawnWindow = p.spawnWindow;
            fx.WidthMin = p.widthMin;
            fx.WidthMax = p.widthMax;
            fx.TrailTimeMin = p.trailTimeMin;
            fx.TrailTimeMax = p.trailTimeMax;
            fx.AlphaMin = p.alphaMin;
            fx.AlphaMax = p.alphaMax;
            fx.BrightRatio = p.brightRatio;
            fx.BrightWidthMul = p.brightWidthMul;
            fx.BrightAlphaMul = p.brightAlphaMul;
            fx.SpawnOffset = p.spawnOffset;
            if (ribbonMat != null) fx.RibbonMaterial = ribbonMat;
        }
    }

    /// <summary>
    /// 이펙트 프리팹에 붙여 두면 스폰될 때마다 프리셋을 읽어 반영한다 —
    /// 프리셋을 만지면 "적용" 없이도 다음 재생부터 곧바로 반영.
    /// 색 변종은 JusticeTrailPresetBinder.UseAlternate 정적 플래그를 공유한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JusticeWindArcsBinder : MonoBehaviour
    {
        [Tooltip("읽어올 프리셋(기본 버전). 비우면 아무것도 하지 않는다.")]
        [SerializeField] private JusticeWindArcsPreset preset;

        [Tooltip("+스킬 버전 프리셋. 비어 있으면 기본을 그대로 쓴다.")]
        [SerializeField] private JusticeWindArcsPreset presetAlt;

        [Tooltip("리본 재질. 프리셋 targets와 같은 것을 가리킨다.")]
        [SerializeField] private Material ribbonMaterial;

        public JusticeWindArcsPreset Preset { get => preset; set => preset = value; }
        public JusticeWindArcsPreset PresetAlt { get => presetAlt; set => presetAlt = value; }

        [Tooltip("★켜면 이 인스턴스는 무조건 알트(강화·적색) 프리셋을 쓴다. 강화 스킬 전용 프리팹용.")]
        [SerializeField] private bool forceAlternate;

        public bool ForceAlternate { get => forceAlternate; set => forceAlternate = value; }

        public JusticeWindArcsPreset ActivePreset =>
            ((forceAlternate || JusticeTrailPresetBinder.UseAlternate) && presetAlt != null) ? presetAlt : preset;

        private void Awake() => ApplyNow();

        public void ApplyNow() => JusticeWindArcsRuntime.ApplyWindArcs(gameObject, ActivePreset, ribbonMaterial);
    }
}
