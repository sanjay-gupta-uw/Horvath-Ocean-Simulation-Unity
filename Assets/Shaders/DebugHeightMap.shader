Shader "Unlit/DebugHeightMap"
{
    Properties{
        _HeightTex ("Height RT", 2D) = "black" {}
        _Scale ("Scale", Float) = 0.1     // multiply height (to fit 0..1)
        _Bias  ("Bias",  Float) = 0.5     // add after scaling (center to mid-gray)
        _Channel ("Channel (0=R,1=G,2=B,3=A)", Int) = 0
    }
    SubShader{
        Tags{ "Queue"="Transparent" "RenderType"="Opaque" }
        Pass{
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            sampler2D _HeightTex;
            float4 _HeightTex_TexelSize;
            float _Scale, _Bias;
            int _Channel;

            struct appdata { float3 pos:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_Position; float2 uv:TEXCOORD0; };

            v2f vert(appdata v){
                v2f o; o.pos = float4(v.pos,1); o.uv = v.uv; return o;
            }

            float pick(float4 c, int i){
                if(i==0) return c.r;
                if(i==1) return c.g;
                if(i==2) return c.b;
                return c.a;
            }

            float4 frag(v2f i):SV_Target{
                float4 h = tex2D(_HeightTex, i.uv);
                float v = pick(h, _Channel);   // your IFFT height channel
                // map to 0..1 for viewing
                float g = saturate(v * _Scale + _Bias);
                return float4(g,g,g,1);
            }
            ENDHLSL
        }
    }
}
