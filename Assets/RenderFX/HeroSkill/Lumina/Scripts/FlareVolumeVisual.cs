using System.Collections.Generic;
using UnityEngine;
namespace JC.VFX
{
    /// <summary>전투 이동은 ASB가 소유합니다. 이 컴포넌트는 원본 FB_M2 메시와 월드 궤적만 구동합니다.</summary>
    public sealed class FlareVolumeVisual : MonoBehaviour
    {
        public FlareVolumePreset preset;
        public Transform body;
        public Transform volume;
        public Material trailMaterial;
        struct Sample { public Vector3 a,b; public float time; }
        readonly List<Sample> samples = new List<Sample>(128);
        LineRenderer[] lines;
        Renderer[] renderers;
        Light[] lights;
        MaterialPropertyBlock block;
        Vector3 volumeScale, volumePosition;
        float clock, speed=1, distanceTravelled, flameStrength=1;
        float appearance=1;
        Quaternion launchRotation;
        bool initialized, flying, ended;
        public void Initialize()
        {
            if(initialized || body==null || volume==null || preset==null) return;
            initialized=true; volumeScale=volume.localScale;volumePosition=volume.localPosition;
            if(preset.startAtApex){flameStrength=0;appearance=0;}
            renderers=body.GetComponentsInChildren<Renderer>(true);lights=body.GetComponentsInChildren<Light>(true);block=new MaterialPropertyBlock();
            lines=new LineRenderer[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("불꽃 회전 잔상 "+i);go.layer=gameObject.layer;go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();lines[i]=line;line.sharedMaterial=trailMaterial;
                line.useWorldSpace=true;line.textureMode=LineTextureMode.Stretch;line.numCornerVertices=2;
                line.widthCurve=new AnimationCurve(new Keyframe(0,1),new Keyframe(.6f,.6f),new Keyframe(1,0));
                line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            }
            ApplyAppearance();
        }
        public void ApplyAppearance()
        {
            Initialize();if(!initialized)return;
            body.localScale=Vector3.one*(preset.diameter/2.8f);
            foreach(var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_ColorLow",preset.lowColor);block.SetColor("_ColorMid",preset.midColor);
                block.SetColor("_ColorHigh",preset.highColor);block.SetColor("_ColorCore",preset.lowColor);
                block.SetColor("_EdgeColor",preset.highColor);
                block.SetColor("_ColorRim",preset.highColor);block.SetFloat("_EmissionStrength",preset.emission*.45f*appearance);
                block.SetColor("_Color",preset.midColor);block.SetFloat("_Emission",preset.emission*appearance);
                if(r.name=="Core_Sphere")
                {
                    r.enabled=appearance>.001f;
                    block.SetFloat("_BaseAlpha",.3f*appearance);
                    block.SetFloat("_RimStrength",1.1f*appearance);
                }
                block.SetFloat("_EffectTime",clock);block.SetFloat("_UseEffectTime",1);
                if(r.transform==volume)
                {
                    r.enabled=flameStrength>.001f;
                    block.SetFloat("_Density",2.91f*flameStrength);
                }
                else if(r.name=="Flames_Clip")
                {
                    r.enabled=flameStrength>.001f;
                    block.SetFloat("_Opacity",flameStrength);
                }
                r.SetPropertyBlock(block);
            }
            foreach(var l in lights){l.color=preset.midColor;l.intensity=.6f;l.range=preset.diameter*2;}
            if(!flying && !ended)
            {
                volume.localScale=new Vector3(volumeScale.x*preset.flareSpread,volumeScale.y*.65f,volumeScale.z*preset.flareSpread);
                volume.localPosition=volumePosition* .65f;
            }
        }
        public void Begin(float playbackSpeed)
        {
            Initialize();speed=Mathf.Max(.01f,playbackSpeed);flying=true;ended=false;samples.Clear();
            distanceTravelled=0;flameStrength=0;appearance=1;launchRotation=body.rotation;
            foreach(var line in lines)line.positionCount=0;
            ApplyAppearance();
        }
        public void SummonPose(float normalizedTime)
        {
            Initialize();if(!initialized||flying)return;
            float rise=Mathf.Clamp01(normalizedTime*preset.summonSeconds/preset.RiseSeconds);
            // 상승 후반에 위쪽 불꽃을 가라앉혀 정점에서는 회전하는 코어만 남깁니다.
            flameStrength=preset.startAtApex?0:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,rise));
            appearance=preset.startAtApex?Mathf.SmoothStep(0,1,Mathf.Clamp01(normalizedTime*preset.summonSeconds/Mathf.Max(.01f,preset.appearSeconds))):1;
            body.localRotation=Quaternion.AngleAxis(clock*preset.apexSpinDegreesPerSecond,Vector3.up);
            ApplyAppearance();
        }
        public void Pose(Vector3 direction,float flightProgress)
        {
            Initialize();if(!initialized)return;
            distanceTravelled+=direction.magnitude;
            float growth=Mathf.Clamp01(distanceTravelled/Mathf.Max(.01f,preset.diameter*preset.tailGrowDistance));
            flameStrength=Mathf.SmoothStep(0,1,growth);
            if(direction.sqrMagnitude>.000001f)
            {
                var aligned=Quaternion.FromToRotation(Vector3.up,-direction.normalized)*Quaternion.AngleAxis(clock*preset.rollDegreesPerSecond,Vector3.up);
                float turn=Mathf.SmoothStep(0,1,Mathf.Clamp01(distanceTravelled/Mathf.Max(.01f,preset.diameter*.6f)));
                body.rotation=Quaternion.Slerp(launchRotation,aligned,turn);
            }
            float maxStretch=Mathf.Max(.06f,preset.tailLength*2.8f/volumeScale.y);
            float stretch=Mathf.Lerp(.06f,maxStretch,flameStrength);
            volume.localScale=new Vector3(volumeScale.x*preset.flareSpread,volumeScale.y*stretch,volumeScale.z*preset.flareSpread);
            // 작은 꼬리도 구체 앞쪽에 튀어나오지 않도록 하단을 구체 중심에 고정합니다.
            volume.localPosition=Vector3.up*(volumeScale.y*stretch*.5f);
        }
        public void End()
        {
            Initialize();ended=true;flying=false;if(body!=null)body.gameObject.SetActive(false);
        }
        public void SetSpeed(float playbackSpeed) {speed=Mathf.Max(.01f,playbackSpeed);}
        void LateUpdate(){if(Application.isPlaying)Tick(Time.deltaTime*speed);}
        public void Tick(float dt)
        {
            Initialize();if(!initialized)return;
            clock+=Mathf.Max(0,dt);ApplyAppearance();
            if(flying && !ended && flameStrength>.001f)
            {
                // 방출점의 실제 월드 좌표를 보관합니다. 탄체 회전 후에도 지난 궤적은 회전하지 않습니다.
                var offset=body.right*preset.spiralRadius*preset.diameter;
                samples.Insert(0,new Sample{a=transform.position+offset,b=transform.position-offset,time=clock});
                if(samples.Count>128)samples.RemoveAt(samples.Count-1);
            }
            samples.RemoveAll(s=>clock-s.time>preset.trailLifetime);
            for(int k=0;k<2;k++)
            {
                var line=lines[k];line.positionCount=samples.Count;line.widthMultiplier=preset.spiralWidth*preset.diameter;
                float fade=ended && samples.Count>0?Mathf.Clamp01(1-(clock-samples[0].time)/preset.trailLifetime):1;
                line.startColor=new Color(preset.midColor.r,preset.midColor.g,preset.midColor.b,fade*preset.spiralBrightness*flameStrength);
                line.endColor=new Color(preset.lowColor.r,preset.lowColor.g,preset.lowColor.b,0);
                for(int i=0;i<samples.Count;i++)line.SetPosition(i,k==0?samples[i].a:samples[i].b);
            }
        }
    }
}
