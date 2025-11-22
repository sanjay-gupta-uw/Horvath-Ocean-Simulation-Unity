Shader "Ocean/DisplaceURP_Lit_Fixed"
{
    Properties{
        _DeepColor("Deep Color", Color) = (0.05, 0.15, 0.5, 1)
            _ShallowColor("Shallow Color", Color) = (0.10, 0.40, 0.8, 1)
                _FoamColor("Foam Color", Color) = (1, 1, 1, 1)

                    _HeightTex("Height RT", 2D) = "black" {} _DispXTex("Disp X RT", 2D) = "black" {} _DispYTex("Disp Y RT", 2D) = "black" {} _NormalFoamTex("Normal + Foam RT", 2D) = "gray" {}

        _TileLength("Tile Length (m)", Float) = 512 _Amplitude("Height Scale (m)", Float) = 1 _Choppiness("Choppiness", Float) = 1

        _InvSize("1 / FFT Size", Float) = 0.0039 // e.g. 1/256; set from script

        _FresnelPower("Fresnel Power", Range(1, 8)) = 4 _SpecularExp("Specular Exp", Range(4, 128)) = 32}

    SubShader
    {
        Tags{
            "RenderType" = "Opaque"
                           "Queue" = "Geometry"
                                     "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            Name "ForwardLit" Tags{"LightMode" = "UniversalForward"}

            Cull Off

                HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

                    CBUFFER_START(UnityPerMaterial)
                        float4 _DeepColor;
            float4 _ShallowColor;
            float4 _FoamColor;

            float _TileLength;
            float _Amplitude;
            float _Choppiness;
            float _InvSize; // = 1.0 / FFT size

            float _FresnelPower;
            float _SpecularExp;
            CBUFFER_END

            TEXTURE2D(_HeightTex);
            SAMPLER(sampler_HeightTex);
            TEXTURE2D(_DispXTex);
            SAMPLER(sampler_DispXTex);
            TEXTURE2D(_DispYTex);
            SAMPLER(sampler_DispYTex);
            TEXTURE2D(_NormalFoamTex);
            SAMPLER(sampler_NormalFoamTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float foam : TEXCOORD3;
            };

            float2 WrapUV(float2 worldXZ)
            {
                float2 uv = frac(worldXZ / _TileLength);
                uv = (uv < 0) ? uv + 1.0 : uv;
                return uv;
            }

            float SampleHeightWS(float2 worldXZ)
            {
                float2 uv = WrapUV(worldXZ);
                return SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, uv, 0).r;
            }

            float SampleDispXWS(float2 worldXZ)
            {
                float2 uv = WrapUV(worldXZ);
                return SAMPLE_TEXTURE2D_LOD(_DispXTex, sampler_DispXTex, uv, 0).r;
            }

            float SampleDispYWS(float2 worldXZ)
            {
                float2 uv = WrapUV(worldXZ);
                return SAMPLE_TEXTURE2D_LOD(_DispYTex, sampler_DispYTex, uv, 0).r;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float2 xz = posWS.xz;

                // Sample FFT displacements
                float dz = SampleHeightWS(xz);
                float dx = SampleDispXWS(xz);
                float dy = SampleDispYWS(xz);

                // World-space scaling: meters per FFT pixel
                float metersPerPixel = _TileLength * _InvSize;

                posWS.x += dx * metersPerPixel * _Choppiness;
                posWS.z += dy * metersPerPixel * _Choppiness;
                posWS.y += dz * _Amplitude;

                float2 uv = WrapUV(posWS.xz);

                // Sample precomputed world-space normal + foam
                float4 nf = SAMPLE_TEXTURE2D_LOD(_NormalFoamTex, sampler_NormalFoamTex, uv, 0);
                float3 nWS = normalize(nf.xyz * 2.0 - 1.0);

                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                OUT.normalWS = nWS;
                OUT.uv = uv;
                OUT.foam = nf.a;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Main directional light
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);

                float NdotL = saturate(dot(N, L));
                float3 diffuse = NdotL * mainLight.color.rgb;

                float spec = pow(saturate(dot(N, H)), _SpecularExp) * NdotL;
                float3 specular = spec * mainLight.color.rgb;

                // Simple ambient term
                float3 ambient = 0.03;

                // Base water color varies with slope (N.y)
                float up = saturate(N.y);
                float3 baseCol = lerp(_DeepColor.rgb, _ShallowColor.rgb, up);

                // Fresnel
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                float3 water =
                    baseCol * (ambient + diffuse) +
                    specular +
                    fresnel * baseCol * 0.5;

                // Foam from precomputed foam mask
                float foamMask = saturate(IN.foam);
                float3 color = lerp(water, _FoamColor.rgb, foamMask);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
