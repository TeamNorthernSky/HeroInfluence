using UnityEngine;
namespace JC.BattleTesting
{
    // JC | 2026-11-09 | 프로필의 서로 다른 enum 숫자를 캐스팅하지 않고 의미로 변환합니다.
    // 두 전투의 빛 배치·감쇠·그림자 적용은 같은 함수가 담당합니다.
    public static class JcIndoorLightSettings
    {
        public static LightType Resolve(JcBankEnvironmentProfile.LocalLightType type) =>
            type == JcBankEnvironmentProfile.LocalLightType.Point ? LightType.Point :
            type == JcBankEnvironmentProfile.LocalLightType.Directional ? LightType.Directional : LightType.Spot;
        public static LightType Resolve(JcBossEnvironmentProfile.LocalLightType type) =>
            type == JcBossEnvironmentProfile.LocalLightType.Point ? LightType.Point : LightType.Spot;
        public static void Apply(Light light, Transform stage, string label, bool enabled, LightType type,
            Vector3 position, Vector3 rotation, Color color, float intensity, float range, float angle,
            float inner, LightShadows shadows, float strength, float bias, float normalBias)
        {
            light.name = label; light.enabled = enabled; light.type = type;
            light.transform.SetPositionAndRotation(stage.TransformPoint(position), stage.rotation * Quaternion.Euler(rotation));
            light.color = color; light.intensity = Mathf.Max(0,intensity); light.range = Mathf.Max(.01f,range);
            light.spotAngle = Mathf.Clamp(angle,1,179); light.innerSpotAngle = light.spotAngle * Mathf.Clamp01(inner);
            light.shadows = shadows; light.shadowStrength = Mathf.Clamp01(strength);
            light.shadowBias = Mathf.Clamp(bias,0,2); light.shadowNormalBias = Mathf.Clamp(normalBias,0,3);
        }
        public static void ApplyPlayerFill(Light light, bool enabled)
        {
            light.type = LightType.Directional; light.cullingMask = 0; light.shadows = LightShadows.None;
            light.intensity = Mathf.Clamp01(light.intensity);
            Shader.SetGlobalVector("_JcBankFillDirection", -light.transform.forward);
            Shader.SetGlobalVector("_JcBankFillColor", light.color.linear * (enabled ? light.intensity : 0));
        }
    }
}
