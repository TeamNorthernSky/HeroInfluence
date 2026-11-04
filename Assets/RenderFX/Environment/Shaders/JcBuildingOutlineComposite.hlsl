// 공통 선 합성: 일반 출력과 AA용 premultiplied 출력이 동일한 형상을 사용한다.
#ifndef JC_BUILDING_OUTLINE_COMPOSITE_INCLUDED
#define JC_BUILDING_OUTLINE_COMPOSITE_INCLUDED
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "JcBuildingOutlineVisibility.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor;
                float _JcOcclusionAlpha;
                float _DepthBias;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            float4 _JcBuildingTuning;
            half4 _JcBuildingFill;
            half4 _JcBuildingOutline;
            float4 _JcBuildingPixelSize;
            float4 _JcBuildingOutlineSettings; // opacity, world width, width mode (0=world / 1=pixels)
            float4 _JcBuildingOrbitBounds; // viewport center.xy, inverse half size.zw
            float4 _JcBuildingOrbit; // enabled, phase, aspect, white base width in pixels
            float4 _JcBuildingOrbitIntensity; // base outline, moving highlight, range, smoothing

            float _JcBuildingEdgeSmoothing;

            half ReadMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            }

            half4 ComposeOutline(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 maskData = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half mask = maskData.r;
                bool exterior = _JcBuildingHighlight > 1.5;
                if (exterior ? mask >= .9999h : mask <= .0001h) return 0;
                half4 fill = _JcBuildingTuning.x > 0.5 ? _JcBuildingFill : _FillColor;
                half4 outline = _JcBuildingTuning.x > 0.5 ? _JcBuildingOutline : _OutlineColor;
                float width = _JcBuildingTuning.x > 0.5 ? _JcBuildingTuning.w : _OutlineWidth;
                float fillOpacity = _JcBuildingTuning.x > 0.5 ? _JcBuildingTuning.y : maskData.g / max(mask, .0001h);
                fillOpacity = saturate(fillOpacity * fill.a);
                float outlineOpacity = _JcBuildingTuning.x > 0.5 ? _JcBuildingOutlineSettings.x : fillOpacity;
                if (_JcBuildingTuning.x > 0.5 && _JcBuildingOutlineSettings.z < 0.5)
                {
                    // 마스크 비율로 깊이를 복원하여 경계의 선형 필터 영향을 상쇄한다.
                    float buildingDepth = mask / max(maskData.b, 0.000001);
                    float distanceScale = _JcBuildingPixelSize.w > 0.5 ? 1.0 : buildingDepth;
                    width = _JcBuildingOutlineSettings.y * _JcBuildingPixelSize.z / distanceScale;
                }
                float whiteWidth = _JcBuildingTuning.x > 0.5 && _JcBuildingOrbit.x > 0.5 ? _JcBuildingOrbit.w : 0;
                half edge, whiteEdge;
                if (exterior)
                {
                    // 두 띠 모두 그룹 전체의 실루엣 밖에 그린다. 흰 선이 표면에 접한다.
                    float totalWidth = whiteWidth + width;
                    float2 pad = _JcBuildingPixelSize.xy * (totalWidth + 1);
                    if (any((abs(input.texcoord - _JcBuildingOrbitBounds.xy) - pad) * _JcBuildingOrbitBounds.zw > 1))
                        return 0;
                    half nearMask = 0, farMask = 0;
                    [unroll] for (int i = 0; i < 16; i++)
                    {
                        float angle = i * (6.28318530718 / 16.0);
                        float2 offset = float2(cos(angle), sin(angle)) * _JcBuildingPixelSize.xy;
                        if (whiteWidth > 0) nearMask = max(nearMask, ReadMask(input.texcoord + offset * whiteWidth));
                        if (width > 0) farMask = max(farMask, ReadMask(input.texcoord + offset * totalWidth));
                    }
                    whiteEdge = nearMask;
                    edge = saturate(farMask - nearMask);
                    fillOpacity = 0;
                    mask = 1; // 바깥 띠의 알파는 확장된 마스크로 결정한다.
                }
                else
                {
                    half inner = mask, whiteInner = mask;
                    if (width > 0 || whiteWidth > 0)
                    {
                        [unroll] for (int i = 0; i < 16; i++)
                        {
                            float angle = i * (6.28318530718 / 16.0);
                            float2 direction = float2(cos(angle), sin(angle));
                            inner = min(inner, ReadMask(input.texcoord + direction * _JcBuildingPixelSize.xy * width));
                            if (whiteWidth > 0)
                                whiteInner = min(whiteInner, ReadMask(input.texcoord + direction * _JcBuildingPixelSize.xy * (width + whiteWidth)));
                        }
                    }
                    whiteEdge = whiteWidth > 0 ? saturate((inner - whiteInner) / mask) : 0;
                    edge = saturate((mask - inner) / mask);
                }
                if (_JcBuildingTuning.x > 0.5 && _JcBuildingOrbit.x > 0.5)
                {
                    // 스킬 버튼과 같은 사각 둘레 좌표: 서로 반대편인 두 머리와 꼬리.
                    float2 p = (input.texcoord - _JcBuildingOrbitBounds.xy) * _JcBuildingOrbitBounds.zw;
                    float2 q = p / max(max(abs(p.x), abs(p.y)), 0.0001);
                    float aspect = max(_JcBuildingOrbit.z, 0.01);
                    float perimeter;
                    if (abs(p.y) >= abs(p.x))
                        perimeter = q.y >= 0 ? (q.x + 1) * aspect : 2 * aspect + 2 + (1 - q.x) * aspect;
                    else
                        perimeter = q.x >= 0 ? 2 * aspect + (1 - q.y) : 4 * aspect + 2 + (q.y + 1);
                    perimeter /= 4 * (aspect + 1);
                    float age = frac((_JcBuildingOrbit.y - perimeter) * 2);
                    float distanceToHead = min(age, 1 - age) * 2;
                    float coverage = saturate(_JcBuildingOrbitIntensity.z);
                    float smoothing = saturate(_JcBuildingOrbitIntensity.w);
                    float feather = max(coverage * smoothing, 0.00001);
                    float gleam = 1 - smoothstep(max(0, coverage - feather), coverage, distanceToHead);
                    // 양끝 의미를 보장: 0은 끔, 1은 위상·스무딩과 무관하게 전체 강조.
                    gleam = coverage <= 0 ? 0 : (coverage >= 1 ? 1 : gleam);
                    float head = gleam;
                    outline.rgb = lerp(outline.rgb, half3(1, 1, 1), saturate(head * 0.8 * _JcBuildingOrbitIntensity.y));
                    outlineOpacity *= 0.25 * _JcBuildingOrbitIntensity.x + 0.75 * gleam * _JcBuildingOrbitIntensity.y;
                }
                // 경계의 AA를 프리멀티플라이드 색으로 보간한 뒤 한 번만 합성한다.
                // 채움 0 / 윤곽선 1도 가능하고, 두 알파가 같으면 기존 결과와 같다.
                half fillWeight = saturate(1 - edge - whiteEdge) * fillOpacity;
                if (_JcBuildingHighlight < .5) fillWeight *= 1 - saturate(maskData.a);
                half outlineWeight = edge * saturate(outlineOpacity);
                half alpha = fillWeight + outlineWeight;
                // 서로 겹치지 않는 띠를 한 번에 합성하여 흰색 선의 폭과 색을 보존한다.
                half whiteWeight = whiteEdge;
                alpha += whiteWeight;
                half3 color = (fill.rgb * fillWeight + outline.rgb * outlineWeight + whiteWeight.xxx) / max(alpha, 0.0001h);
                return half4(color, saturate(mask * alpha));
            }

#endif
