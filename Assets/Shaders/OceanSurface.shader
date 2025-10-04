Shader "Ocean/DisplaceMinimalURP"
{
    Properties
    {
        _Color       ("Tint", Color) = (0.15,0.4,0.7,1)
        _HeightTex   ("Height RT", 2D) = "black" {}
        _TileLength  ("Tile Length (m)", Float) = 512
        _Amplitude   ("Height Scale (m)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags{ "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _TileLength;
                float  _Amplitude;
            CBUFFER_END

            TEXTURE2D(_HeightTex);
            SAMPLER(sampler_HeightTex);

            struct Attributes {
                float4 positionOS : POSITION;
            };
            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            float SampleHeightWS(float2 worldXZ)
            {
                float2 uv = frac(worldXZ / _TileLength);
                uv = (uv < 0) ? uv + 1.0 : uv;            // keep [0,1)
                return SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, uv, 0).r;
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS.y += SampleHeightWS(posWS.xz) * _Amplitude;
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag (Varyings v) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
