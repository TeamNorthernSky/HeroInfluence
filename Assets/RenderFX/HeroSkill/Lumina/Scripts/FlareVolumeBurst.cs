using UnityEngine;
namespace JC.VFX
{
    public sealed class FlareVolumeBurst : MonoBehaviour
    {
        public FlareVolumePreset preset;
        public Renderer fire;
        MaterialPropertyBlock block;
        public void Sample(float progress)
        {
            if(!preset||!fire)return;
            float p=Mathf.Clamp01(progress);
            fire.gameObject.SetActive(p<.85f);
            float expansion=1-Mathf.Pow(1-Mathf.Clamp01(p/.35f),3);
            fire.transform.localScale=Vector3.one*(preset.explosionDiameter*Mathf.Lerp(.15f,1,expansion));
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.85f,p));
            if(block==null)block=new MaterialPropertyBlock();fire.GetPropertyBlock(block);
            block.SetFloat("_EffectTime",p*.9f);block.SetFloat("_UseEffectTime",1);
            block.SetColor("_ColorLow",preset.lowColor);block.SetColor("_ColorMid",preset.midColor);block.SetColor("_ColorHigh",preset.highColor);
            block.SetFloat("_Emission",preset.explosionGlow*fade);block.SetFloat("_Density",2.3f*fade);
            block.SetFloat("_Threshold",Mathf.Lerp(.25f,.72f,p));block.SetFloat("_StretchY",1);
            block.SetFloat("_TopTaper",0);block.SetFloat("_MirrorX",0);block.SetFloat("_ShapeNoise",.25f);
            fire.SetPropertyBlock(block);
        }
    }
}
