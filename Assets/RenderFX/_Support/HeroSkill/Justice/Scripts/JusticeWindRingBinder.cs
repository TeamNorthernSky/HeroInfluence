using UnityEngine;

namespace JC.VFX
{
    /// <summary>바람무늬 방사광 프리셋 → 재질·컴포넌트 반영 (정적 적용 로직).</summary>
    public static class JusticeWindRingRuntime
    {
        /// <summary>
        /// 프리셋을 재질(공용 미학)과 이펙트 컴포넌트(지오메트리·수명)에 나눠 밀어 넣는다.
        /// 렌더러별 차이(모드·밴드·회전 배속)는 컴포넌트가 재생 시 MPB로 얹는다.
        /// </summary>
        public static void ApplyWindRing(GameObject root, JusticeWindRingPreset p, Material mat = null)
        {
            if (root == null || p == null) return;

            // [굽기 전용] 룩 파라미터는 에디터 「적용」에서만 재질 에셋에 기록.
            // 라이브 MPB 경로가 없는 이유: 렌더러 3장이 JcWindRingEffect.Play→Build에서 생성되므로
            // 스폰(Awake) 시점엔 대상이 없다. 룩은 구운 재질에서 읽는다(_Study·미결선 습작이라 수용).
            if (JusticeTrailPresetRuntime.WriteSharedMaterials && mat != null)
            {
                mat.SetColor("_ColorA", p.colorA);
                mat.SetColor("_ColorB", p.colorB);
                mat.SetFloat("_Emission", p.emission);
                mat.SetFloat("_ColorNoiseTile", p.colorNoiseTile);
                mat.SetFloat("_NoiseScale", p.noiseScale);
                mat.SetFloat("_CrossScale", p.crossScale);
                mat.SetFloat("_NoiseAmp", p.noiseAmp);
                mat.SetFloat("_SpinSpeed", p.spinSpeed);
                mat.SetFloat("_DriftSpeed", p.driftSpeed);
                mat.SetFloat("_AlphaMax", p.alphaMax);
            }

            var fx = root.GetComponent<Seam.JcWindRingEffect>();
            if (fx != null)
            {
                fx.FloorInner = p.floorInner;
                fx.FloorOuter = p.floorOuter;
                fx.WallRadius = p.wallRadius;
                fx.WallThickness = p.wallThickness;
                fx.WallHeight = p.wallHeight;
                fx.GroundLift = p.groundLift;
                fx.BandSoft = p.bandSoft;
                fx.TearAmount = p.tearAmount;
                fx.InnerSpinMul = p.innerSpinMul;
                fx.FloorAlphaMul = p.floorAlphaMul;
                fx.FadeIn = p.fadeIn;
                fx.Hold = p.hold;
                fx.FadeOut = p.fadeOut;
                fx.SpawnOffset = p.spawnOffset;
                fx.CameraTilt = p.cameraTilt;
                fx.AutoCenter = p.autoCenter;
                fx.CenterShift = p.centerShift;
                if (mat != null) fx.WindMaterial = mat;
            }
        }
    }

    /// <summary>
    /// 이펙트 프리팹에 붙여 두면 스폰될 때마다 프리셋을 읽어 반영한다 —
    /// 프리셋을 만지면 "적용" 없이도 다음 재생부터 곧바로 반영(궤적 프리셋과 같은 운용).
    /// 색 변종은 JusticeTrailPresetBinder.UseAlternate 정적 플래그를 공유한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JusticeWindRingBinder : MonoBehaviour
    {
        [Tooltip("읽어올 프리셋(기본 버전). 비우면 아무것도 하지 않는다.")]
        [SerializeField] private JusticeWindRingPreset preset;

        [Tooltip("+스킬 버전 프리셋. 비어 있으면 기본을 그대로 쓴다.")]
        [SerializeField] private JusticeWindRingPreset presetAlt;

        [Tooltip("바람 고리 재질. 프리셋 targets와 같은 것을 가리킨다.")]
        [SerializeField] private Material windMaterial;

        public JusticeWindRingPreset Preset { get => preset; set => preset = value; }
        public JusticeWindRingPreset PresetAlt { get => presetAlt; set => presetAlt = value; }

        public JusticeWindRingPreset ActivePreset =>
            (JusticeTrailPresetBinder.UseAlternate && presetAlt != null) ? presetAlt : preset;

        private void Awake() => ApplyNow();

        public void ApplyNow() => JusticeWindRingRuntime.ApplyWindRing(gameObject, ActivePreset, windMaterial);
    }
}
