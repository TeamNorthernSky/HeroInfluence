Shader "JC/Battle/SoftCharacter"
{
 Properties
 {
  _BaseMap("Base Map",2D)="white"{}
  _BaseColor("Base Color",Color)=(1,1,1,1)
  // JC | 2026-11-18 | 증폭기 전용 변형에서만 샘플하는 색 영역·접점 AO입니다. 원본 메시와 노멀은 그대로 사용합니다.
  _AmpMask("증폭기 색 영역·AO",2D)="white"{}
  _AmpBody("증폭기 몸체",Color)=(.84,.86,.89,1)
  _AmpTrim("증폭기 받침",Color)=(.48,.51,.58,1)
  _AmpAccent("증폭기 포인트",Color)=(.48,.32,.62,1)
  _AmpSmoothness("증폭기 매끄러움",Range(0,1))=.3
  _AmpReflection("증폭기 반사",Range(0,1))=.55
  _AmpOcclusion("증폭기 홈 음영",Range(0,1))=.55
  _HitFlash("Hit Flash",Range(0,1))=0
  _EmissionMap("Emission Map",2D)="white"{}
  _EmissionColor("Emission",Color)=(0,0,0,0)
  _Cutoff("Cutoff",Range(0,1))=.5
  _Cull("Cull",Float)=2
  _ShadeFloor("Shade Floor",Range(0,1))=.7
  _Wrap("Light Wrap",Range(0,1))=.4
  _ShadowStrength("Received Shadow",Range(0,1))=.2
  _AlbedoLift("Albedo Lift",Range(0,1))=.08
  _Saturation("Saturation",Range(0,2))=1.15
  // JC | 2026-11-18 | 캐릭터 복제 재질 전용 보정: x=사용, y=밝기 배율. 씬 노출/조명에는 영향이 없습니다.
  _CharacterColor("Character Color",Vector)=(1,1,0,0)
  // JC | 2026-11-18 | 율리아·증폭기 복제 재질만 시간대별 대비/수광/반사를 받습니다. 기본값은 기존 출력입니다.
  [HideInInspector] _BossSurfaceResponse("Boss Surface Response",Vector)=(0,.18,1,1)
  _RimStrength("Rim Strength",Range(0,.3))=.025
  _MoldingStrength("Molding Strength",Range(0,1))=0
  _MoldingSharpness("Molding Sharpness",Range(4,96))=32
  _KeyDirection("Key Direction",Vector)=(.4,.8,-.3,0)
  _KeyTint("Key Tint",Color)=(1,.98,.94,1)
  _FillTint("Fill Tint",Color)=(.87,.92,1,1)
  // JC | 2026-11-09 | 후면광은 실제 플레이어 4종 복제 재질만 받습니다.
  [HideInInspector] _JcPlayerBackFillTarget("Player Back Fill Target",Float)=0
  [HideInInspector] _BossLocalOnly("보스 전용 조명만",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit"
   Tags{"LightMode"="UniversalForward"}
   Cull [_Cull]
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   // JC | 2026-11-05 | BE490에서는 기존 자체 명암 대신 실제 창문/추가광 및 그림자를 받습니다.
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   // JC | 2026-11-09 | 선택형 실내 실제광 모드에서도 은행의 기존 주변 음영을 수신합니다.
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fragment _ _LIGHT_COOKIES
   #pragma multi_compile _ _FORWARD_PLUS
   // JC | 2026-11-12 | 실내 실제광을 선택한 캐릭터 복제 재질만 IBL을 받습니다.
   #pragma multi_compile_local_fragment _ _JC_INDOOR_IBL_TARGET
   // JC | 2026-11-18 | 런타임 복제 재질의 증폭기 전용 변형도 빌드에 보존합니다.
   #pragma multi_compile_local_fragment _ _JC_AMPLIFIER_SURFACE
   #pragma multi_compile_fog
   #pragma shader_feature_local_fragment _ALPHATEST_ON
   // JC | 2026-11-12 | 실제 실내광 모드의 캐릭터에도 환경광·반사를 연결합니다. 기존 부드러운 명암 모드는 보존합니다.
   #include "Assets/_ProtoType_Merge/JC/BattleTesting/JcIndoorLighting.hlsl"
   #include "Assets/_ProtoType_Merge/JC/BattleTesting/JcBossLocalLighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
   TEXTURE2D(_AmpMask); SAMPLER(sampler_AmpMask);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_EmissionMap_ST,_KeyDirection;
   half4 _BaseColor,_EmissionColor,_KeyTint,_FillTint;
   float4 _CharacterColor;
   float4 _BossSurfaceResponse;
   half4 _AmpBody,_AmpTrim,_AmpAccent;
   float _AmpSmoothness,_AmpReflection,_AmpOcclusion;
   float _Cutoff,_ShadeFloor,_Wrap,_ShadowStrength,_AlbedoLift,_RimStrength,_Saturation,_MoldingStrength,_MoldingSharpness,_HitFlash;
   float _BossLocalOnly, _JcPlayerBackFillTarget;
   CBUFFER_END
   float4 _JcDayNight, _JcNightReadability;
   // JC | 2026-11-18 | 실내 실제광 결과에도 공통 채도·밝기를 적용합니다. 가산 밝기를 쓰지 않아 무광원 검정과 그림자를 유지합니다.
   half3 CharacterColor(half3 color)
   {
    half luminance=dot(color,half3(.2126,.7152,.0722));
    half saturation=lerp(1,clamp(_Saturation,0,2),saturate(_CharacterColor.x));
    return max(0,lerp(luminance.xxx,color,saturation))*lerp(1,_CharacterColor.y,saturate(_CharacterColor.x));
   }
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
   // JC | 2026-11-18 | 원본 UV를 별도 유지하여 기본색 타일링과 색 영역 데이터가 섞이지 않게 합니다.
   struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;float2 maskUV:TEXCOORD4;};
   V vert(A i){V o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.maskUV=i.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
   half4 frag(V i):SV_Target
   {
    half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
    half amplifierAo=1, amplifierReflection=1;
    #if defined(_JC_AMPLIFIER_SURFACE)
     half3 mask=SAMPLE_TEXTURE2D(_AmpMask,sampler_AmpMask,i.maskUV).rgb;
     // JC | 2026-11-18 | 몸체 색을 분리해도 기존 렌더러의 기본색 곱(런타임 틴트)은 보존합니다.
     albedo.rgb=lerp(lerp(_AmpBody.rgb,_AmpTrim.rgb,saturate(mask.r)),_AmpAccent.rgb,saturate(mask.g))*_BaseColor.rgb;
     amplifierAo=lerp(1,saturate(mask.b),saturate(_AmpOcclusion));
     amplifierReflection=saturate(_AmpReflection);
    #endif
    #if defined(_ALPHATEST_ON)
     clip(albedo.a-_Cutoff);
    #endif
    float3 n=normalize(i.normalWS);
    if(_BossLocalOnly>.5)
    {
     // JC | 2026-11-05 | 최소 밝기/림/발광/안개를 제외하여 조명이 없으면 캐릭터도 검게 됩니다. 일반 전투 경로는 유지합니다.
     SurfaceData surface=(SurfaceData)0; surface.albedo=albedo.rgb; surface.alpha=albedo.a; surface.smoothness=.25; surface.occlusion=1;
     // JC | 2026-11-18 | 기존 캐릭터의 고정 표면값은 유지하고 증폭기만 프로필과 베이크 AO를 사용합니다.
     #if defined(_JC_AMPLIFIER_SURFACE)
      surface.smoothness=saturate(_AmpSmoothness);surface.occlusion=amplifierAo;
     #endif
     InputData inputData=(InputData)0; inputData.positionWS=i.positionWS; inputData.normalWS=n; inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
     inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
     // JC | 2026-11-09 | 선택형 은행 실제광 경로의 주광 그림자/실내 간접광 입력을 완성합니다. 보스 GI는 계속 차단합니다.
     inputData.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
     inputData.shadowMask=half4(1,1,1,1);
     inputData.bakedGI=_JcIndoorMode.x>1.5?SampleSH(n):half3(0,0,0);
     // JC | 2026-11-09 | 은행 실제광은 실내 환경을, 보스는 창문/추가광만 계산합니다.
     half4 localColor=_JcIndoorMode.x>1.5 ? JcBankPbr(inputData,surface) : JcBossLocalSurface(inputData,surface,2,amplifierReflection,amplifierAo,_BossSurfaceResponse.zw);
     localColor.rgb+=albedo.rgb*JcBankCharacterFill(n)*saturate(_JcPlayerBackFillTarget);
     // JC | 2026-11-18 | 암부만 부드럽게 낮추고 임계 밝기 이상은 보존합니다. RGB 공통 배율로 색상을 유지하며 스폿 감쇠·그림자·공통 EV 계산은 바꾸지 않습니다.
     half bossLuminance=dot(localColor.rgb,half3(.2126,.7152,.0722));
     half bossShadow=1-smoothstep(0,max(.05,_BossSurfaceResponse.y),bossLuminance);
     localColor.rgb*=1-clamp(_BossSurfaceResponse.x,0,.6)*bossShadow;
     // JC | 2026-11-18 | 실제광·IBL·보조광 합산 이후 한 번만 보정하며 배경/유리/광원 계산은 변경하지 않습니다.
     localColor.rgb=CharacterColor(localColor.rgb);
     localColor.a=albedo.a; return localColor;
    }
    float lightAmount=smoothstep(-max(.01,_Wrap),1,dot(n,normalize(_KeyDirection.xyz)));
    // JC | 2026-11-09 | 은행의 기존 단계 명암을 보존하며 보스도 동일 옵션으로 조절합니다.
    lightAmount=JcBankCharacterRamp(lightAmount);
    half3 lighting=lerp(_FillTint.rgb*_ShadeFloor,_KeyTint.rgb,lightAmount);
    Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    lighting*=lerp(1,main.shadowAttenuation,_ShadowStrength);
    lighting*=lerp(half3(1,1,1),lerp(_JcBankToonTint.rgb,half3(1,1,1),lightAmount),saturate(_JcBankToon.x*_JcBankToonOptions.y));
    lighting+=JcBankCharacterFill(n)*saturate(_JcPlayerBackFillTarget);
    half3 color=lerp(albedo.rgb,sqrt(max(albedo.rgb,0)),_AlbedoLift)*lighting;
    // JC | 2026-11-18 | 기존 부드러운 명암의 채도 적용 위치를 보존하고 같은 공통 밝기 조절을 연결합니다.
    color=CharacterColor(color);
    float rim=pow(1-saturate(dot(n,GetWorldSpaceNormalizeViewDir(i.positionWS))),3);
    color+=albedo.rgb*rim*_RimStrength;
    // 실제 면의 노멀 변화와 좁은 반사띠로 몰딩을 강조합니다. 새 경계선을 만들지는 않습니다.
    float crease=saturate(length(fwidth(n))*5);
    float3 halfVector=normalize(normalize(_KeyDirection.xyz)+GetWorldSpaceNormalizeViewDir(i.positionWS));
    float highlight=pow(saturate(dot(n,halfVector)),max(4,_MoldingSharpness));
    color=color*(1-crease*_MoldingStrength*.4)+_KeyTint.rgb*highlight*_MoldingStrength*.22;
    // JC | 2026-11-18 | 부드러운 명암 모드에서도 증폭기 AO와 완만한 표면 반사를 유지합니다. 단계식 툰이나 외곽선은 추가하지 않습니다.
    #if defined(_JC_AMPLIFIER_SURFACE)
     color*=amplifierAo;
     float broadHighlight=pow(saturate(dot(n,halfVector)),lerp(8,64,saturate(_AmpSmoothness)));
     color+=_KeyTint.rgb*broadHighlight*amplifierReflection*.08;
    #endif
    if(_JcDayNight.x>.5)color*=max(.01,_JcNightReadability.y);
    color+=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb;
    color*=JcIndoorOcclusion(i.positionWS,n);
    color=lerp(color,half3(1,1,1),saturate(_HitFlash));
    return half4(MixFog(color,i.fog),albedo.a);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
