// Three-stop vertical gradient skybox.
//
// Unity's procedural sky is physically derived and puts its interesting colour
// above the horizon - but this game's camera is pitched down, so most of the
// frame is the sky's LOWER hemisphere, which renders as a flat wash. This maps
// colour directly to view-direction height instead, so the gradient reads across
// the visible frame regardless of camera pitch.
Shader "Game/GradientSky"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.16, 0.38, 0.72, 1)
        _HorizonColor("Horizon Color", Color) = (0.68, 0.85, 0.96, 1)
        _BottomColor("Bottom Color", Color) = (0.80, 0.86, 0.90, 1)
        _HorizonHeight("Horizon Height", Range(-1, 1)) = 0.0
        _TopFalloff("Top Falloff", Range(0.1, 6)) = 1.2
        _BottomFalloff("Bottom Falloff", Range(0.1, 6)) = 1.6
        _Exposure("Exposure", Range(0, 4)) = 1.0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half  _HorizonHeight;
                half  _TopFalloff;
                half  _BottomFalloff;
                half  _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Skybox geometry is a unit cube centred on the camera, so object
                // space doubles as the view direction.
                output.viewDirWS = input.positionOS.xyz;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half h = normalize(input.viewDirWS).y - _HorizonHeight;

                half3 col;
                if (h >= 0.0h)
                {
                    half t = pow(saturate(h), 1.0h / max(_TopFalloff, 0.001h));
                    col = lerp(_HorizonColor.rgb, _TopColor.rgb, t);
                }
                else
                {
                    half t = pow(saturate(-h), 1.0h / max(_BottomFalloff, 0.001h));
                    col = lerp(_HorizonColor.rgb, _BottomColor.rgb, t);
                }

                return half4(col * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
