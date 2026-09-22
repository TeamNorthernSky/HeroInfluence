using UnityEngine;
namespace JC.VFX
{
    /// <summary>분리 부품이 자신의 프리셋만 읽습니다. 프리뷰와 전투가 동일하게 사용합니다.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class JcLuminaPartPresetBinder : MonoBehaviour
    {
        [Tooltip("이 부품의 배치·시간·색을 제어하는 공용 프리셋입니다. 같은 참조를 쓰는 차징·비행·착탄에 함께 반영됩니다.")]
        public ScriptableObject preset;
        private void Awake() => ApplyNow();
        public void ApplyNow()
        {
            if (preset == null) return;
            if (preset is SolarPrismPreset solar)
            {
                var fx=GetComponent<SolarPrismVfx>(); if(fx!=null) {int count=fx.UnitCount;ApplyVfx(fx,solar);if(fx.IsPlaying)fx.UnitCount=count;}
                foreach(var im in GetComponentsInChildren<FlareBombImpact>(true)) ApplySolarImpact(im,solar);
            }
            else if(preset is PrismExplosionPreset prism)
            {
                var fx=GetComponent<PrismExplosionVfx>(); if(fx!=null) ApplyVfx(fx,prism);
                foreach(var im in GetComponentsInChildren<FlareBombImpact>(true)) ApplyPrismImpact(im,prism);
            }
            else if(preset is ChainLightningPreset chain)
            {
                var fx=GetComponent<ChainLightningVfx>();if(fx!=null) ApplyTimings(fx,chain);
            }
            else if(preset is FlareImpactPreset flare)
                foreach(var im in GetComponentsInChildren<FlareBombImpact>(true))
                {ApplySizes(im.transform,flare);im.BurstDuration=flare.burstDuration;im.RingDuration=flare.ringDuration;im.RingGroundOffset=flare.ringGroundOffset;}
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                // 다른 독립 부품이 하위에 생성된 경우 그 부품의 프리셋이 정본입니다.
                if(renderer.GetComponentInParent<JcLuminaPartPresetBinder>()!=this) continue;
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                System.Action<string,float> f=(k,v)=>block.SetFloat(k,v);
                System.Action<string,Color> c=(k,v)=>block.SetColor(k,v);
                string n=renderer.name;
                if(preset is SolarPrismPreset a)
                {
                    if(n.Contains("Crystal")) FillCrystal(f,c,a);
                    else if(n.Contains("ImpactBurst")) FillImpactBurst(f,c,a);
                    else if(n.Contains("ImpactRing")) FillImpactRing(f,c,a);
                    else if(n.Contains("Flare")) FillFlare(f,c,a);
                    else if(n.Contains("Ground")) FillGround(f,c,a);
                }
                else if(preset is PrismExplosionPreset b)
                {
                    if(n.Contains("Crystal")) FillCrystal(f,c,b);
                    else if(n.Contains("ImpactBurst")) FillImpactBurst(f,c,b);
                    else if(n.Contains("ImpactRing")) FillImpactRing(f,c,b);
                    else if(n.Contains("Flare")) FillFlare(f,c,b);
                    else if(n.Contains("Ground")) FillGround(f,c,b);
                }
                else if(preset is ChainLightningPreset d)
                {if(n.Contains("Bolt")) FillBolt(f,c,d);else if(n.Contains("Bg")) FillBg(f,c,d);else FillShock(f,c,d);if(n.Contains("Muzzle")) f("_CoreGlow",d.muzzleCoreGlow);}
                else if(preset is FlareImpactPreset e)
                {if(n.Contains("Ring")) FillRing(f,c,e);else FillBurst(f,c,e);}
                renderer.SetPropertyBlock(block);
            }
        }
        static void ApplySolarImpact(FlareBombImpact im,SolarPrismPreset p)
        {
            im.BurstDuration=p.impactBurstDuration;im.RingDuration=p.impactRingDuration;im.RingGroundOffset=p.targetHeight;
            Scale(im.transform,"ImpactBurst",p.impactSize,p.impactSize);Scale(im.transform,"ImpactRing",p.impactRingSize,p.impactRingSize);
        }
        static void ApplyPrismImpact(FlareBombImpact im,PrismExplosionPreset p)
        {
            im.BurstDuration=p.impactBurstDuration;im.RingDuration=p.impactRingDuration;im.RingGroundOffset=p.targetHeight;
            Scale(im.transform,"ImpactBurstH",p.impactSizeH,p.impactSizeH*.6f);Scale(im.transform,"ImpactBurstV",p.impactSizeV*.5f,p.impactSizeV);Scale(im.transform,"ImpactRing",p.impactRingSize,p.impactRingSize);
        }
        static void Scale(Transform root,string name,float x,float y){var t=root.Find(name);if(t!=null)t.localScale=new Vector3(x,y,1);}
        static void FillCrystal(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_ColorBody", p.bodyColor);
            setC("_ColorRim", p.rimColor);
            setF("_Emission", p.crystalEmission);
            setF("_BodyAlpha", p.bodyAlpha);
            setF("_RimAlpha", p.rimAlpha);
            setF("_FresnelPow", p.fresnelPow);
            setF("_FacetAmount", p.facetAmount);
        }

