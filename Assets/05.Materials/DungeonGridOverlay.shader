Shader "Unknow/Dungeon Grid Overlay"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0.62, 0.48, 0.88, 0.34)
        _CenterColor ("Core Slot Color", Color) = (0.98, 0.44, 0.12, 0.34)
        _GridSize ("Grid Size", Vector) = (6, 6, 0, 0)
        _CentralRect ("Central Slot", Vector) = (0.3333, 0.3333, 0.6667, 0.6667)
        _LineWidth ("Line Width", Range(0.004, 0.12)) = 0.025
        _GlowStrength ("Ember Pulse", Range(0, 0.35)) = 0.10
        [NoScaleOffset] _CellMask ("Open Cell Mask (1 texel per cell)", 2D) = "white" {}
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
            Name "DungeonGridOverlay"
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

            TEXTURE2D(_CellMask);

            // 1 = 열린 칸(방 바닥, 칸막이 없음). 격자 밖은 닫힌 칸이다.
            float CellOpen(int2 cell, float2 gridSize)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= (int)gridSize.x || cell.y >= (int)gridSize.y)
                    return 0.0;
                return LOAD_TEXTURE2D(_CellMask, cell).r > 0.5 ? 1.0 : 0.0;
            }

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

                // 열린 칸에 닿은 경계만 그린다. 방 모양 밖·기둥·칸막이 칸끼리의 선은 지운다.
                int2 cell = (int2)min(floor(uv * gridSize), gridSize - 1.0);
                int2 side = int2(cellUv.x < 0.5 ? -1 : 1, cellUv.y < 0.5 ? -1 : 1);
                float open = CellOpen(cell, gridSize);
                verticalLine *= max(open, CellOpen(cell + int2(side.x, 0), gridSize));
                horizontalLine *= max(open, CellOpen(cell + int2(0, side.y), gridSize));

                // Break long spreadsheet-like lines into corner brackets. The grid
                // stays readable for placement without covering the stone artwork.
                float verticalSegment = 1.0 - smoothstep(0.25, 0.39, cellEdge.y);
                float horizontalSegment = 1.0 - smoothstep(0.25, 0.39, cellEdge.x);
                float gridLine = max(verticalLine * verticalSegment, horizontalLine * horizontalSegment);

                float hasCenter = step(_CentralRect.x + 0.0001, _CentralRect.z)
                                  * step(_CentralRect.y + 0.0001, _CentralRect.w);
                float centerInside = RectMask(uv, _CentralRect) * hasCenter;
                gridLine *= 1.0 - centerInside;

                // Signed-distance outline around the reserved dungeon core slot.
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

                // 선택 외곽은 사각 틀이 아니라 실제 열린 칸의 경계를 잇는다.
                float2 borderAa = max(fwidth(cellEdge) * 1.5, 0.0004);
                float2 borderLine = 1.0 - smoothstep(_LineWidth * 1.8, _LineWidth * 1.8 + borderAa, cellEdge);
                float outerBorder = max(
                    borderLine.x * abs(open - CellOpen(cell + int2(side.x, 0), gridSize)),
                    borderLine.y * abs(open - CellOpen(cell + int2(0, side.y), gridSize)));
                float radial = saturate(1.0 - length((uv - 0.5) * 1.25));
                float emberPulse = 1.0 + sin(_Time.y * 1.15 + (uv.x + uv.y) * 4.0) * _GlowStrength;
                float cellCore = pow(saturate(1.0 - length((cellUv - 0.5) * 2.0)), 4.0);

                float normalAlpha = (gridLine * lerp(0.56, 1.0, radial) + outerBorder * 1.2)
                                    * _GridColor.a * emberPulse;
                normalAlpha += cellCore * _GridColor.a * 0.045 * (1.0 - centerInside) * open;

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
