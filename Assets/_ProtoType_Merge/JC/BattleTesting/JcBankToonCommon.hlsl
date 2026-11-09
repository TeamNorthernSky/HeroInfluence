// JC | 2026-11-06 | 은행에서만 활성화되는 단계 명암과 보조광입니다. 비활성 기본값 0은 기존 표시를 보존합니다.
#ifndef JC_BANK_TOON_COMMON_INCLUDED
#define JC_BANK_TOON_COMMON_INCLUDED
float4 _JcBankToon, _JcBankToonOptions, _JcBankToonTint, _JcBankFillDirection, _JcBankFillColor;
half JcBankBand(half value)
{
    half levels = max(1, _JcBankToon.y - 1);
    half scaled = saturate(value) * levels;
    half edge = max(.001, max(_JcBankToon.z, fwidth(scaled)));
    return saturate((floor(scaled) + smoothstep(.5 - edge, .5 + edge, frac(scaled))) / levels);
}
// JC | 2026-11-06 | 비활성 때 단계 계산을 생략하여 원래 명암과 수치적으로 동일하게 유지합니다.
half JcBankCharacterRamp(half value)
{
    half strength = saturate(_JcBankToon.x * _JcBankToonOptions.y);
    if (strength <= 0) return value;
    return lerp(value,JcBankBand(value),strength);
}
half3 JcBankCharacterFill(half3 normalWS) { return _JcBankFillColor.rgb * saturate(dot(normalWS,_JcBankFillDirection.xyz)); }
#endif
