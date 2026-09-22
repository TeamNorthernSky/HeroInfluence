using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace JC.VFX
{
    /// <summary>원본 루미나 Inspector의 적용/캡처 동작을 현재 부품의 실제 참조로 연결합니다.</summary>
    public static class JcLuminaPresetEditorBridge
    {
        private static bool Supported(Object p) => p is FlareOrbPresetBase || p is FlareOrbSpritePreset || p is FlareImpactPreset || p is SolarPrismPreset || p is PrismExplosionPreset || p is ChainLightningPreset;
        public static bool IsPartPreset(Object p) => Supported(p)&&JcPresetPartTargets.Roots(p).Length>0;
        public static bool TryLivePush(Object p)
        {
            if(!IsPartPreset(p))return false;
            foreach(var b in Object.FindObjectsByType<JcFlareOrbPartPresetBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(b.Uses(p))b.ApplyNow();
            foreach(var b in Object.FindObjectsByType<JcLuminaPartPresetBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(b.preset==p)
            {
                b.ApplyNow();
                if(!Application.isPlaying)foreach(var renderer in b.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                    if(p is FlareImpactPreset impact)block.SetFloat("_Progress",impact.previewProgress);
                    if(p is ChainLightningPreset chain){block.SetFloat("_Progress",chain.previewProgress);block.SetFloat("_Seed",chain.previewSeed);block.SetFloat("_Opacity",1f);}
                    renderer.SetPropertyBlock(block);
                }
            }
            SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();return true;
        }
        public static bool TryApply(Object p)
        {
            if(!Supported(p))return false;var roots=JcPresetPartTargets.Roots(p);if(roots.Length==0)return false;
            foreach(var asset in roots)
            {
                var path=AssetDatabase.GetAssetPath(asset);var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var b in root.GetComponentsInChildren<JcFlareOrbPartPresetBinder>(true))if(b.Uses(p))b.Transfer(p,false,true);
                    foreach(var b in root.GetComponentsInChildren<JcLuminaPartPresetBinder>(true))if(b.preset==p)
                    {
                        b.ApplyNow();
                        foreach(var renderer in b.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            if(renderer.GetComponentInParent<JcLuminaPartPresetBinder>()!=b)continue;
                            var mat=renderer.sharedMaterial;if(mat==null)continue;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                            Undo.RecordObject(mat,"루미나 전용 재질 적용");
                            for(int i=0;i<mat.shader.GetPropertyCount();i++)
                            {
                                string key=mat.shader.GetPropertyName(i);int id=Shader.PropertyToID(key);if(!block.HasProperty(id))continue;
                                switch(mat.shader.GetPropertyType(i))
                                {case ShaderPropertyType.Color:mat.SetColor(id,block.GetColor(id));break;case ShaderPropertyType.Float:case ShaderPropertyType.Range:mat.SetFloat(id,block.GetFloat(id));break;case ShaderPropertyType.Vector:mat.SetVector(id,block.GetVector(id));break;}
                            }
                            EditorUtility.SetDirty(mat);
                        }
                    }
                    foreach(var mat in root.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials).Where(x=>x!=null).Distinct())
                    {EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);}
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            TryLivePush(p);return true;
        }
        public static bool TryCapture(Object p)
        {
            if(!Supported(p))return false;var roots=JcPresetPartTargets.Roots(p);if(roots.Length==0)return false;
            Undo.RecordObject(p,"루미나 연결 부품 캡처");
            if(p is FlareOrbPresetBase || p is FlareOrbSpritePreset)
                roots.SelectMany(g=>g.GetComponentsInChildren<JcFlareOrbPartPresetBinder>(true)).First(b=>b.Uses(p)).Transfer(p,true,false);
            else if(p is SolarPrismPreset solar)CaptureSolar(solar,roots);
            else if(p is PrismExplosionPreset prism)CapturePrism(prism,roots);
            else if(p is ChainLightningPreset chain)CaptureChain(chain,roots);
            else if(p is FlareImpactPreset impact)CaptureImpact(impact,roots);
            EditorUtility.SetDirty(p);TryLivePush(p);return true;
        }
        private static Material MaterialFor(GameObject[] roots,string name)
        {
            var r=roots.SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).FirstOrDefault(x=>x.name.Contains(name)&&x.sharedMaterial!=null);
            if(r==null)throw new InvalidOperationException("루미나 실제 연결에서 재질을 찾지 못했습니다: "+name);
            return r.sharedMaterial;
        }
        private static void CaptureSolar(SolarPrismPreset p,GameObject[] roots)
        {
            Undo.RecordObject(p, "Capture Solar Prism");

            var mc = MaterialFor(roots,"Crystal");
            p.bodyColor = mc.GetColor("_ColorBody");
            p.rimColor = mc.GetColor("_ColorRim");
            p.crystalEmission = mc.GetFloat("_Emission");
            p.bodyAlpha = mc.GetFloat("_BodyAlpha");
            p.rimAlpha = mc.GetFloat("_RimAlpha");
            p.fresnelPow = mc.GetFloat("_FresnelPow");
            p.facetAmount = mc.GetFloat("_FacetAmount");

            var mf = MaterialFor(roots,"Flare");
            p.flareColor = mf.GetColor("_Color");
            p.flareEmission = mf.GetFloat("_Emission");
            p.spikeNarrow = mf.GetFloat("_SpikeNarrow");
            p.spikeFall = mf.GetFloat("_SpikeFall");
            p.diagRatio = mf.GetFloat("_DiagRatio");
            p.flareCoreSize = mf.GetFloat("_CoreSize");

            var mg = MaterialFor(roots,"Ground");
            p.groundColor = mg.GetColor("_Color");
            p.groundEmission = mg.GetFloat("_Emission");
            p.groundFalloff = mg.GetFloat("_Falloff");

            var full = roots.FirstOrDefault(g=>g.GetComponentInChildren<SolarPrismVfx>(true)!=null);
            var vfx = full != null ? full.GetComponentInChildren<SolarPrismVfx>(true) : null;
            if (vfx != null)
            {
                p.unitCount = vfx.UnitCount;
                p.spacing = vfx.Spacing;
                p.forwardOffset = vfx.ForwardOffset;
                p.hoverHeight = vfx.HoverHeight;
                p.prismScale = vfx.PrismScale;
                p.hoverAmp = vfx.HoverAmp;
                p.hoverFreq = vfx.HoverFreq;
                p.spinSpeed = vfx.SpinSpeed;
                p.spinJitter = vfx.SpinJitter;
                p.summonTime = vfx.SummonTime;
                p.summonStagger = vfx.SummonStagger;
                p.beltRadius = vfx.BeltRadius;
                p.beltLocalY = vfx.BeltLocalY;
                p.beltAngleOffset = vfx.BeltAngleOffset;
                p.flareThreshold = vfx.FlareThreshold;
                p.flareSharp = vfx.FlareSharp;
                p.flareSize = vfx.FlareSize;
                p.groundSize = vfx.GroundSize;
                p.groundBase = vfx.GroundBase;
                p.groundPulse = vfx.GroundPulse;
                p.launchSpeed = vfx.LaunchSpeed;
                p.launchStagger = vfx.LaunchStagger;
                p.launchSpinMul = vfx.LaunchSpinMul;
                p.launchArc = vfx.LaunchArc;
                p.targetHeight = vfx.TargetHeight;
                p.aimBlend = vfx.AimBlend;
                var trc = vfx.GetComponentInChildren<TrailRenderer>(true);
                if (trc != null) { p.launchTrailTime = trc.time; p.launchTrailWidth = trc.widthMultiplier; }
                var im = roots.OrderByDescending(g=>g.name.Contains("impact")).SelectMany(g=>g.GetComponentsInChildren<FlareBombImpact>(true)).FirstOrDefault();
                if (im != null)
                {
                    p.impactBurstDuration = im.BurstDuration;
                    p.impactRingDuration = im.RingDuration;
                    var burst = im.transform.Find("ImpactBurst");
                    if (burst != null) p.impactSize = burst.localScale.x;
                    var ring = im.transform.Find("ImpactRing");
                    if (ring != null) p.impactRingSize = ring.localScale.x;
                }
                var mb = MaterialFor(roots,"Burst");
                if (mb != null)
                {
                    p.impactHotColor = mb.GetColor("_ColorHot");
                    p.impactGlowColor = mb.GetColor("_ColorGold");
                    p.impactEmission = mb.GetFloat("_Emission");
                }
                var mr2 = MaterialFor(roots,"Ring");
                if (mr2 != null) p.impactRingColor = mr2.GetColor("_ColorRing");
                var ps = vfx.GetComponentInChildren<ParticleSystem>(true);
                if (ps != null)
                {
                    p.emberRate = ps.emission.rateOverTime.constant;
                    p.emberSize = ps.main.startSize.constantMax / 1.3f;
                }
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[SolarPrismPreset] 현재값 캡처 완료");
                }
        private static void CapturePrism(PrismExplosionPreset p,GameObject[] roots)
        {
            Undo.RecordObject(p, "Capture Prism Explosion");

            var mc = MaterialFor(roots,"Crystal");
            if (mc != null)
            {
                p.bodyColor = mc.GetColor("_ColorBody");
                p.rimColor = mc.GetColor("_ColorRim");
                p.crystalEmission = mc.GetFloat("_Emission");
                p.bodyAlpha = mc.GetFloat("_BodyAlpha");
                p.rimAlpha = mc.GetFloat("_RimAlpha");
                p.fresnelPow = mc.GetFloat("_FresnelPow");
                p.facetAmount = mc.GetFloat("_FacetAmount");
            }
            var mf = MaterialFor(roots,"Flare");
            if (mf != null) { p.flareColor = mf.GetColor("_Color"); p.flareEmission = mf.GetFloat("_Emission"); }
            var mg = MaterialFor(roots,"Ground");
            if (mg != null) { p.groundColor = mg.GetColor("_Color"); p.groundEmission = mg.GetFloat("_Emission"); }
            var mb = MaterialFor(roots,"Burst");
            if (mb != null)
            {
                p.impactHotColor = mb.GetColor("_ColorHot");
                p.impactGlowColor = mb.GetColor("_ColorGold");
                p.impactEmission = mb.GetFloat("_Emission");
            }
            var mr2 = MaterialFor(roots,"Ring");
            if (mr2 != null) p.impactRingColor = mr2.GetColor("_ColorRing");

            var full = roots.FirstOrDefault(g=>g.GetComponentInChildren<PrismExplosionVfx>(true)!=null);
            var vfx = full != null ? full.GetComponentInChildren<PrismExplosionVfx>(true) : null;
            if (vfx != null)
            {
                p.forwardOffset = vfx.ForwardOffset;
                p.hoverHeight = vfx.HoverHeight;
                p.prismScale = vfx.PrismScale;
                p.hoverAmp = vfx.HoverAmp;
                p.hoverFreq = vfx.HoverFreq;
                p.spinSpeed = vfx.SpinSpeed;
                p.summonTime = vfx.SummonTime;
                p.flareThreshold = vfx.FlareThreshold;
                p.flareSharp = vfx.FlareSharp;
                p.flareSize = vfx.FlareSize;
                p.groundSize = vfx.GroundSize;
                p.groundBase = vfx.GroundBase;
                p.groundPulse = vfx.GroundPulse;
                p.riseTime = vfx.RiseTime;
                p.riseHeight = vfx.RiseHeight;
                p.tremorAmp = vfx.TremorAmp;
                p.tremorFreq = vfx.TremorFreq;
                p.aimSpinMul = vfx.AimSpinMul;
                p.launchSpeed = vfx.LaunchSpeed;
                p.launchSpinMul = vfx.LaunchSpinMul;
                p.launchArc = vfx.LaunchArc;
                p.targetHeight = vfx.TargetHeight;
                p.spiralRadius = vfx.SpiralRadius;
                p.spiralRate = vfx.SpiralRate;
                p.smokeLinger = vfx.SmokeLinger;
                var im = roots.OrderByDescending(g=>g.name.Contains("impact")).SelectMany(g=>g.GetComponentsInChildren<FlareBombImpact>(true)).FirstOrDefault();
                if (im != null)
                {
                    p.impactBurstDuration = im.BurstDuration;
                    p.impactRingDuration = im.RingDuration;
                    var bh = im.transform.Find("ImpactBurstH");
                    if (bh != null) p.impactSizeH = bh.localScale.x;
                    var bv = im.transform.Find("ImpactBurstV");
                    if (bv != null) p.impactSizeV = bv.localScale.y;
                    var ring = im.transform.Find("ImpactRing");
                    if (ring != null) p.impactRingSize = ring.localScale.x;
                }
                var tr = vfx.transform.Find("PrismUnit/FlightTrail");
                var trc = tr != null ? tr.GetComponent<TrailRenderer>() : null;
                if (trc != null) { p.trailTime = trc.time; p.trailWidth = trc.widthMultiplier; }
                foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!ps.gameObject.name.Contains("Embers")) continue;
                    p.emberRate = ps.emission.rateOverTime.constant;
                    p.emberSize = ps.main.startSize.constantMax / 1.3f;
                    break;
                }
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[PrismExplosionPreset] 현재값 캡처 완료");
                }
        private static void CaptureChain(ChainLightningPreset p,GameObject[] roots)
        {
            Undo.RecordObject(p, "Capture Chain Lightning");

            var matB = MaterialFor(roots,"Bolt");
            p.boltCoreColor = matB.GetColor("_ColorCore");
            p.boltGlowColor = matB.GetColor("_ColorGlow");
            p.boltEmission = matB.GetFloat("_Emission");
            p.boltWidth = matB.GetFloat("_Width");
            p.segCount = Mathf.RoundToInt(matB.GetFloat("_SegCount"));
            p.jitterAmp = matB.GetFloat("_JitterAmp");
            p.microJag = matB.GetFloat("_MicroJag");
            p.microFreq = matB.GetFloat("_MicroFreq");
            p.endPin = matB.GetFloat("_EndPin");
            p.coreWidth = matB.GetFloat("_CoreWidth");
            p.glowWidth = matB.GetFloat("_GlowWidth");
            p.headSize = matB.GetFloat("_HeadSize");
            p.headBoost = matB.GetFloat("_HeadBoost");
            p.branchCount = Mathf.RoundToInt(matB.GetFloat("_BranchCount"));
            p.branchLen = matB.GetFloat("_BranchLen");
            p.branchSlope = matB.GetFloat("_BranchSlope");
            p.branchWidthMul = matB.GetFloat("_BranchWidthMul");

            var matS = MaterialFor(roots,"Shock");
            p.shockCoreColor = matS.GetColor("_ColorCore");
            p.shockGlowColor = matS.GetColor("_ColorGlow");
            p.shockEmission = matS.GetFloat("_Emission");
            p.shockRadius = matS.GetFloat("_Radius");
            p.shockEllipseK = matS.GetFloat("_EllipseK");
            p.radJitter = matS.GetFloat("_RadJitter");
            p.arcCount = Mathf.RoundToInt(matS.GetFloat("_ArcCount"));
            p.arcDensity = matS.GetFloat("_Density");
            p.arcWidth = matS.GetFloat("_ArcWidth");
            p.arcHighlightRatio = matS.GetFloat("_HighlightRatio");
            p.wobbleAmp = matS.GetFloat("_WobbleAmp");
            p.wobbleFreq = matS.GetFloat("_WobbleFreq");
            p.glowAmt = matS.GetFloat("_GlowAmt");
            p.shockCoreGlow = matS.GetFloat("_CoreGlow");

            var matBg = MaterialFor(roots,"Bg");
            if (matBg != null)
            {
                p.bgBodyColor = matBg.GetColor("_ColorBody");
                p.bgCenterColor = matBg.GetColor("_ColorCenter");
                p.bgCenterSize = matBg.GetFloat("_CenterSize");
                p.bgSpikeCount = Mathf.RoundToInt(matBg.GetFloat("_SpikeCount"));
                p.bgSpikeLen = matBg.GetFloat("_SpikeLen");
                p.bgLenJitter = matBg.GetFloat("_LenJitter");
                p.bgSpikeWidth = matBg.GetFloat("_SpikeWidth");
                p.bgTaperSharp = matBg.GetFloat("_TaperSharp");
                p.bgOpacity = matBg.GetFloat("_MaxAlpha");
            }

            var full = roots.FirstOrDefault(g=>g.GetComponentInChildren<ChainLightningVfx>(true)!=null);
            var vfx = full != null ? full.GetComponentInChildren<ChainLightningVfx>(true) : null;
            if (vfx != null)
            {
                p.muzzleTime = vfx.MuzzleTime;
                p.muzzleSize = vfx.MuzzleSize;
                p.muzzleOffset = vfx.MuzzleOffset;
                p.drawTime = vfx.DrawTime;
                p.holdTime = vfx.HoldTime;
                p.fadeTime = vfx.FadeTime;
                p.flickerRate = vfx.FlickerRate;
                p.chainDelay = vfx.ChainDelay;
                p.chainDrawTime = vfx.ChainDrawTime;
                p.chainHoldTime = vfx.ChainHoldTime;
                p.shockDuration = vfx.ShockDuration;
                p.shockFadeTime = vfx.ShockFadeTime;
                p.shockSize = vfx.ShockSize;
                p.targetHeight = vfx.TargetHeight;
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[ChainLightningPreset] 현재값 캡처 완료");
                }
        private static void CaptureImpact(FlareImpactPreset p,GameObject[] roots)
        {
            Undo.RecordObject(p, "Capture Flare Impact");

            var matB = MaterialFor(roots,"Burst");
            p.hotColor = matB.GetColor("_ColorHot");
            p.goldColor = matB.GetColor("_ColorGold");
            p.burstEmission = matB.GetFloat("_Emission");
            p.hotCore = matB.GetFloat("_HotCore");
            p.growFrac = matB.GetFloat("_GrowFrac");
            p.fadeStart = matB.GetFloat("_FadeStart");
            p.spikeCount = Mathf.RoundToInt(matB.GetFloat("_SpikeCount"));
            p.spikeLen = matB.GetFloat("_SpikeLen");
            p.lenJitter = matB.GetFloat("_LenJitter");
            p.spikeWidth = matB.GetFloat("_SpikeWidth");
            p.widthJitter = matB.GetFloat("_WidthJitter");
            p.taperSharp = matB.GetFloat("_TaperSharp");
            p.subCount = Mathf.RoundToInt(matB.GetFloat("_SubCount"));
            p.subLen = matB.GetFloat("_SubLen");
            p.subWidth = matB.GetFloat("_SubWidth");
            p.posJitter = matB.GetFloat("_PosJitter");
            p.seed = matB.GetFloat("_Seed");
            p.flashSize = matB.GetFloat("_FlashSize");
            p.flashIntensity = matB.GetFloat("_FlashIntensity");
            p.flashFade = matB.GetFloat("_FlashFade");

            var matR = MaterialFor(roots,"Ring");
            p.ringColor = matR.GetColor("_ColorRing");
            p.ringEmission = matR.GetFloat("_Emission");
            p.ringMaxR = matR.GetFloat("_RingMaxR");
            p.ringWidth = matR.GetFloat("_RingWidth");
            p.innerGlow = matR.GetFloat("_InnerGlow");
            p.ringFadeStart = matR.GetFloat("_FadeStart");

            var full = roots.FirstOrDefault(g=>g.GetComponentInChildren<FlareBombVfx>(true)!=null);
            var impact = roots.SelectMany(g=>g.GetComponentsInChildren<FlareBombImpact>(true)).FirstOrDefault();
            if (impact != null)
            {
                var g = impact.transform.Find("BurstGround");
                if (g != null) { p.groundWidth = g.localScale.x; p.groundHeight = g.localScale.y; }
                var s = impact.transform.Find("BurstSpike");
                if (s != null) { p.spikeWidth2 = s.localScale.x; p.spikeHeight = s.localScale.y; }
                var r = impact.transform.Find("GroundRing");
                if (r != null) p.ringSize = r.localScale.x;
                p.burstDuration = impact.BurstDuration;
                p.ringDuration = impact.RingDuration;
                p.ringGroundOffset = impact.RingGroundOffset;
            }
            var vfx = full != null ? full.GetComponentInChildren<FlareBombVfx>(true) : null;
            if (vfx != null)
            {
                p.orbScale = vfx.OrbScale;
                p.summonOffset = vfx.SummonOffset;
                p.chargeDuration = vfx.ChargeDuration;
                p.chargeGrowTime = vfx.ChargeGrowTime;
                p.speed = vfx.Speed;
                p.arcHeight = vfx.ArcHeight;
                p.flightScale = vfx.FlightScale;
                p.targetHeight = vfx.TargetHeight;
                p.lingerTime = vfx.LingerTime;
                var trail = vfx.GetComponentInChildren<TrailRenderer>(true);
                if (trail != null) { p.trailTime = trail.time; p.trailWidth = trail.widthMultiplier; }
                var ember = vfx.GetComponentInChildren<ParticleSystem>(true);
                if (ember != null)
                {
                    p.emberRate = ember.emission.rateOverDistance.constant;
                    p.emberSize = ember.main.startSize.constantMax / 1.3f;
                    p.emberNoise = ember.noise.strength.constant;
                }
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[FlareImpactPreset] 현재값 캡처 완료");
                }
    }
}
