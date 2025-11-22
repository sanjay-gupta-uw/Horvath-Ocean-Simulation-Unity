Shader "Ocean/DisplaceURP_Modular"
{
    Properties
    {
        _HeightMap          ("Height RT", 2D) = "white" {}
        _NormalMap          ("Normal Map", 2D) = "bump" {}

        _AirBubblesColor    ("Air Bubbles Color", Color)   = (0.35, 0.51, 0.69, 1)
        _WaterScatterColor  ("Water Scatter Color", Color) = (0.56, 0.75, 0.72, 1)
        [HDR]_SpecColor     ("Specular Color", Color)      = (1, 1, 1, 1)

        _Shininess          ("Shininess", Float)           = 50
        _Reflectivity       ("Reflectivity", Range(0, 2))  = 0.4
        _BaseFresnel        ("Base Fresnel F0", Range(0,1))= 0.02

        _DensityOfWaterBubbles ("Density of Water Bubbles", Float) = 0.45
        _Tweak1             ("Scatter Tweak 1", Float)     = 0.5
        _Tweak2             ("Scatter Tweak 2", Float)     = -0.1
        _Tweak3             ("Ambient Tweak", Float)       = -0.1

        _Amplitude          ("Displacement Amplitude", Float) = 0.2
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"

            TEXTURE2D(_HeightMap);       SAMPLER(sampler_HeightMap);
            TEXTURE2D(_NormalMap);       SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterScatterColor;
                float4 _AirBubblesColor;
                float4 _SpecColor;

                float  _Shininess;
                float  _Reflectivity;
                float  _BaseFresnel;

                float  _DensityOfWaterBubbles;
                float  _Tweak1;
                float  _Tweak2;
                float  _Tweak3;

                float  _Amplitude;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            // --------------------
            // Helper functions
            // --------------------

            float DotClamped(float3 a, float3 b)
            {
                return max(0.0, dot(a, b));
            }

            // World-space normal from normal map (assume encoded in [0,1] and approx world-space)
            float3 GetWorldNormal(float2 uv)
            {
                float3 nTex = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv).rgb;
                return normalize(nTex * 2.0 - 1.0);
            }

            // Fresnel using URP's Schlick function
            float ComputeFresnel(float3 N, float3 V)
            {
                half cosNV = saturate(dot(N, V));
                half3 F0   = _BaseFresnel.xxx;
                half3 F    = F_Schlick(F0, cosNV);
                return (F.r + F.g + F.b) * (1.0h / 3.0h); // scalar average
            }

            // Simple Lambert diffuse using "water color"
            float3 ComputeDiffuse(float3 N, float3 L, float3 lightColor)
            {
                float NdotL = DotClamped(N, L);
                float3 baseWater = _WaterScatterColor.rgb;
                return baseWater * lightColor * NdotL;
            }

            // Your custom ambient / bubbles term
            float3 ComputeAmbient(float3 N, float3 lightColor)
            {
                float3 term =
                    _Tweak3 * N.x * _WaterScatterColor.rgb * lightColor +
                    _DensityOfWaterBubbles * _AirBubblesColor.rgb * lightColor;

                return term;
            }

            // Your custom subsurface-style scattering
            float3 ComputeScatter(float3 N, float3 V, float3 L, float3 lightColor, float3 posWS, float fresnel)
            {
                float part1 =
                    _Tweak1 * max(0.0, posWS.y) *
                    pow(DotClamped(L, -V), 4.0) *
                    pow(0.5 - 0.5 * dot(L, N), 3.0);

                float part2 =
                    _Tweak2 * pow(DotClamped(V, N), 2.0);

                float3 scatter =
                    (1.0 - fresnel) * (part1 + part2) * _WaterScatterColor.rgb * lightColor;

                return scatter;
            }

            // Specular from main light
            float3 ComputeSpecular(float3 N, float3 V, float3 L, float3 lightColor, float fresnel)
            {
                float3 reflectDir = reflect(-L, N);
                float  specPow    = pow(max(dot(V, reflectDir), 0.0), _Shininess);
                float3 specular   = lightColor * _SpecColor.rgb * specPow * fresnel;
                return specular;
            }

            // Environment reflection from reflection probe / skybox
            float3 ComputeEnvReflection(float3 N, float3 posWS, float fresnel)
            {
                float3 I = normalize(posWS - _WorldSpaceCameraPos);
                float3 R = reflect(I, N);

                half4 skySample       = SAMPLE_TEXTURECUBE(unity_SpecCube0, samplerunity_SpecCube0, R);
                half3 reflectionColor = DecodeHDREnvironment(skySample, unity_SpecCube0_HDR);

                return fresnel * _Reflectivity * reflectionColor;
            }

            // "Refraction" / underwater color approximation
            // (not true ray-traced refraction, just a body color term you can expand later)
            float3 ComputeRefractionColor(float3 N, float3 V, float3 posWS)
            {
                // Heavier blue/green when looking into the water
                float viewFacing = 1.0 - saturate(dot(N, V)); // more when grazing
                float depthFactor = saturate(posWS.y * -0.01); // tweak based on height if desired

                float3 deepColor    = float3(0.0, 0.1, 0.25);
                float3 shallowColor = _WaterScatterColor.rgb;

                float3 waterBody = lerp(deepColor, shallowColor, saturate(N.y));
                return waterBody * (0.2 + 0.8 * viewFacing) * (0.3 + 0.7 * depthFactor);
            }

            // --------------------
            // Vertex / Fragment
            // --------------------

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 posOS = IN.positionOS.xyz;

                // Displace in object space by height map
                float h = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, IN.uv, 0).r;
                posOS.y += h * _Amplitude;

                float3 posWS = TransformObjectToWorld(posOS);

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv         = IN.uv;

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 posWS   = IN.positionWS;
                float3 V       = normalize(GetWorldSpaceViewDir(posWS));
                float3 N       = GetWorldNormal(IN.uv);

                Light mainLight   = GetMainLight();
                float3 L          = normalize(mainLight.direction);
                float3 lightColor = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // Core physical-ish terms
                float  fresnel   = ComputeFresnel(N, V);
                float3 ambient   = ComputeAmbient(N, lightColor);
                float3 diffuse   = ComputeDiffuse(N, L, lightColor);
                float3 scatter   = ComputeScatter(N, V, L, lightColor, posWS, fresnel);
                // float3 specular  = ComputeSpecular(N, V, L, lightColor, fresnel);
                float3 envRefl   = ComputeEnvReflection(N, posWS, fresnel);
                // float3 refrColor = ComputeRefractionColor(N, V, posWS);

                // Combine
                float3 color =
                    ambient +
                    diffuse +
                    scatter +
                    // specular ;
                    envRefl;
                    // refrColor;

                color = saturate(color);
                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }
}
