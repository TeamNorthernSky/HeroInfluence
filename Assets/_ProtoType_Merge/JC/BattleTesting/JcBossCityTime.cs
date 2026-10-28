using System.Collections.Generic;
using UnityEngine;
using JC.Environment;

namespace JC.BattleTesting
{
    // 도시 외형에만 적용합니다. 전투 캐릭터/실내 조명과 전역 탐사 환경은 변경하지 않습니다.
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class JcBossCityTime : MonoBehaviour
    {
        [Tooltip("탐사와 공유하는 시간대 색상 설정입니다.")] public JcDayNightProfile profile;
        [Range(0,24),Tooltip("탐사 진행이 없는 테스트의 기준 시각입니다.")] public float previewHour=8.5f;
        [Min(0),Tooltip("야간 창문 발광 강도입니다.")] public float windowIntensity=2.2f;
        [ColorUsage(false,true),Tooltip("야간 창문 불빛 색상입니다.")] public Color windowColor=new Color(1,.74f,.36f);
        readonly List<Renderer> targets=new List<Renderer>();
        public double Hour {get; private set;}
        void OnEnable(){if(!Application.isPlaying)SetHour(previewHour);}
        void Awake(){if(Application.isPlaying)SetHour(CaptureHour());}
        public static double CaptureHour()
        {
            var p=Resources.Load<JcDayNightProfile>("JC_DayNight_Default");
            return p!=null?p.HoursForTurn(GameManager.Instance!=null?GameManager.Instance.CurrentDay:1):8.5;
        }
        public void SetHour(double hour)
        {
            Hour=hour;if(profile==null)profile=Resources.Load<JcDayNightProfile>("JC_DayNight_Default");
            if(profile==null)return;
            if(targets.Count==0)targets.AddRange(GetComponentsInChildren<Renderer>(true));
            var key=profile.Evaluate(hour);
            float night=1-Mathf.InverseLerp(.22f,.85f,key.daylight);
            Color tint=Color.Lerp(Color.white,profile.nightHorizon*.9f,night);
            foreach(var r in targets)
            {
                if(r==null)continue;
                var mats=r.sharedMaterials;
                for(int slot=0;slot<mats.Length;slot++)
                {
                    var mat=mats[slot];if(mat==null)continue;
                    var block=new MaterialPropertyBlock();r.GetPropertyBlock(block,slot);
                    if(mat.HasProperty("_BaseColor"))block.SetColor("_BaseColor",mat.GetColor("_BaseColor")*tint);
                    if(mat.HasProperty("_EdgeColor"))block.SetColor("_EdgeColor",mat.GetColor("_EdgeColor")*tint);
                    if(mat.HasProperty("_EmissionMap")&&mat.GetTexture("_EmissionMap")!=null)
                        block.SetColor("_EmissionColor",windowColor*(windowIntensity*night));
                    if(mat.HasProperty("_Reflection")){var v=mat.GetVector("_Reflection");v.x*=1-night*.9f;block.SetVector("_Reflection",v);}
                    r.SetPropertyBlock(block,slot);
                }
            }
        }
        [ContextMenu("도시 시간 미리보기 적용")]
        void Preview(){SetHour(previewHour);}
    }
}
