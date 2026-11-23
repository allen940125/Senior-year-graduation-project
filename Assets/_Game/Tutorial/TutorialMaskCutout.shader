Shader "Custom/UI/TutorialMaskCutout"
{
    Properties
    {
        _Color ("Mask Color", Color) = (0, 0, 0, 0.65)
        _HoleRect ("Hole Rect (minX, minY, maxX, maxY)", Vector) = (0, 0, 0, 0)
        _Softness ("Edge Softness", Range(0.001, 0.05)) = 0.01
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
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
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _HoleRect;
                float _Softness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 若 _HoleRect 為全 0，代表不挖洞（全螢幕壓黑）
                if (_HoleRect.z <= _HoleRect.x || _HoleRect.w <= _HoleRect.y)
                {
                    return _Color;
                }

                // 計算像素到目標矩形框的距離
                float2 d = max(_HoleRect.xy - input.uv, input.uv - _HoleRect.zw);
                float dist = length(max(d, 0.0));
                float alphaMask = smoothstep(0.0, _Softness, dist);

                return half4(_Color.rgb, _Color.a * alphaMask);
            }
            ENDHLSL
        }
    }
}