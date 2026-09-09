Shader "Unknow/Dungeon Edge Line"
{
    // 노드를 잇는 통로. LineRenderer 가 기본 머티리얼을 쓰면 납작한 띠 하나라
    // 지도가 도형 연결선처럼 보인다. 가장자리를 흐리고 빛이 흘러가게 해서
    // "통로"로 읽히게 한다.
    //
    // LineRenderer 는 UV 를 길이 방향 U, 두께 방향 V 로 준다(textureMode = Stretch 기준).
    // 그래서 V 로 굵기 감쇠를, U 로 흐름을 만든다.
    Properties
    {
        _Color ("Line Color", Color) = (0.86, 0.62, 0.30, 1)
        _CoreColor ("Core Color", Color) = (1, 0.90, 0.70, 1)
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.55
        _CoreWidth ("Core Width", Range(0.01, 1)) = 0.30
        _FlowSpeed ("Flow Speed", Range(0, 4)) = 0.6
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.35
        _FlowTiling ("Flow Tiling", Range(0.5, 16)) = 3
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
            Name "DungeonEdgeLine"
            Tags { "LightMode" = "Universal2D" }

            // 더해서 그린다. 통로는 빛이지 물체가 아니라, 뒤의 바닥이 비쳐야 자연스럽다.
            Blend SrcAlpha One
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _CoreColor;
                float _EdgeSoftness;
                float _CoreWidth;
                float _FlowSpeed;
                float _FlowStrength;
                float _FlowTiling;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 두께 방향 가운데가 1, 가장자리가 0. 가장자리를 흐리게 만드는 값이다.
                float across = abs(IN.uv.y - 0.5) * 2.0;
                float body = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, across);

                // 가운데를 한 겹 더 밝게 — 심지가 있어야 선이 얇아 보여도 또렷하다.
                float core = 1.0 - smoothstep(0.0, max(_CoreWidth, 0.001), across);

                // 길이 방향으로 흐르는 빛. 완전히 껐다 켜지 않고 밝기만 흔든다 —
                // 끊기면 통로가 아니라 점선으로 읽힌다.
                float flow = sin((IN.uv.x * _FlowTiling - _Time.y * _FlowSpeed) * TWO_PI) * 0.5 + 0.5;
                float pulse = lerp(1.0, flow, saturate(_FlowStrength));

                half3 rgb = lerp(_Color.rgb, _CoreColor.rgb, core);
                half alpha = body * _Color.a * IN.color.a * pulse;

                return half4(rgb * IN.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
