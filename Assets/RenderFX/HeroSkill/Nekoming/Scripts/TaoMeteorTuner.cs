using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// Taosenaiyo 유성 프리셋 브릿지: T5_TaoMeteor 값을 매 프레임
    /// ProjectileVfx(비행) + CometShell(리본 크기) + 혜성 재질(MPB, 비파괴)에 주입.
    /// Tao_MeteorOrb 프리팹 루트에 부착. ProjectileVfx.SetFade와 같은 MPB를 공유하므로
    /// GetPropertyBlock 후 덧쓰기 — _FadeMul과 충돌 없음.
    /// </summary>
    public class TaoMeteorTuner : MonoBehaviour
    {
        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영.")]
        [SerializeField] private TaoMeteorPreset preset;
        [Tooltip("켜면 변경한 설정을 실행 중인 이 효과에 갱신합니다. 파일 저장과는 별개이며 이미 시작된 시간표는 재시전하여 확인합니다.")]
        [SerializeField] private bool livePreview = true;

        [Header("References")]
        [Tooltip("혜성의 이동·도착 효과를 담당하는 투사체 컴포넌트입니다.")]
        [SerializeField] private ProjectileVfx projectile;
        [Tooltip("혜성 외곽의 화염 셸 컴포넌트입니다. 형태·재질 조절을 적용할 대상을 연결합니다.")]
        [SerializeField] private CometShell cometShell;
        [Tooltip("혜성 리본 렌더러(TaoComet 재질)")]
        [SerializeField] private MeshRenderer cometRenderer;
        [Tooltip("혜성 머리 구형 코마 렌더러(TaoCometHead 재질). 스케일도 여기서 구동")]
        [SerializeField] private MeshRenderer cometHeadRenderer;

        private MaterialPropertyBlock _mpb;

        private static readonly int ColorHeadID = Shader.PropertyToID("_ColorHead");
        private static readonly int ColorTailID = Shader.PropertyToID("_ColorTail");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int TailStartWidthID = Shader.PropertyToID("_TailStartWidth");
        private static readonly int FillColorID = Shader.PropertyToID("_FillColor");
        private static readonly int FillIntensityID = Shader.PropertyToID("_FillIntensity");
        private static readonly int FillPowerID = Shader.PropertyToID("_FillPower");
        private static readonly int RimPowerID = Shader.PropertyToID("_RimPower");
        private static readonly int TailEndWidthID = Shader.PropertyToID("_TailEndWidth");
        private static readonly int TailTaperID = Shader.PropertyToID("_TailTaper");
        private static readonly int TailFadeID = Shader.PropertyToID("_TailFade");
        private static readonly int FlickerAmpID = Shader.PropertyToID("_FlickerAmp");
        private static readonly int FlickerSpeedID = Shader.PropertyToID("_FlickerSpeed");
        private static readonly int RimColorID = Shader.PropertyToID("_RimColor");
        private static readonly int RimIntensityID = Shader.PropertyToID("_RimIntensity");
        private static readonly int RimPosID = Shader.PropertyToID("_RimPos");
        private static readonly int RimSoftID = Shader.PropertyToID("_RimSoft");
        private static readonly int RimFadeID = Shader.PropertyToID("_RimFade");

        public void SetPreset(TaoMeteorPreset p) => preset = p;

        /// <summary>호출자(스테퍼/큐 드라이버)가 발사 시작·탄착 위치를 읽어 가는 창구(260807).</summary>
        public TaoMeteorPreset Preset => preset;

        private void Update()
        {
            if (!livePreview || preset == null) return;
            // ★라이브 따름(260807) — 위치·비행·크기·플리커는 TransformSource(Alter→Basic), 색·강도만 자기 것.
            var t = preset.TransformSource;

            if (projectile) projectile.ApplyTuning(t.worldSize, t.speed, t.arcHeight, t.trailTime);
            if (cometShell) cometShell.SetSize(t.cometLength, t.cometWidth);

            if (cometRenderer)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                cometRenderer.GetPropertyBlock(_mpb);   // _FadeMul(ProjectileVfx) 보존
                _mpb.SetColor(ColorHeadID, preset.headColor);
                _mpb.SetColor(ColorTailID, preset.tailColor);
                _mpb.SetFloat(IntensityID, preset.intensity);   // 마스터(리본 전체 곱)
                _mpb.SetFloat(TailStartWidthID, t.tailStartWidth);
                _mpb.SetFloat(TailEndWidthID, t.tailEndWidth);
                _mpb.SetFloat(TailTaperID, t.tailTaper);
                _mpb.SetFloat(TailFadeID, t.tailFade);
                _mpb.SetFloat(FlickerAmpID, t.flickerAmp);
                _mpb.SetFloat(FlickerSpeedID, t.flickerSpeed);
                _mpb.SetColor(RimColorID, preset.rimColor);
                _mpb.SetFloat(RimIntensityID, preset.rimIntensity);
                _mpb.SetFloat(RimPosID, t.rimPos);
                _mpb.SetFloat(RimSoftID, t.rimSoft);
                _mpb.SetFloat(RimFadeID, t.rimFade);
                cometRenderer.SetPropertyBlock(_mpb);
            }

            if (cometHeadRenderer)
            {
                cometHeadRenderer.transform.localScale = Vector3.one * t.headSize;
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                cometHeadRenderer.GetPropertyBlock(_mpb);   // _FadeMul 보존
                _mpb.SetColor(FillColorID, preset.headColor);
                _mpb.SetFloat(FillIntensityID, preset.headFillIntensity * preset.intensity);   // 개별 × 마스터
                _mpb.SetFloat(FillPowerID, preset.headFillPower);
                _mpb.SetColor(RimColorID, preset.rimColor);
                _mpb.SetFloat(RimIntensityID, preset.headRimIntensity * preset.intensity);     // 개별 × 마스터
                _mpb.SetFloat(RimPowerID, preset.headRimPower);
                cometHeadRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
