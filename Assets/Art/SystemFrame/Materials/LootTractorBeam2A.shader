Shader "StarFrontier/LootTractorBeam2A"
{
    Properties
    {
        _BeamColor ("Glow color", Color) = (0.12, 0.65, 1, 0.55)
        _CoreColor ("Core color", Color) = (0.72, 0.96, 1, 0.95)
        _BeamPhase ("Flow phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            float4 _BeamColor;
            float4 _CoreColor;
            float _BeamPhase;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float across = abs(input.uv.y * 2.0 - 1.0);
                float glow = pow(saturate(1.0 - across), 2.0);
                float core = 1.0 - smoothstep(0.03, 0.18, across);
                // Positive phase moves highlights from loot (u=1) toward ship (u=0).
                float pulse = pow(0.5 + 0.5 * cos((input.uv.x * 5.0 + _BeamPhase) * 6.283185), 8.0);
                float tips = smoothstep(0.0, 0.04, input.uv.x) *
                    smoothstep(0.0, 0.04, 1.0 - input.uv.x);
                float3 color = lerp(_BeamColor.rgb, _CoreColor.rgb, core);
                float alpha = saturate(glow * _BeamColor.a + core * _CoreColor.a);
                alpha *= tips * (0.7 + pulse * 0.3);
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
