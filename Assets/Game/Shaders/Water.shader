// Stylised-realistic animated water for URP (mobile friendly):
// analytic multi-directional waves for normals, Fresnel sky reflection,
// sharp sun glints, depth-coloured body and distance fog.
Shader "StreetRush/Water"
{
    Properties
    {
        _BaseMap("Detail Texture", 2D) = "white" {}
        _ShallowColor("Shallow Color", Color) = (0.12, 0.55, 0.62, 1)
        _DeepColor("Deep Color", Color) = (0.02, 0.16, 0.28, 1)
        _WaveStrength("Wave Strength", Float) = 0.35
        _WaveSpeed("Wave Speed", Float) = 0.8
        _Smoothness("Smoothness", Range(0, 1)) = 0.93
        _TextureStrength("Texture Strength", Range(0, 1)) = 0.2
        _SunGlint("Sun Glint", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _ShallowColor;
                half4 _DeepColor;
                float _WaveStrength;
                float _WaveSpeed;
                half _Smoothness;
                half _TextureStrength;
                half _SunGlint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Gradient of a single directional sine wave.
            float2 WaveGrad(float2 xz, float2 dir, float freq, float amp, float speed, float t)
            {
                return dir * (amp * freq * cos(dot(dir, xz) * freq + t * speed));
            }

            float3 WaveNormal(float2 xz, float t)
            {
                float2 g = 0;
                g += WaveGrad(xz, normalize(float2(1.0, 0.35)), 0.21, 0.55, 1.1, t);
                g += WaveGrad(xz, normalize(float2(-0.45, 1.0)), 0.37, 0.32, 1.4, t);
                g += WaveGrad(xz, normalize(float2(0.7, -0.8)), 0.83, 0.16, 2.1, t);
                g += WaveGrad(xz, normalize(float2(-1.0, -0.25)), 1.65, 0.08, 2.9, t);
                g += WaveGrad(xz, normalize(float2(0.2, 1.0)), 3.10, 0.035, 3.7, t);
                g *= _WaveStrength;
                return normalize(float3(-g.x, 1.0, -g.y));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float3 n = WaveNormal(i.positionWS.xz, t);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // Distance flattens the waves (avoids shimmering far away).
                float dist = distance(_WorldSpaceCameraPos.xyz, i.positionWS);
                n = normalize(lerp(n, float3(0, 1, 0), saturate(dist / 900.0) * 0.7));

                half ndv = saturate(dot(n, v));
                half fresnel = 0.03 + 0.97 * pow(1.0 - ndv, 5.0);
                fresnel = saturate(fresnel * 1.15);

                float3 r = reflect(-v, n);
                half3 reflection = GlossyEnvironmentReflection(r, 1.0 - _Smoothness, 1.0);

                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv + float2(t * 0.012, t * 0.007)).rgb;
                half3 body = lerp(_DeepColor.rgb, _ShallowColor.rgb, ndv * 0.8);
                body *= lerp(1.0, tex * 1.5, _TextureStrength);

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = lerp(0.35, 1.0, light.shadowAttenuation);
                half3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), 520.0) * _SunGlint * shadow;
                half3 lit = body * (saturate(dot(n, light.direction)) * light.color * 0.55 * shadow + SampleSH(n) * 0.9);

                half3 color = lerp(lit, reflection, fresnel) + spec * light.color;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
