Shader "Unknow/Nest Grid Overlay"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0.62, 0.48, 0.88, 0.34)
        _CenterColor ("Nest Slot Color", Color) = (0.98, 0.44, 0.12, 0.34)
        _GridSize ("Grid Size", Vector) = (6, 6, 0, 0)
        _CentralRect ("Central Slot", Vector) = (0.3333, 0.3333, 0.6667, 0.6667)
        _LineWidth ("Line Width", Range(0.004, 0.12)) = 0.025
        _GlowStrength ("Ember Pulse", Range(0, 0.35)) = 0.10
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "NestGridOverlay"
            Tags { "LightMode" = "Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GridColor;
                half4 _CenterColor;
                float4 _GridSize;
                float4 _CentralRect;
                float _LineWidth;
                float _GlowStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float RectMask(float2 uv, float4 rect)
            {
                float2 lower = step(rect.xy, uv);
                float2 upper = step(uv, rect.zw);
                return lower.x * lower.y * upper.x * upper.y;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = saturate(input.uv);
                float2 gridSize = max(_GridSize.xy, 1.0);

                // Analytic, anti-aliased cell borders. Unlike LineRenderer geometry,
                // this keeps a stable apparent thickness while the camera zooms.
                float2 cellUv = frac(uv * gridSize);
                float2 cellEdge = min(cellUv, 1.0 - cellUv);
                float2 lineAa = max(fwidth(cellEdge) * 1.05, 0.0004);
                float verticalLine = 1.0 - smoothstep(_LineWidth, _LineWidth + lineAa.x, cellEdge.x);
                float horizontalLine = 1.0 - smoothstep(_LineWidth, _LineWidth + lineAa.y, cellEdge.y);

                // Break long spreadsheet-like lines into corner brackets. The grid
                // stays readable for placement without covering the stone artwork.
                float verticalSegment = 1.0 - smoothstep(0.25, 0.39, cellEdge.y);
                float horizontalSegment = 1.0 - smoothstep(0.25, 0.39, cellEdge.x);
                float gridLine = max(verticalLine * verticalSegment, horizontalLine * horizontalSegment);

                float hasCenter = step(_CentralRect.x + 0.0001, _CentralRect.z)
                                  * step(_CentralRect.y + 0.0001, _CentralRect.w);
                float centerInside = RectMask(uv, _CentralRect) * hasCenter;
                gridLine *= 1.0 - centerInside;

                // Signed-distance outline around the reserved dragon nest slot.
                float2 center = (_CentralRect.xy + _CentralRect.zw) * 0.5;
                float2 halfSize = max((_CentralRect.zw - _CentralRect.xy) * 0.5, 0.0001);
                float2 q = abs(uv - center) - halfSize;
                float centerSdf = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
                float centerAa = max(fwidth(centerSdf) * 1.5, 0.0005);
                float centerBorderWidth = _LineWidth / min(gridSize.x, gridSize.y) * 1.8;
                float centerBorder = (1.0 - smoothstep(
                    centerBorderWidth,
                    centerBorderWidth + centerAa,
                    abs(centerSdf))) * hasCenter;

                float2 outerDistance2 = min(uv, 1.0 - uv);
                float outerDistance = min(outerDistance2.x, outerDistance2.y);
                float outerWidth = _LineWidth / min(gridSize.x, gridSize.y) * 1.45;
                float outerBorder = 1.0 - smoothstep(outerWidth, outerWidth + fwidth(outerDistance), outerDistance);

                float radial = saturate(1.0 - length((uv - 0.5) * 1.25));
                float emberPulse = 1.0 + sin(_Time.y * 1.15 + (uv.x + uv.y) * 4.0) * _GlowStrength;
                float cellCore = pow(saturate(1.0 - length((cellUv - 0.5) * 2.0)), 4.0);

                float normalAlpha = (gridLine * lerp(0.56, 1.0, radial) + outerBorder * 0.55)
                                    * _GridColor.a * emberPulse;
                normalAlpha += cellCore * _GridColor.a * 0.045 * (1.0 - centerInside);

                float centerFill = centerInside * _CenterColor.a * (0.18 + radial * 0.12);
                float centerAlpha = centerBorder * _CenterColor.a + centerFill;
                float alpha = saturate(normalAlpha + centerAlpha);

                half3 color = lerp(_GridColor.rgb, _CenterColor.rgb,
                    saturate(centerBorder + centerInside * 0.32));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