        static void FillFlare(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_Color", p.flareColor);
            setF("_Emission", p.flareEmission);
            setF("_SpikeNarrow", p.spikeNarrow);
            setF("_SpikeFall", p.spikeFall);
            setF("_DiagRatio", p.diagRatio);
            setF("_CoreSize", p.flareCoreSize);
        }

        static void FillGround(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_Color", p.groundColor);
            setF("_Emission", p.groundEmission);
            setF("_Falloff", p.groundFalloff);
        }

        static void FillImpactBurst(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_ColorHot", p.impactHotColor);
            setC("_ColorGold", p.impactGlowColor);
            setF("_Emission", p.impactEmission);
        }

        static void FillImpactRing(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_ColorRing", p.impactRingColor);
        }

        static void ApplyVfx(SolarPrismVfx vfx, SolarPrismPreset p)
        {
            vfx.UnitCount = p.unitCount;
            vfx.Spacing = p.spacing;
            vfx.ForwardOffset = p.forwardOffset;
            vfx.HoverHeight = p.hoverHeight;
            vfx.PrismScale = p.prismScale;
            vfx.HoverAmp = p.hoverAmp;
            vfx.HoverFreq = p.hoverFreq;
            vfx.SpinSpeed = p.spinSpeed;
            vfx.SpinJitter = p.spinJitter;
            vfx.SummonTime = p.summonTime;
            vfx.SummonStagger = p.summonStagger;
            vfx.BeltRadius = p.beltRadius;
            vfx.BeltLocalY = p.beltLocalY;
            vfx.BeltAngleOffset = p.beltAngleOffset;
            vfx.FlareThreshold = p.flareThreshold;
            vfx.FlareSharp = p.flareSharp;
            vfx.FlareSize = p.flareSize;
            vfx.GroundSize = p.groundSize;
            vfx.GroundBase = p.groundBase;
            vfx.GroundPulse = p.groundPulse;
            vfx.LaunchSpeed = p.launchSpeed;
            vfx.LaunchStagger = p.launchStagger;
            vfx.LaunchSpinMul = p.launchSpinMul;
            vfx.LaunchArc = p.launchArc;
            vfx.TargetHeight = p.targetHeight;
            vfx.AimBlend = p.aimBlend;
            foreach (var tr in vfx.GetComponentsInChildren<TrailRenderer>(true))
            {
                tr.time = p.launchTrailTime;
                tr.widthMultiplier = p.launchTrailWidth;
            }
            foreach (var im in vfx.GetComponentsInChildren<FlareBombImpact>(true))
            {
                im.BurstDuration = p.impactBurstDuration;
                im.RingDuration = p.impactRingDuration;
                im.RingGroundOffset = p.targetHeight;
                var burst = im.transform.Find("ImpactBurst");
                if (burst != null) burst.localScale = new Vector3(p.impactSize, p.impactSize, 1f);
                var ring = im.transform.Find("ImpactRing");
                if (ring != null) ring.localScale = new Vector3(p.impactRingSize, p.impactRingSize, 1f);
            }
        }

        static void FillCrystal(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_ColorBody", p.bodyColor);
            setC("_ColorRim", p.rimColor);
            setF("_Emission", p.crystalEmission);
            setF("_BodyAlpha", p.bodyAlpha);
            setF("_RimAlpha", p.rimAlpha);
            setF("_FresnelPow", p.fresnelPow);
            setF("_FacetAmount", p.facetAmount);
        }

        static void FillFlare(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_Color", p.flareColor);
            setF("_Emission", p.flareEmission);
        }

