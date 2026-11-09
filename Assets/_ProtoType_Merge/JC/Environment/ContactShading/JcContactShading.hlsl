// JC | 2026-11-06 | 카메라와 무관한 접점 마스크입니다. 금속·광택·조명 계산 전에 표면색에만 적용합니다.
#ifndef JC_CONTACT_SHADING_INCLUDED
#define JC_CONTACT_SHADING_INCLUDED
#define JC_CONTACT_MAX_SEGMENTS 64
float4x4 _JcContactWorldToLocal;
float4 _JcContactStarts[JC_CONTACT_MAX_SEGMENTS];
float4 _JcContactEnds[JC_CONTACT_MAX_SEGMENTS];
float4 _JcContactReceivers[JC_CONTACT_MAX_SEGMENTS];
int _JcContactCount;
half4 _JcContactTint;
float _JcContactStrength, _JcContactWidth, _JcContactSoftness;

half3 JcApplyContactShading(half3 albedo, float3 positionWS)
{
    if (_JcContactStrength <= 0 || _JcContactCount <= 0) return albedo;
    float3 p = mul(_JcContactWorldToLocal, float4(positionWS, 1)).xyz;
    float distanceSquared = _JcContactWidth * _JcContactWidth;
    // JC | 2026-11-06 | 가장 가까운 접점까지의 거리로 합집합을 만들어 코너에서도 음영을 중복 가산하지 않습니다.
    [loop] for (int i = 0; i < min(_JcContactCount, JC_CONTACT_MAX_SEGMENTS); i++)
    {
        float3 a = _JcContactStarts[i].xyz;
        float3 ab = _JcContactEnds[i].xyz - a;
        float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-10));
        float3 delta = p - (a + t * ab);
        // JC | 2026-11-06 | 벽 접점에서 음영이 끊기거나 양쪽으로 번지지 않도록 바닥 쪽 반공간만 사용합니다.
        if (dot(delta, _JcContactReceivers[i].xyz) < -0.0001) continue;
        distanceSquared = min(distanceSquared, dot(delta, delta));
    }
    float featherStart = _JcContactWidth * (1 - _JcContactSoftness);
    half mask = 1 - smoothstep(featherStart, _JcContactWidth, sqrt(distanceSquared));
    return albedo * lerp(half3(1, 1, 1), _JcContactTint.rgb, saturate(mask * _JcContactStrength));
}
#endif
