Shader "Custom/MovingDashedLine"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _DashLength("Dash Length", Float) = 0.35
        _GapLength("Gap Length", Float) = 0.2
        _ScrollSpeed("Scroll Speed", Float) = 1
        _EdgeFade("Edge Fade", Range(0.001, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "UniversalMaterialType"="Unlit"
        }

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _DashLength;
                float _GapLength;
                float _ScrollSpeed;
                float _EdgeFade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float widthCoord = abs(input.uv.y * 2.0 - 1.0);
                float edgeMask = saturate(1.0 - smoothstep(1.0 - _EdgeFade, 1.0, widthCoord));
                float cycleLength = max(0.0001, _DashLength + _GapLength);
                float scrolledCoord = input.uv.x - _Time.y * _ScrollSpeed;
                float dashCoord = frac(scrolledCoord / cycleLength) * cycleLength;
                float dashMask = step(dashCoord, _DashLength);
                float alpha = saturate(_BaseColor.a * edgeMask * dashMask);
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DefaultUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _DashLength;
                float _GapLength;
                float _ScrollSpeed;
                float _EdgeFade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float widthCoord = abs(input.uv.y * 2.0 - 1.0);
                float edgeMask = saturate(1.0 - smoothstep(1.0 - _EdgeFade, 1.0, widthCoord));
                float cycleLength = max(0.0001, _DashLength + _GapLength);
                float scrolledCoord = input.uv.x - _Time.y * _ScrollSpeed;
                float dashCoord = frac(scrolledCoord / cycleLength) * cycleLength;
                float dashMask = step(dashCoord, _DashLength);
                float alpha = saturate(_BaseColor.a * edgeMask * dashMask);
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