        static void FillGround(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_Color", p.groundColor);
            setF("_Emission", p.groundEmission);
        }

        static void FillImpactBurst(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_ColorHot", p.impactHotColor);
            setC("_ColorGold", p.impactGlowColor);
            setF("_Emission", p.impactEmission);
        }

        static void FillImpactRing(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_ColorRing", p.impactRingColor);
        }

        static void ApplyVfx(PrismExplosionVfx vfx, PrismExplosionPreset p)
        {
            vfx.ForwardOffset = p.forwardOffset;
            vfx.HoverHeight = p.hoverHeight;
            vfx.PrismScale = p.prismScale;
            vfx.HoverAmp = p.hoverAmp;
            vfx.HoverFreq = p.hoverFreq;
            vfx.SpinSpeed = p.spinSpeed;
            vfx.SummonTime = p.summonTime;
            vfx.FlareThreshold = p.flareThreshold;
            vfx.FlareSharp = p.flareSharp;
            vfx.FlareSize = p.flareSize;
            vfx.GroundSize = p.groundSize;
            vfx.GroundBase = p.groundBase;
            vfx.GroundPulse = p.groundPulse;
            vfx.RiseTime = p.riseTime;
            vfx.RiseHeight = p.riseHeight;
            vfx.TremorAmp = p.tremorAmp;
            vfx.TremorFreq = p.tremorFreq;
            vfx.AimSpinMul = p.aimSpinMul;
            vfx.LaunchSpeed = p.launchSpeed;
            vfx.LaunchSpinMul = p.launchSpinMul;
            vfx.LaunchArc = p.launchArc;
            vfx.TargetHeight = p.targetHeight;
            vfx.SpiralRadius = p.spiralRadius;
            vfx.SpiralRate = p.spiralRate;
            vfx.SmokeLinger = p.smokeLinger;

            var mainTrail = vfx.transform.Find("PrismUnit/FlightTrail");
            if (mainTrail != null)
            {
                var tr = mainTrail.GetComponent<TrailRenderer>();
                if (tr != null) { tr.time = p.trailTime; tr.widthMultiplier = p.trailWidth; }
            }
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!ps.gameObject.name.Contains("Embers")) continue;
                var em = ps.emission;
                em.rateOverTime = p.emberRate;
                var main = ps.main;
                main.startSize = new ParticleSystem.MinMaxCurve(p.emberSize * 0.6f, p.emberSize * 1.3f);
            }
            foreach (var im in vfx.GetComponentsInChildren<FlareBombImpact>(true))
            {
                im.BurstDuration = p.impactBurstDuration;
                im.RingDuration = p.impactRingDuration;
                im.RingGroundOffset = p.targetHeight;
                var bh = im.transform.Find("ImpactBurstH");
                if (bh != null) bh.localScale = new Vector3(p.impactSizeH, p.impactSizeH * 0.6f, 1f);
                var bv = im.transform.Find("ImpactBurstV");
                if (bv != null) bv.localScale = new Vector3(p.impactSizeV * 0.5f, p.impactSizeV, 1f);
                var ring = im.transform.Find("ImpactRing");
                if (ring != null) ring.localScale = new Vector3(p.impactRingSize, p.impactRingSize, 1f);
            }
        }

        static void FillBolt(System.Action<string, float> setF, System.Action<string, Color> setC,
                             ChainLightningPreset p)
        {
            setC("_ColorCore", p.boltCoreColor);
            setC("_ColorGlow", p.boltGlowColor);
            setF("_Emission", p.boltEmission);
            setF("_Width", p.boltWidth);
            setF("_SegCount", p.segCount);
            setF("_JitterAmp", p.jitterAmp);
            setF("_MicroJag", p.microJag);
            setF("_MicroFreq", p.microFreq);
            setF("_EndPin", p.endPin);
            setF("_CoreWidth", p.coreWidth);
            setF("_GlowWidth", p.glowWidth);
            setF("_HeadSize", p.headSize);
            setF("_HeadBoost", p.headBoost);
            setF("_BranchCount", p.branchCount);
            setF("_BranchLen", p.branchLen);
            setF("_BranchSlope", p.branchSlope);
            setF("_BranchWidthMul", p.branchWidthMul);
        }

        static void FillShock(System.Action<string, float> setF, System.Action<string, Color> setC,
                              ChainLightningPreset p)
        {
            setC("_ColorCore", p.shockCoreColor);
            setC("_ColorGlow", p.shockGlowColor);
            setF("_Emission", p.shockEmission);
            setF("_Radius", p.shockRadius);
            setF("_EllipseK", p.shockEllipseK);
            setF("_RadJitter", p.radJitter);
            setF("_ArcCount", p.arcCount);
            setF("_Density", p.arcDensity);
            setF("_ArcWidth", p.arcWidth);
            setF("_HighlightRatio", p.arcHighlightRatio);
            setF("_WobbleAmp", p.wobbleAmp);
            setF("_WobbleFreq", p.wobbleFreq);
            setF("_GlowAmt", p.glowAmt);
            setF("_CoreGlow", p.shockCoreGlow);
        }

        static void FillBg(System.Action<string, float> setF, System.Action<string, Color> setC,
                           ChainLightningPreset p)
        {
            setC("_ColorBody", p.bgBodyColor);
            setC("_ColorCenter", p.bgCenterColor);
            setF("_CenterSize", p.bgCenterSize);
            setF("_SpikeCount", p.bgSpikeCount);
            setF("_SpikeLen", p.bgSpikeLen);
            setF("_LenJitter", p.bgLenJitter);
            setF("_SpikeWidth", p.bgSpikeWidth);
            setF("_TaperSharp", p.bgTaperSharp);
            setF("_MaxAlpha", p.bgOpacity);
        }

        static void ApplyTimings(ChainLightningVfx vfx, ChainLightningPreset p)
        {
            vfx.MuzzleTime = p.muzzleTime;
            vfx.MuzzleSize = p.muzzleSize;
            vfx.MuzzleOffset = p.muzzleOffset;
            vfx.DrawTime = p.drawTime;
            vfx.HoldTime = p.holdTime;
            vfx.FadeTime = p.fadeTime;
            vfx.FlickerRate = p.flickerRate;
            vfx.ChainDelay = p.chainDelay;
            vfx.ChainDrawTime = p.chainDrawTime;
            vfx.ChainHoldTime = p.chainHoldTime;
            vfx.ShockDuration = p.shockDuration;
            vfx.ShockFadeTime = p.shockFadeTime;
            vfx.ShockSize = p.shockSize;
            vfx.TargetHeight = p.targetHeight;
        }

        static void FillBurst(System.Action<string, float> setF, System.Action<string, Color> setC,
                              FlareImpactPreset p)
        {
            setC("_ColorHot", p.hotColor);
            setC("_ColorGold", p.goldColor);
            setF("_Emission", p.burstEmission);
            setF("_HotCore", p.hotCore);
            setF("_GrowFrac", p.growFrac);
            setF("_FadeStart", p.fadeStart);
            setF("_SpikeCount", p.spikeCount);
            setF("_SpikeLen", p.spikeLen);
            setF("_LenJitter", p.lenJitter);
            setF("_SpikeWidth", p.spikeWidth);
            setF("_WidthJitter", p.widthJitter);
            setF("_TaperSharp", p.taperSharp);
            setF("_SubCount", p.subCount);
            setF("_SubLen", p.subLen);
            setF("_SubWidth", p.subWidth);
            setF("_PosJitter", p.posJitter);
            setF("_Seed", p.seed);
            setF("_FlashSize", p.flashSize);
            setF("_FlashIntensity", p.flashIntensity);
            setF("_FlashFade", p.flashFade);
        }

        static void FillRing(System.Action<string, float> setF, System.Action<string, Color> setC,
                             FlareImpactPreset p)
        {
            setC("_ColorRing", p.ringColor);
            setF("_Emission", p.ringEmission);
            setF("_RingMaxR", p.ringMaxR);
            setF("_RingWidth", p.ringWidth);
            setF("_InnerGlow", p.innerGlow);
            setF("_FadeStart", p.ringFadeStart);
        }

        static void ApplySizes(Transform impactRoot, FlareImpactPreset p)
        {
            var g = impactRoot.Find("BurstGround");
            if (g != null) g.localScale = new Vector3(p.groundWidth, p.groundHeight, 1f);
            var s = impactRoot.Find("BurstSpike");
            if (s != null) s.localScale = new Vector3(p.spikeWidth2, p.spikeHeight, 1f);
            var r = impactRoot.Find("GroundRing");
            if (r != null) r.localScale = new Vector3(p.ringSize, p.ringSize, 1f);
        }
    }
}
