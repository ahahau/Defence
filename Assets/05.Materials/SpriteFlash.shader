// 맞은 캐릭터 위에 덮는 단색 실루엣. 그림의 알파만 쓰고 색은 SpriteRenderer.color(정점 색)로 칠한다.
// 기본 스프라이트 셰이더는 색을 곱하므로 흰색으로 칠해도 밝아지지 않는다 — 그래서 따로 둔다(SpriteFlashOverlay).
Shader "Defence/Sprite Flash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.color = input.color;
            output.uv = input.uv;
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
            return half4(input.color.rgb, alpha * input.color.a);
        }
        ENDHLSL

        Pass
        {
            Name "SpriteFlash2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    FallBack Off
}
