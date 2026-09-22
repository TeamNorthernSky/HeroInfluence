using UnityEngine;

namespace JC.VFX
{
    /// <summary>분리된 플레어 봄의 불꽃/암흑불꽃 오브. F0/F1/F2는 표현 레이어이며 스킬 변종과 별개입니다.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class JcFlareOrbPartPresetBinder : MonoBehaviour
    {
        [Tooltip("코어·림·광원·필드 화염 설정입니다. Basic은 불꽃, Alter는 암흑불꽃입니다.")]
        public FlareOrbPreset fieldPreset;
        [Tooltip("F1: 입체 레인 화염 설정입니다. Alt 클래스 이름은 강화판을 뜻하지 않습니다.")]
        public FlareOrbAltPreset lanePreset;
        [Tooltip("F2: 스프라이트 화염 설정입니다. 부품의 오라 레이어 선택에 따라 표시됩니다.")]
        public FlareOrbSpritePreset spritePreset;

        private void Awake() => ApplyNow();
        public bool Uses(Object p) => p!=null && (p==fieldPreset || p==lanePreset || p==spritePreset);
        public void ApplyNow() => Transfer(null,false,false);

        // capture/bake는 Inspector의 명시 동작에서만 사용합니다. 일반 재생은 MPB로 인스턴스만 변경합니다.
        public void Transfer(Object selected,bool capture,bool bake)
        {
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.GetComponentInParent<JcFlareOrbPartPresetBinder>()!=this)continue;
                var mat=renderer.sharedMaterial;if(mat==null)continue;
                var block=new MaterialPropertyBlock();if(!capture&&!bake)renderer.GetPropertyBlock(block);
                System.Action<string,float> f=(k,v)=>{if(bake)mat.SetFloat(k,v);else block.SetFloat(k,v);};
                System.Action<string,Color> c=(k,v)=>{if(bake)mat.SetColor(k,v);else block.SetColor(k,v);};
                var read=capture?mat:null;bool used=false;
                string n=renderer.name;
                if(fieldPreset!=null&&(selected==null||selected==fieldPreset))
                {
                    if(n=="Core_Sphere"){Core(f,c,read,fieldPreset);used=true;}
                    else if(n=="CoreRim"){Rim(f,c,read,fieldPreset);used=true;}
                    else if(renderer.GetComponent<FlareOrbShell>()!=null && !Lane(renderer.transform))
                    {Field(f,c,read,fieldPreset,n.Contains("Back"));used=true;}
                }
                if(lanePreset!=null&&(selected==null||selected==lanePreset)&&renderer.GetComponent<FlareOrbShell>()!=null&&Lane(renderer.transform))
                {LaneMaterial(f,c,read,lanePreset,n.Contains("Back"));used=true;}
                if(spritePreset!=null&&(selected==null||selected==spritePreset)&&renderer.GetComponent<FlareOrbSpriteAura>()!=null)
                {Sprite(f,c,read,spritePreset);used=true;}
                if(used&&!capture&&!bake)renderer.SetPropertyBlock(block);
            }
            foreach(var shell in GetComponentsInChildren<FlareOrbShell>(true))
            {
                FlareOrbPresetBase p=Lane(shell.transform)?(FlareOrbPresetBase)lanePreset:fieldPreset;
                if(p==null||(selected!=null&&selected!=p))continue;
                bool back=shell.name.Contains("Back");
                if(capture)
                {
                    if(back)p.backSpinSpeed=shell.spinSpeed;
                    else {p.shellRadius=shell.radius;p.shellTipStart=shell.tipStart;p.shellTipHeight=shell.tipHeight;p.shellTipPower=shell.tipPower;p.shellYOffset=shell.yOffset;p.shellBodyHeightRatio=shell.bodyHeightRatio;p.shellSpinSpeed=shell.spinSpeed;}
                }
                else {shell.radius=p.shellRadius;shell.tipStart=p.shellTipStart;shell.tipHeight=p.shellTipHeight;shell.tipPower=p.shellTipPower;shell.yOffset=p.shellYOffset;shell.bodyHeightRatio=p.shellBodyHeightRatio;shell.spinSpeed=back?p.backSpinSpeed:p.shellSpinSpeed;}
            }
            if(fieldPreset!=null&&(selected==null||selected==fieldPreset))foreach(var light in GetComponentsInChildren<Light>(true))
            {
                var flicker=light.GetComponent<FireLightFlicker>();
                if(capture){fieldPreset.lightColor=light.color;fieldPreset.lightRange=light.range;if(flicker!=null){fieldPreset.lightIntensity=flicker.baseIntensity;fieldPreset.lightFlickerAmplitude=flicker.intensityAmplitude;}}
                else {light.color=fieldPreset.lightColor;light.range=fieldPreset.lightRange;if(flicker!=null){flicker.baseIntensity=fieldPreset.lightIntensity;flicker.intensityAmplitude=fieldPreset.lightFlickerAmplitude;}else light.intensity=fieldPreset.lightIntensity;}
            }
            if(spritePreset!=null&&(selected==null||selected==spritePreset))foreach(var sprite in GetComponentsInChildren<FlareOrbSpriteAura>(true))
            {if(capture)spritePreset.worldSize=sprite.worldSize;else sprite.worldSize=spritePreset.worldSize;}
        }
        private static bool Lane(Transform t)
        {for(var p=t.parent;p!=null;p=p.parent)if(p.name=="FlareOrbAlt")return true;return false;}
        private static void Core(System.Action<string,float> f,System.Action<string,Color> c,Material mat,FlareOrbPreset p)
        {
            if(mat!=null){if(mat.HasProperty("_ColorCore"))p.coreColor=mat.GetColor("_ColorCore");}else c("_ColorCore",p.coreColor);
            if(mat!=null){if(mat.HasProperty("_ColorMid"))p.coreMidColor=mat.GetColor("_ColorMid");}else c("_ColorMid",p.coreMidColor);
            if(mat!=null){if(mat.HasProperty("_ColorRim"))p.coreRimColor=mat.GetColor("_ColorRim");}else c("_ColorRim",p.coreRimColor);
            if(mat!=null){if(mat.HasProperty("_EmissionStrength"))p.coreEmission=mat.GetFloat("_EmissionStrength");}else f("_EmissionStrength",p.coreEmission);
            if(mat!=null){if(mat.HasProperty("_BaseAlpha"))p.coreBaseAlpha=mat.GetFloat("_BaseAlpha");}else f("_BaseAlpha",p.coreBaseAlpha);
            if(mat!=null){if(mat.HasProperty("_RimPower"))p.coreRimPower=mat.GetFloat("_RimPower");}else f("_RimPower",p.coreRimPower);
            if(mat!=null){}else f("_RimStrength",0f);
            if(mat!=null){if(mat.HasProperty("_NoiseScale"))p.coreNoiseScale=mat.GetFloat("_NoiseScale");}else f("_NoiseScale",p.coreNoiseScale);
            if(mat!=null){if(mat.HasProperty("_NoiseSpeed"))p.coreNoiseSpeed=mat.GetFloat("_NoiseSpeed");}else f("_NoiseSpeed",p.coreNoiseSpeed);
            if(mat!=null){if(mat.HasProperty("_SwirlSpeed"))p.coreSwirlSpeed=mat.GetFloat("_SwirlSpeed");}else f("_SwirlSpeed",p.coreSwirlSpeed);
        }
        private static void Rim(System.Action<string,float> f,System.Action<string,Color> c,Material mat,FlareOrbPreset p)
        {
            if(mat!=null){if(mat.HasProperty("_Color"))p.coreRimColor=mat.GetColor("_Color");}else c("_Color",p.coreRimColor);
            if(mat!=null){if(mat.HasProperty("_Intensity"))p.coreRimStrength=mat.GetFloat("_Intensity");}else f("_Intensity",p.coreRimStrength);
            if(mat!=null){if(mat.HasProperty("_RingRadius"))p.rimRadius=mat.GetFloat("_RingRadius");}else f("_RingRadius",p.rimRadius);
            if(mat!=null){if(mat.HasProperty("_RingWidth"))p.rimWidth=mat.GetFloat("_RingWidth");}else f("_RingWidth",p.rimWidth);
            if(mat!=null){if(mat.HasProperty("_HaloStrength"))p.rimHalo=mat.GetFloat("_HaloStrength");}else f("_HaloStrength",p.rimHalo);
        }
        private static void Field(System.Action<string,float> f,System.Action<string,Color> c,Material mat,FlareOrbPreset p,bool back)
        {
            if(mat!=null){if(mat.HasProperty("_ColorTongue"))p.tongueColor=mat.GetColor("_ColorTongue");}else c("_ColorTongue",p.tongueColor);
            if(mat!=null){if(mat.HasProperty("_ColorHighlight"))p.tongueHighlightColor=mat.GetColor("_ColorHighlight");}else c("_ColorHighlight",p.tongueHighlightColor);
            if(mat!=null){if(mat.HasProperty("_Emission")){if(back)p.backEmissionMul=mat.GetFloat("_Emission")/Mathf.Max(.001f,p.tongueEmission);else p.tongueEmission=mat.GetFloat("_Emission");}}else f("_Emission",back ? p.tongueEmission * p.backEmissionMul : p.tongueEmission);
            if(mat!=null){if(back&&mat.HasProperty("_Opacity"))p.backOpacity=mat.GetFloat("_Opacity");}else f("_Opacity",back ? p.backOpacity : 1f);
            if(mat!=null){if(mat.HasProperty("_NoiseScale"))p.patternScale=mat.GetFloat("_NoiseScale");}else f("_NoiseScale",p.patternScale);
            if(mat!=null){if(mat.HasProperty("_VStretch"))p.patternVStretch=mat.GetFloat("_VStretch");}else f("_VStretch",p.patternVStretch);
            if(mat!=null){if(mat.HasProperty("_Detail"))p.patternDetail=mat.GetFloat("_Detail");}else f("_Detail",p.patternDetail);
            if(mat!=null){if(mat.HasProperty("_RidgeMix"))p.ridgeMix=mat.GetFloat("_RidgeMix");}else f("_RidgeMix",p.ridgeMix);
            if(mat!=null){if(mat.HasProperty("_TaperSharp"))p.taperSharp=mat.GetFloat("_TaperSharp");}else f("_TaperSharp",p.taperSharp);
            if(mat!=null){if(mat.HasProperty("_BreakScale"))p.breakScale=mat.GetFloat("_BreakScale");}else f("_BreakScale",p.breakScale);
            if(mat!=null){if(mat.HasProperty("_BreakAmount"))p.breakAmount=mat.GetFloat("_BreakAmount");}else f("_BreakAmount",p.breakAmount);
            if(mat!=null){if(mat.HasProperty("_Threshold"))p.patternThreshold=mat.GetFloat("_Threshold");}else f("_Threshold",p.patternThreshold);
            if(mat!=null){if(mat.HasProperty("_EdgeSoftRatio"))p.edgeSoftRatio=mat.GetFloat("_EdgeSoftRatio");}else f("_EdgeSoftRatio",p.edgeSoftRatio);
            if(mat!=null){if(mat.HasProperty("_HighlightShift"))p.highlightShift=mat.GetFloat("_HighlightShift");}else f("_HighlightShift",p.highlightShift);
            if(mat!=null){if(mat.HasProperty("_FlowSpeed"))p.flowSpeed=mat.GetFloat("_FlowSpeed");}else f("_FlowSpeed",p.flowSpeed);
            if(mat!=null){if(mat.HasProperty("_Waver"))p.waver=mat.GetFloat("_Waver");}else f("_Waver",p.waver);
            if(mat!=null){if(mat.HasProperty("_WaverSpeed"))p.waverSpeed=mat.GetFloat("_WaverSpeed");}else f("_WaverSpeed",p.waverSpeed);
            if(mat!=null){if(mat.HasProperty("_Shear"))p.spiralShear=mat.GetFloat("_Shear");}else f("_Shear",p.spiralShear);
            if(mat!=null){if(mat.HasProperty("_SCurveAmount"))p.sCurveAmount=mat.GetFloat("_SCurveAmount");}else f("_SCurveAmount",p.sCurveAmount);
            if(mat!=null){if(mat.HasProperty("_SCurveFreq"))p.sCurveFreq=mat.GetFloat("_SCurveFreq");}else f("_SCurveFreq",p.sCurveFreq);
            if(mat!=null){if(mat.HasProperty("_SCurveFollow"))p.sCurveFollow=mat.GetFloat("_SCurveFollow");}else f("_SCurveFollow",p.sCurveFollow);
            if(mat!=null){if(mat.HasProperty("_SCurveUpperRatio"))p.sCurveUpperRatio=mat.GetFloat("_SCurveUpperRatio");}else f("_SCurveUpperRatio",p.sCurveUpperRatio);
            if(mat!=null){if(mat.HasProperty("_TipErodeStart"))p.tipErodeStart=mat.GetFloat("_TipErodeStart");}else f("_TipErodeStart",p.tipErodeStart);
            if(mat!=null){if(mat.HasProperty("_TipErodeStrength"))p.tipErodeStrength=mat.GetFloat("_TipErodeStrength");}else f("_TipErodeStrength",p.tipErodeStrength);
            if(mat!=null){if(mat.HasProperty("_SpeckAmount"))p.speckAmount=mat.GetFloat("_SpeckAmount");}else f("_SpeckAmount",p.speckAmount);
            if(mat!=null){if(mat.HasProperty("_SpeckScale"))p.speckScale=mat.GetFloat("_SpeckScale");}else f("_SpeckScale",p.speckScale);
        }
        private static void LaneMaterial(System.Action<string,float> f,System.Action<string,Color> c,Material mat,FlareOrbAltPreset p,bool back)
        {
            if(mat!=null){if(mat.HasProperty("_ColorTongue"))p.tongueColor=mat.GetColor("_ColorTongue");}else c("_ColorTongue",p.tongueColor);
            if(mat!=null){if(mat.HasProperty("_ColorHighlight"))p.tongueHighlightColor=mat.GetColor("_ColorHighlight");}else c("_ColorHighlight",p.tongueHighlightColor);
            if(mat!=null){if(mat.HasProperty("_Emission")){if(back)p.backEmissionMul=mat.GetFloat("_Emission")/Mathf.Max(.001f,p.tongueEmission);else p.tongueEmission=mat.GetFloat("_Emission");}}else f("_Emission",back ? p.tongueEmission * p.backEmissionMul : p.tongueEmission);
            if(mat!=null){if(back&&mat.HasProperty("_Opacity"))p.backOpacity=mat.GetFloat("_Opacity");}else f("_Opacity",back ? p.backOpacity : 1f);
            if(mat!=null){if(mat.HasProperty("_LaneCount"))p.laneCount=Mathf.RoundToInt(mat.GetFloat("_LaneCount"));}else f("_LaneCount",p.laneCount);
            if(mat!=null){if(mat.HasProperty("_LaneWidth"))p.laneWidth=mat.GetFloat("_LaneWidth");}else f("_LaneWidth",p.laneWidth);
            if(mat!=null){if(mat.HasProperty("_LaneWidthJitter"))p.laneWidthJitter=mat.GetFloat("_LaneWidthJitter");}else f("_LaneWidthJitter",p.laneWidthJitter);
            if(mat!=null){if(mat.HasProperty("_LanePosJitter"))p.lanePosJitter=mat.GetFloat("_LanePosJitter");}else f("_LanePosJitter",p.lanePosJitter);
            if(mat!=null){if(mat.HasProperty("_LaneTiltJitter"))p.laneTiltJitter=mat.GetFloat("_LaneTiltJitter");}else f("_LaneTiltJitter",p.laneTiltJitter);
            if(mat!=null){if(mat.HasProperty("_WidthNoiseScale"))p.widthNoiseScale=mat.GetFloat("_WidthNoiseScale");}else f("_WidthNoiseScale",p.widthNoiseScale);
            if(mat!=null){if(mat.HasProperty("_WidthNoiseAmount"))p.widthNoiseAmount=mat.GetFloat("_WidthNoiseAmount");}else f("_WidthNoiseAmount",p.widthNoiseAmount);
            if(mat!=null){if(mat.HasProperty("_VStart"))p.laneVStart=mat.GetFloat("_VStart");}else f("_VStart",p.laneVStart);
            if(mat!=null){if(mat.HasProperty("_VEnd"))p.laneVEnd=mat.GetFloat("_VEnd");}else f("_VEnd",p.laneVEnd);
            if(mat!=null){if(mat.HasProperty("_VJitter"))p.laneVJitter=mat.GetFloat("_VJitter");}else f("_VJitter",p.laneVJitter);
            if(mat!=null){if(mat.HasProperty("_BirthBias"))p.birthBottomBias=mat.GetFloat("_BirthBias");}else f("_BirthBias",p.birthBottomBias);
            if(mat!=null){if(mat.HasProperty("_VLengthJitter"))p.laneLengthJitter=mat.GetFloat("_VLengthJitter");}else f("_VLengthJitter",p.laneLengthJitter);
            if(mat!=null){if(mat.HasProperty("_MaxLen"))p.laneMaxLength=mat.GetFloat("_MaxLen");}else f("_MaxLen",p.laneMaxLength);
            if(mat!=null){if(mat.HasProperty("_UpperShrink"))p.laneUpperShrink=mat.GetFloat("_UpperShrink");}else f("_UpperShrink",p.laneUpperShrink);
            if(mat!=null){if(mat.HasProperty("_TravelDist"))p.laneTravelDist=mat.GetFloat("_TravelDist");}else f("_TravelDist",p.laneTravelDist);
            if(mat!=null){if(mat.HasProperty("_TravelSpeed"))p.laneTravelSpeed=mat.GetFloat("_TravelSpeed");}else f("_TravelSpeed",p.laneTravelSpeed);
            if(mat!=null){if(mat.HasProperty("_LifeFadePeak"))p.laneLifeFadePeak=mat.GetFloat("_LifeFadePeak");}else f("_LifeFadePeak",p.laneLifeFadePeak);
            if(mat!=null){if(mat.HasProperty("_TaperSharp"))p.taperSharp=mat.GetFloat("_TaperSharp");}else f("_TaperSharp",p.taperSharp);
            if(mat!=null){if(mat.HasProperty("_HighlightRatio"))p.highlightRatio=mat.GetFloat("_HighlightRatio");}else f("_HighlightRatio",p.highlightRatio);
            if(mat!=null){if(mat.HasProperty("_EdgeSoftRatio"))p.edgeSoftRatio=mat.GetFloat("_EdgeSoftRatio");}else f("_EdgeSoftRatio",p.edgeSoftRatio);
            if(mat!=null){if(mat.HasProperty("_FlowSpeed"))p.flowSpeed=mat.GetFloat("_FlowSpeed");}else f("_FlowSpeed",p.flowSpeed);
            if(mat!=null){if(mat.HasProperty("_Shear"))p.spiralShear=mat.GetFloat("_Shear");}else f("_Shear",p.spiralShear);
            if(mat!=null){if(mat.HasProperty("_SCurveAmount"))p.sCurveAmount=mat.GetFloat("_SCurveAmount");}else f("_SCurveAmount",p.sCurveAmount);
            if(mat!=null){if(mat.HasProperty("_SCurveFreq"))p.sCurveFreq=mat.GetFloat("_SCurveFreq");}else f("_SCurveFreq",p.sCurveFreq);
            if(mat!=null){if(mat.HasProperty("_SCurveUpperRatio"))p.sCurveUpperRatio=mat.GetFloat("_SCurveUpperRatio");}else f("_SCurveUpperRatio",p.sCurveUpperRatio);
            if(mat!=null){if(mat.HasProperty("_TipErodeStart"))p.tipErodeStart=mat.GetFloat("_TipErodeStart");}else f("_TipErodeStart",p.tipErodeStart);
            if(mat!=null){if(mat.HasProperty("_TipErodeStrength"))p.tipErodeStrength=mat.GetFloat("_TipErodeStrength");}else f("_TipErodeStrength",p.tipErodeStrength);
            if(mat!=null){if(!back&&mat.HasProperty("_DebugOutline"))p.debugOutline=mat.GetFloat("_DebugOutline");}else f("_DebugOutline",back ? 0f : p.debugOutline);
        }
        private static void Sprite(System.Action<string,float> f,System.Action<string,Color> c,Material mat,FlareOrbSpritePreset p)
        {
            if(mat!=null){if(mat.HasProperty("_ColorTongue"))p.tongueColor=mat.GetColor("_ColorTongue");}else c("_ColorTongue",p.tongueColor);
            if(mat!=null){if(mat.HasProperty("_ColorHighlight"))p.tongueHighlightColor=mat.GetColor("_ColorHighlight");}else c("_ColorHighlight",p.tongueHighlightColor);
            if(mat!=null){if(mat.HasProperty("_Emission"))p.tongueEmission=mat.GetFloat("_Emission");}else f("_Emission",p.tongueEmission);
            if(mat!=null){if(mat.HasProperty("_BaseRadius"))p.baseRadius=mat.GetFloat("_BaseRadius");}else f("_BaseRadius",p.baseRadius);
            if(mat!=null){if(mat.HasProperty("_EllipseRatio"))p.ellipseRatio=mat.GetFloat("_EllipseRatio");}else f("_EllipseRatio",p.ellipseRatio);
            if(mat!=null){if(mat.HasProperty("_MaxReach"))p.maxReach=mat.GetFloat("_MaxReach");}else f("_MaxReach",p.maxReach);
            if(mat!=null){if(mat.HasProperty("_ReachRatio"))p.reachRatio=mat.GetFloat("_ReachRatio");}else f("_ReachRatio",p.reachRatio);
            if(mat!=null){if(mat.HasProperty("_ReachSoft"))p.reachSoft=mat.GetFloat("_ReachSoft");}else f("_ReachSoft",p.reachSoft);
            if(mat!=null){if(mat.HasProperty("_YOffset"))p.patternYOffset=mat.GetFloat("_YOffset");}else f("_YOffset",p.patternYOffset);
            if(mat!=null){if(mat.HasProperty("_EmitterDepth"))p.emitterDepth=mat.GetFloat("_EmitterDepth");}else f("_EmitterDepth",p.emitterDepth);
            if(mat!=null){if(mat.HasProperty("_CutHeight"))p.cutHeight=mat.GetFloat("_CutHeight");}else f("_CutHeight",p.cutHeight);
            if(mat!=null){if(mat.HasProperty("_LaneCount"))p.laneCount=Mathf.RoundToInt(mat.GetFloat("_LaneCount"));}else f("_LaneCount",p.laneCount);
            if(mat!=null){if(mat.HasProperty("_LaneWidth"))p.laneWidth=mat.GetFloat("_LaneWidth");}else f("_LaneWidth",p.laneWidth);
            if(mat!=null){if(mat.HasProperty("_LaneWidthJitter"))p.laneWidthJitter=mat.GetFloat("_LaneWidthJitter");}else f("_LaneWidthJitter",p.laneWidthJitter);
            if(mat!=null){if(mat.HasProperty("_LanePosJitter"))p.lanePosJitter=mat.GetFloat("_LanePosJitter");}else f("_LanePosJitter",p.lanePosJitter);
            if(mat!=null){if(mat.HasProperty("_LaneTiltJitter"))p.laneTiltJitter=mat.GetFloat("_LaneTiltJitter");}else f("_LaneTiltJitter",p.laneTiltJitter);
            if(mat!=null){if(mat.HasProperty("_WidthNoiseScale"))p.widthNoiseScale=mat.GetFloat("_WidthNoiseScale");}else f("_WidthNoiseScale",p.widthNoiseScale);
            if(mat!=null){if(mat.HasProperty("_WidthNoiseAmount"))p.widthNoiseAmount=mat.GetFloat("_WidthNoiseAmount");}else f("_WidthNoiseAmount",p.widthNoiseAmount);
            if(mat!=null){if(mat.HasProperty("_BirthMin"))p.birthOffsetMin=mat.GetFloat("_BirthMin");}else f("_BirthMin",p.birthOffsetMin);
            if(mat!=null){if(mat.HasProperty("_BirthMax"))p.birthOffsetMax=mat.GetFloat("_BirthMax");}else f("_BirthMax",p.birthOffsetMax);
            if(mat!=null){if(mat.HasProperty("_BirthBias"))p.birthInnerBias=mat.GetFloat("_BirthBias");}else f("_BirthBias",p.birthInnerBias);
            if(mat!=null){if(mat.HasProperty("_VLengthJitter"))p.laneLengthJitter=mat.GetFloat("_VLengthJitter");}else f("_VLengthJitter",p.laneLengthJitter);
            if(mat!=null){if(mat.HasProperty("_MaxLen"))p.laneMaxLength=mat.GetFloat("_MaxLen");}else f("_MaxLen",p.laneMaxLength);
            if(mat!=null){if(mat.HasProperty("_OuterShrink"))p.outerShrink=mat.GetFloat("_OuterShrink");}else f("_OuterShrink",p.outerShrink);
            if(mat!=null){if(mat.HasProperty("_CutFarShrink"))p.cutFarShrink=mat.GetFloat("_CutFarShrink");}else f("_CutFarShrink",p.cutFarShrink);
            if(mat!=null){if(mat.HasProperty("_TravelDist"))p.laneTravelDist=mat.GetFloat("_TravelDist");}else f("_TravelDist",p.laneTravelDist);
            if(mat!=null){if(mat.HasProperty("_TravelSpeed"))p.laneTravelSpeed=mat.GetFloat("_TravelSpeed");}else f("_TravelSpeed",p.laneTravelSpeed);
            if(mat!=null){if(mat.HasProperty("_LifeFadePeak"))p.laneLifeFadePeak=mat.GetFloat("_LifeFadePeak");}else f("_LifeFadePeak",p.laneLifeFadePeak);
            if(mat!=null){if(mat.HasProperty("_TaperSharp"))p.taperSharp=mat.GetFloat("_TaperSharp");}else f("_TaperSharp",p.taperSharp);
            if(mat!=null){if(mat.HasProperty("_HighlightRatio"))p.highlightRatio=mat.GetFloat("_HighlightRatio");}else f("_HighlightRatio",p.highlightRatio);
            if(mat!=null){if(mat.HasProperty("_EdgeSoftRatio"))p.edgeSoftRatio=mat.GetFloat("_EdgeSoftRatio");}else f("_EdgeSoftRatio",p.edgeSoftRatio);
            if(mat!=null){if(mat.HasProperty("_FlowSpeed"))p.flowSpeed=mat.GetFloat("_FlowSpeed");}else f("_FlowSpeed",p.flowSpeed);
            if(mat!=null){if(mat.HasProperty("_SCurveAmount"))p.sCurveAmount=mat.GetFloat("_SCurveAmount");}else f("_SCurveAmount",p.sCurveAmount);
            if(mat!=null){if(mat.HasProperty("_SCurveFreq"))p.sCurveFreq=mat.GetFloat("_SCurveFreq");}else f("_SCurveFreq",p.sCurveFreq);
            if(mat!=null){if(mat.HasProperty("_DebugCircles"))p.debugCircles=mat.GetFloat("_DebugCircles");}else f("_DebugCircles",p.debugCircles);
        }
    }
}
