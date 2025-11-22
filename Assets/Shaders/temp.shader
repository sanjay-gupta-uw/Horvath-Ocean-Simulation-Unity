Shader "Ocean/DisplaceURP_Lit_Simple"
{
    Properties
    {
        _HeightMap ("Height RT", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _AirBubblesColor("Air Bubbles Color", Color) = (0.3475881, 0.513974, 0.6886792, 1)
        _WaterScatterColor("Water Scatter Color", Color) = (0.5589178, 0.7547169, 0.7188664, 1)
        [HDR] _SpecColor ("Specular Color", Color) = (191, 138, 101, 255)
        _Shininess ("Shininess", Float) = 50
        _Reflectivity("Reflectivity", Range(0, 2)) = 0.4

        _DensityOfWaterBubbles("Density of Water Bubbles", Float) = 0.45
        _Tweak1("Tweak1", Float) = 0.5
        _Tweak2("Tweak2", Float) = -0.1
        _Tweak3("Tweak3", Float) = -0.1
        _Amplitude("Amplitude", Float) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_HeightMap);   SAMPLER(sampler_HeightMap);
            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterScatterColor;
                float4 _AirBubblesColor;
                float4 _SpecColor;
                float  _Shininess;
                float  _Reflectivity;
                float  _DensityOfWaterBubbles;
                float  _Tweak1;
                float  _Tweak2;
                float  _Tweak3;
                float  _Amplitude;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS : POSITION;
                float2 uv    : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float3 posWS : TEXCOORD0;
                float2 uv    : TEXCOORD1;
            };

            float DotClamped(float3 a, float3 b)
            {
                return max(0.0, dot(a, b));
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posOS = IN.posOS.xyz;

                // sample height in vertex stage
                float h = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, IN.uv, 0).r;
                posOS.y = h * _Amplitude;

                OUT.posWS = TransformObjectToWorld(posOS);
                OUT.posCS = TransformWorldToHClip(OUT.posWS);
                OUT.uv    = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 posWorld = IN.posWS;

                float3 viewDir = normalize(GetWorldSpaceViewDir(posWorld));

                // main directional light in URP
                Light mainLight = GetMainLight();
                float3 sunDirection = normalize(mainLight.direction);
                float3 lightColor   = mainLight.color;

                // normal from normal map (treat as world-space for now)
                float3 nTex = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv).rgb;
                float3 normal = normalize(nTex * 2.0 - 1.0);

                float part3 = _Tweak3 * normal.x;
                float3 ambient = part3 * _WaterScatterColor.rgb * lightColor
                               + _DensityOfWaterBubbles * _AirBubblesColor.rgb * lightColor;

                float fresnel = pow(1.0 - max(dot(viewDir, normal), 0.15), 5.0);

                float3 reflectDir = reflect(-sunDirection, normal);
                float spec = pow(max(dot(viewDir, reflectDir), 0.0), _Shininess);
                float3 specular = lightColor * (spec * _SpecColor.rgb) * fresnel;

                float part1 = _Tweak1 * max(0, posWorld.y)
                              * pow(DotClamped(sunDirection, -viewDir), 4.0)
                              * pow(0.5 - 0.5 * dot(sunDirection, normal), 3.0);
                float part2 = _Tweak2 * pow(DotClamped(viewDir, normal), 2.0);

                float3 scatter = (1 - fresnel) * (part1 + part2) * _WaterScatterColor.rgb * lightColor;

                float3 output = ambient + scatter + specular;

                return half4(output, 1);
            }
            ENDHLSL
        }
    }
}

