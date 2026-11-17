using System.Collections.Generic;
using UnityEngine;
using JC.Environment;

namespace JC.BattleTesting
{
    // 도시 외형에만 적용합니다. 전투 캐릭터/실내 조명과 전역 탐사 환경은 변경하지 않습니다.
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class JcBossCityTime : MonoBehaviour
    {
        // JC | 2026-11-16 | 이름에 붙는 복제 번호와 무관하게 기존 시간대 컴포넌트로 도시 단위를 식별합니다.
        public static Transform[] Roots(Transform stage)
        {
            var result = new List<Transform>();
            if (!stage) return result.ToArray();
            foreach (var time in stage.GetComponentsInChildren<JcBossCityTime>(true))
            {
                bool nested = false;
                for (var parent = time.transform.parent; parent && parent.IsChildOf(stage); parent = parent.parent)
                    if (parent.GetComponent<JcBossCityTime>()) { nested = true; break; }
                if (!nested) result.Add(time.transform);
            }
            return result.ToArray();
        }

        // JC | 2026-11-17 | 미리보기 중 도시를 복제해도 배율 적용 전 조명값을 함께 복제하여 중복 보정을 막습니다.
        [System.Serializable]
        public sealed class LampDefaults
        {
            public Light light;
            public DayNightLightController timer;
            public float range, intensity, innerAngle;
            public int mask;
            public bool timerEnabled, lightEnabled;
            public LightShadows shadows;
        }
        [SerializeField, HideInInspector] List<LampDefaults> lampDefaults = new List<LampDefaults>();
        public LampDefaults GetLampDefaults(Light light)
        {
            var found = lampDefaults.Find(v => v.light == light);
            if (found != null) return found;
            var timer = light.GetComponentInParent<DayNightLightController>();
            found = new LampDefaults { light=light, timer=timer, range=light.range, intensity=light.intensity,
                innerAngle=light.innerSpotAngle, mask=light.cullingMask, shadows=light.shadows,
                timerEnabled=timer && timer.enabled, lightEnabled=light.enabled };
            lampDefaults.Add(found); return found;
        }
        public void CaptureLampDefaults()
        {
            lampDefaults.Clear();
            foreach (var light in GetComponentsInChildren<Light>(true)) GetLampDefaults(light);
        }
        public void RestoreLampDefaults()
        {
            foreach (var v in lampDefaults) if (v.light) {
                v.light.range=v.range; v.light.intensity=v.intensity; v.light.innerSpotAngle=v.innerAngle;
                v.light.cullingMask=v.mask; v.light.shadows=v.shadows;
                if(v.timer) v.timer.enabled=v.timerEnabled;
                v.light.enabled=v.lightEnabled;
            }
        }

        [Tooltip("탐사와 공유하는 시간대 색상 설정입니다.")] public JcDayNightProfile profile;
        [Range(0,24),Tooltip("탐사 진행이 없는 테스트의 기준 시각입니다.")] public float previewHour=8.5f;
        [Min(0),Tooltip("야간 창문 발광 강도입니다.")] public float windowIntensity=2.2f;
        [ColorUsage(false,true),Tooltip("야간 창문 불빛 색상입니다.")] public Color windowColor=new Color(1,.74f,.36f);
        readonly List<Renderer> targets=new List<Renderer>();
        public double Hour {get; private set;}
        // JC | 2026-11-05 | 실제 월드 조명에서는 예전 베이크용 색 곱셈을 중복 적용하지 않습니다.
        [HideInInspector] public bool worldLighting;
        public void UseWorldLighting(){worldLighting=true;foreach(var r in GetComponentsInChildren<Renderer>(true)){r.SetPropertyBlock(null);for(int i=0;i<r.sharedMaterials.Length;i++)r.SetPropertyBlock(null,i);}}
        void OnEnable(){if(!Application.isPlaying)SetHour(previewHour);}
        void Awake(){if(Application.isPlaying)SetHour(CaptureHour());}
        public static double CaptureHour()
        {
            var p=Resources.Load<JcDayNightProfile>("JC_DayNight_Default");
            return p!=null?p.HoursForTurn(GameManager.Instance!=null?GameManager.Instance.CurrentDay:1):8.5;
        }
        public void SetHour(double hour)
        {
            Hour=hour;if(worldLighting)return;if(profile==null)profile=Resources.Load<JcDayNightProfile>("JC_DayNight_Default");
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
