Shader "Testbed/FlareBomb/FireVolume_Meteor"
{
    Properties
    {
        [HDR] _ColorLow  ("Low Color",  Color) = (0.9, 0.10, 0.02, 1)
        [HDR] _ColorMid  ("Mid Color",  Color) = (1.0, 0.40, 0.05, 1)
        [HDR] _ColorHigh ("High Color", Color) = (1.0, 0.90, 0.45, 1)
        _Emission ("Emission", Range(0,6)) = 2.0
        _NoiseScale ("Noise Scale", Range(1, 10)) = 4.0
        _RiseSpeed ("Rise Speed", Range(0, 6)) = 1.8
        _SwirlSpeed ("Swirl Speed", Range(0, 4)) = 0.6
        _Density ("Density", Range(0.2, 6)) = 2.2
        _Threshold ("Threshold", Range(0, 1)) = 0.45
        _EdgeFade ("Edge Fade", Range(0.2, 1)) = 0.7
        _Steps ("March Steps", Range(8, 48)) = 28
        _FlameTex ("Flame Profile (u=angle, v=height)", 2D) = "white" {}
        _AngleScroll ("Angle Scroll", Range(0,2)) = 0.12
        _ShapeNoise ("Shape Noise Breakup", Range(0,0.5)) = 0.14
        _DetailNoise ("Detail Noise (wispy turbulence)", Range(0,1)) = 0.4
        _DetailScale ("Detail Noise Scale", Range(1,8)) = 3.0
        _DensityBoost ("Density Boost (max headroom)", Range(1,3)) = 1.0
        _StretchY ("Teardrop Slim (taller look)", Range(1,3)) = 1.4
        _TopTaper ("Teardrop Top Taper (pointed tip)", Range(0,0.9)) = 0.45
        _MirrorX ("Bilateral Symmetry (mirror X)", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "FireVolume"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front                       // render backfaces; march from camera through volume

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float hash31(float3 p){ p=frac(p*0.3183099+0.1); p*=17.0; return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
            float vnoise(float3 x){
                float3 i=floor(x), f=frac(x); f=f*f*(3.0-2.0*f);
                float n000=hash31(i),n100=hash31(i+float3(1,0,0)),n010=hash31(i+float3(0,1,0)),n110=hash31(i+float3(1,1,0));
                float n001=hash31(i+float3(0,0,1)),n101=hash31(i+float3(1,0,1)),n011=hash31(i+float3(0,1,1)),n111=hash31(i+float3(1,1,1));
                float nx00=lerp(n000,n100,f.x),nx10=lerp(n010,n110,f.x),nx01=lerp(n001,n101,f.x),nx11=lerp(n011,n111,f.x);
                return lerp(lerp(nx00,nx10,f.y), lerp(nx01,nx11,f.y), f.z);
            }
            float fbm(float3 p){ float s=0,a=0.5; [unroll] for(int i=0;i<4;i++){ s+=a*vnoise(p); p*=2.03; a*=0.5;} return s; }
            float3x3 rotY(float a){ float c=cos(a),s=sin(a); return float3x3(c,0,s, 0,1,0, -s,0,c); }

            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 positionOS:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorLow,_ColorMid,_ColorHigh;
                half _Emission,_NoiseScale,_RiseSpeed,_SwirlSpeed,_Density,_Threshold,_EdgeFade,_Steps;
                half _AngleScroll,_ShapeNoise;
                half _DetailNoise,_DetailScale,_DensityBoost;
                half _StretchY,_TopTaper,_MirrorX;
                float4 _FlameTex_ST;
            CBUFFER_END

            TEXTURE2D(_FlameTex); SAMPLER(sampler_FlameTex);

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.positionHCS=TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS=IN.positionOS.xyz;
                return OUT;
            }

            half3 ramp(float v){
                v=saturate(v);
                return (v<0.5) ? lerp(_ColorLow.rgb,_ColorMid.rgb,v*2.0)
                               : lerp(_ColorMid.rgb,_ColorHigh.rgb,(v-0.5)*2.0);
            }

            half4 frag(Varyings IN):SV_Target{
                float R = 0.5;                                  // base radius in OS
                float3 camOS = TransformWorldToObject(GetCameraPositionWS());
                float3 dir = normalize(IN.positionOS - camOS);

                // ray-ellipsoid intersect: thin the XZ so the drop body stays INSIDE the unit-sphere
                // container (no clipping). Overall vertical length comes from the object's Y scale.
                float3 sc = float3(_StretchY, 1.0, _StretchY);  // >1 narrows XZ -> taller-looking drop
                float3 co = camOS * sc;
                float3 dd = dir   * sc;                         // NOT renormalized: keeps t in real units
                float a = dot(dd, dd);
                float b = 2.0*dot(co, dd);
                float c = dot(co, co) - R*R;
                float disc = b*b - 4.0*a*c;
                if (disc <= 0) discard;
                float sq = sqrt(disc);
                float inv2a = 0.5 / a;
                float t0 = max(0.0, (-b - sq)*inv2a);
                float t1 = (-b + sq)*inv2a;
                if (t1 <= t0) discard;

                int steps = (int)_Steps;
                float dt = (t1 - t0) / steps;
                float t = t0;
                float swirl = _Time.y * _SwirlSpeed * (1.0 - _MirrorX);   // rotation breaks symmetry -> off when mirrored
                float3 flow = float3(0, -_Time.y * _RiseSpeed, 0);

                // view-aligned symmetry: fold across a plane that contains the up axis (local +Y)
                // and faces the camera, so left-right symmetry holds from ANY angle (projectile use)
                float3 viewOS = normalize(-camOS);             // camera -> center, constant per draw
                float3 foldN = cross(float3(0,1,0), viewOS);   // screen-horizontal axis in OS
                float foldLen = length(foldN);
                foldN = (foldLen > 1e-3) ? foldN/foldLen : float3(1,0,0);
                float foldAmt = _MirrorX * step(1e-3, foldLen);

                half3 accum = 0;
                half acc = 0;
                [loop] for (int s = 0; s < steps; s++)
                {
                    float3 p = camOS + dir * t;                 // OS, within the thinned sphere (drop body)
                    float h = saturate(p.y/R*0.5 + 0.5);        // 0 bottom .. 1 top
                    float horiz = length(p.xz) * _StretchY / R; // 0..1 over the thinned radius
                    // teardrop: allowed radius narrows toward the top => pointed tip
                    float radLimit = 1.0 - smoothstep(1.0 - _TopTaper, 1.0, h);

                    // flame silhouette from profile texture (u=angle, v=height) => jagged tongues
                    // fold across the camera-facing plane => left-right (screen) symmetry from any view
                    float sdist = dot(p, foldN);
                    float3 pf = p + foldN * (abs(sdist) - sdist) * foldAmt;
                    float ang = atan2(pf.z, pf.x) * 0.1591549 + 0.5;
                    ang += _Time.y * _AngleScroll * (1.0 - _MirrorX);       // scroll breaks symmetry -> off when mirrored
                    ang += (fbm(pf*3.0 + flow) - 0.5) * _ShapeNoise;        // organic breakup (mirrored)
                    float maxr = SAMPLE_TEXTURE2D_LOD(_FlameTex, sampler_FlameTex, float2(ang, h), 0).r;
                    float edge = max(maxr * radLimit, 1e-3);                // taper allowed radius toward the tip
                    float shape = smoothstep(edge, edge*0.72, horiz);       // crisper tongue edge

                    float3 q = mul(rotY(swirl), pf);
                    q.y *= 0.5;                                  // vertical stretch => tall upward flames
                    float3 sp = q * _NoiseScale + flow;
                    float n = fbm(sp);
                    // second faster-rising octave for licking motion
                    n = n*0.7 + fbm(sp*2.1 + float3(0,-_Time.y*_RiseSpeed*1.8,0))*0.3;
                    // high-frequency ridged turbulence => wispy, noisy detail
                    float turb = abs(fbm(sp*_DetailScale + float3(0,-_Time.y*_RiseSpeed*2.4,0))*2.0 - 1.0);
                    n += (turb - 0.5) * _DetailNoise * 0.7;
                    float vgrad = h;
                    float dens = saturate((n - _Threshold) / (1.0 - _Threshold)) * shape;
                    // upward bias: flames lick upward
                    dens *= lerp(0.9, 1.3, vgrad);

                    if (dens > 0.001)
                    {
                        float a = saturate(dens * _Density * _DensityBoost * dt * (float)steps / 28.0);
                        half3 emis = ramp(dens*0.6 + vgrad*0.4) * _Emission;
                        accum += (1.0 - acc) * emis * a;
                        acc   += (1.0 - acc) * a;
                        if (acc > 0.97) break;
                    }
                    t += dt;
                }
                if (acc <= 0.001) discard;
                return half4(accum, acc);
            }
            ENDHLSL
        }
    }
    CustomEditor "FlameShaderGUI"
    Fallback Off
}
