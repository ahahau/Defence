Shader "Unknow/Dungeon Room Glow"
{
    // 방 바닥 위에 한 겹 덧그리는 횃불빛.
    //
    // 바닥 자체의 머티리얼은 건드리지 않는다. 그쪽은 2D 조명을 받는 재질이라 갈아치우면
    // 방이 통째로 조명에서 빠진다. 그래서 이건 따로 얹는 층이고, 더해서 그리기만 한다.
    //
    // 방 그림의 알파를 마스크로 쓴다. 그러지 않으면 사각형 빛이 방 밖으로 삐져나가
    // 바닥이 아니라 창문처럼 보인다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (1, 0.72, 0.38, 1)
        _Strength ("Strength", Range(0, 2)) = 0.5
        _CenterSize ("Center Size", Range(0.05, 1.2)) = 0.55
        _EdgeSoftness ("Edge Softness", Range(0.05, 1.5)) = 0.75
        _FlickerDepth ("Flicker Depth", Range(0, 1)) = 0.22
        _FlickerSpeed ("Flicker Speed", Range(0, 8)) = 2.3
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
            Name "DungeonRoomGlow"
            Tags { "LightMode" = "Universal2D" }

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _GlowColor;
                float _Strength;
                float _CenterSize;
                float _EdgeSoftness;
                float _FlickerDepth;
                float _FlickerSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 방 그림이 있는 자리에만 빛을 얹는다.
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;

                // 가운데가 밝고 가장자리로 갈수록 어두워진다. 방이 파여 있는 것처럼 읽힌다.
                float2 centered = IN.uv - 0.5;
                float dist = length(centered) * 2.0;
                float pool = 1.0 - smoothstep(_CenterSize, _CenterSize + _EdgeSoftness, dist);

                // 횃불은 규칙적으로 깜빡이지 않는다. 주기가 다른 둘을 겹쳐 박자를 흐트러뜨린다.
                float t = _Time.y * _FlickerSpeed;
                float flicker = sin(t) * 0.6 + sin(t * 1.73 + 1.3) * 0.4;
                float brightness = 1.0 + flicker * _FlickerDepth;

                half alpha = mask * pool * _Strength * brightness * _GlowColor.a * IN.color.a;
                return half4(_GlowColor.rgb * IN.color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
