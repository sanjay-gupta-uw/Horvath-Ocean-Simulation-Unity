Shader "K/Vis"
{
    Properties
    {
        _KTex   ("K Texture (R=kx, G=ky, B=|k|)", 2D) = "black" {}
        _Channel("Channel (0=kx, 1=ky, 2=|k|)", Float) = 0
        _Range  ("Range (maps +/- for kx/ky; scale for |k|)", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _KTex;
            float _Channel;
            float _Range;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.texcoord.xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 k = tex2D(_KTex, i.uv);
                float v;
                if      (_Channel < 0.5) v = k.r;         // kx
                else if (_Channel < 1.5) v = k.g;         // ky
                else if (_Channel < 2.5) v = k.b;         // |k|
                else                     v = k.a;         // omega

                // Map to 0..1 for display.
                // For signed (kx/ky): 0.5 + 0.5 * tanh(v/_Range)
                // For |k|:            saturate(v/_Range)
                float g;
                if (_Channel < 1.5)
                    g = 0.5 + 0.5 * tanh(v / max(_Range, 1e-5));
                else
                    g = saturate(v / max(_Range, 1e-5));

                return float4(g, g, g, 1);
            }
            ENDHLSL
        }
    }
}
