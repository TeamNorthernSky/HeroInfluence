using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    public sealed partial class JcBossCityLighting
    {
        // JC | 2026-11-17 | 원경 가로등만 카메라 거리/개수로 제한합니다. 실내 광원과 기존 점등 시각은 유지합니다.
        sealed class Lamp
        {
            public JcBossCityTime.LampDefaults baseline;
            public float intensity, weight, target, distance;
        }
        readonly List<Lamp> lamps = new();
        readonly List<Lamp> candidates = new();
        readonly Plane[] lampPlanes = new Plane[6];
        JcBossBattleBackground optimization;
        Camera lampCamera;
        double nextLampSelection, lastLampUpdate;

        void ConfigureLamp(Transform city, JcBossCityTime.LampDefaults baseline)
        {
            var light = baseline.light;
            float scale = Mathf.Abs(city.lossyScale.x);
            light.cullingMask = 1 << CityLayer;
            light.range = baseline.range * scale;
            light.innerSpotAngle = light.spotAngle * optimization.cityLampInnerRatio;
            light.shadows = LightShadows.None;
            if(baseline.timer) baseline.timer.enabled = false;
            float intensity = baseline.intensity * scale * scale * optimization.cityLampBrightness;
            light.intensity = intensity;
            lamps.Add(new Lamp { baseline=baseline, intensity=intensity });
        }

        void UpdateCityLamps(bool immediate)
        {
            if(!optimization || !Clock) return;
            double now = Time.realtimeSinceStartupAsDouble;
            float step = immediate ? 1 : Mathf.Clamp01((float)(now-lastLampUpdate)/.35f);
            lastLampUpdate=now;
            if(!lampCamera) {
                foreach(var root in gameObject.scene.GetRootGameObjects())
                    foreach(var camera in root.GetComponentsInChildren<Camera>())
                        if(camera.CompareTag("MainCamera")) { lampCamera=camera; break; }
            }
            if(immediate || now>=nextLampSelection) {
                nextLampSelection=now+.15;
                candidates.Clear();
                if(lampCamera) GeometryUtility.CalculateFrustumPlanes(lampCamera,lampPlanes);
                foreach(var lamp in lamps) {
                    var v=lamp.baseline; var light=v.light; lamp.target=0;
                    if(!light || !light.gameObject.activeInHierarchy) continue;
                    // JC | 2026-11-17 | 기존 타이머를 직접 갱신하여 18:30~06:30 등 개별 점등 설정을 재사용합니다.
                    if(v.timer && v.timerEnabled) v.timer.RefreshLight();
                    else light.enabled=v.lightEnabled;
                    if(!light.enabled) continue;
                    lamp.distance=lampCamera?Vector3.Distance(light.transform.position,lampCamera.transform.position):0;
                    if(lamp.distance>=optimization.cityLampDistance) continue;
                    if(lampCamera && !GeometryUtility.TestPlanesAABB(lampPlanes,new Bounds(light.transform.position,Vector3.one*light.range*2))) continue;
                    candidates.Add(lamp);
                }
                candidates.Sort((a,b)=>a.distance.CompareTo(b.distance));
                int count=Mathf.Min(optimization.cityLampBudget,candidates.Count);
                for(int i=0;i<count;i++) {
                    var lamp=candidates[i];
                    lamp.target=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(optimization.cityLampDistance*.75f,optimization.cityLampDistance,lamp.distance));
                }
            }
            foreach(var lamp in lamps) if(lamp.baseline.light) {
                // JC | 2026-11-17 | 거리/개수 경계 진입 시 광량을 보간해 갑자기 켜지거나 꺼지는 현상을 줄입니다.
                lamp.weight=Mathf.Lerp(lamp.weight,lamp.target,step);
                var light=lamp.baseline.light;
                light.intensity=lamp.intensity*lamp.weight;
                light.enabled=lamp.weight>.005f;
            }
        }
    }
}
