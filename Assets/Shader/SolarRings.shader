Shader "Custom/PlanetRingsVoid"
{
    Properties
    {
        [Header(Shape and Bending)]
        _RingCenter     ("Ring Center (X, Y)", Vector) = (0.5, -0.5, 0, 0)
        _PerspDistortion ("Perspective Distortion", Range(0.1, 10)) = 1.2
        _GlobalScale    ("Global Scale", Float) = 1.0

        [Header(Black Hole or Planet Cutout)]
        _InnerRadius    ("Inner Radius Void", Range(0.0, 2.0)) = 0.25
        _InnerFade      ("Inner Edge Softness", Range(0.001, 0.5)) = 0.05

        [Header(Movement Control)]
        _SpeedRotation  ("Rotation Speed", Float) = 1.5

        [Header(Rings Structure)]
        _LineDensity    ("Line Density", Float) = 35.0
        _StreakLength   ("Streak Length", Range(0.05, 2.0)) = 0.2
        _Threshold      ("Line Threshold", Range(0.1, 0.8)) = 0.45

        [Header(Detail Micro Lines)]
        _ThinStrength   ("Thin Line Strength", Range(0, 1)) = 0.4

        [Header(Color and Transparency)]
        _ColorA         ("Color A Bright", Color) = (1.0, 0.68, 0.08, 1)
        _ColorB         ("Color B Dark Transparent", Color) = (0.12, 0.06, 0.01, 0)
        _Brightness     ("Global Brightness", Range(0, 4)) = 1.3
        _Alpha          ("Global Alpha", Range(0, 1)) = 1.0

        [Header(Fading)]
        _GradPower      ("Gradient Power", Range(0.1, 6)) = 1.8
        _EdgeFadeRadial ("Edge Fade Radial", Range(0.01, 0.49)) = 0.15
        _EdgeFadeX      ("Edge Fade X", Range(0.01, 0.49)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PlanetRingsVoidForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RingCenter;
                float  _PerspDistortion;
                float  _GlobalScale;
                float  _InnerRadius;
                float  _InnerFade;
                float  _SpeedRotation;
                float  _LineDensity;
                float  _StreakLength;
                float  _Threshold;
                float  _ThinStrength;
                float4 _ColorA;
                float4 _ColorB;
                float  _Brightness;
                float  _GradPower;
                float  _EdgeFadeRadial;
                float  _EdgeFadeX;
                float  _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float  fogFactor   : TEXCOORD1;
            };

            float2 _Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = _Hash2(i).x;
                float b = _Hash2(i + float2(1,0)).x;
                float c = _Hash2(i + float2(0,1)).x;
                float d = _Hash2(i + float2(1,1)).x;
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.fogFactor   = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 centeredUV = IN.uv - _RingCenter.xy;

                centeredUV.y /= _PerspDistortion; 
                centeredUV *= _GlobalScale;

                float rho = length(centeredUV);       
                float theta = atan2(centeredUV.y, centeredUV.x); 

                float innerCutout = smoothstep(_InnerRadius, _InnerRadius + _InnerFade, rho);

                float movedTheta = theta - _Time.y * _SpeedRotation;

                float2 streakUV = float2(rho * _LineDensity, movedTheta * _StreakLength);
                float mainNoise = ValueNoise(streakUV);
                float mainLines = smoothstep(_Threshold, _Threshold + 0.12, mainNoise);

                float2 detailUV = float2(rho * _LineDensity * 3.5, movedTheta * _StreakLength * 2.5);
                float detailNoise = ValueNoise(detailUV);
                float thinLines = smoothstep(0.4, 0.52, detailNoise) * _ThinStrength;

                float lines = saturate(mainLines + thinLines);

                float grad = pow(saturate(IN.uv.y), _GradPower);
                float fadeRadial = smoothstep(1.0, 1.0 - _EdgeFadeRadial, rho);
                float fadeX = smoothstep(0.0, _EdgeFadeX, IN.uv.x) * smoothstep(1.0, 1.0 - _EdgeFadeX, IN.uv.x);
                
                float totalFade = grad * fadeRadial * fadeX * innerCutout;

                float4 finalColor = lerp(_ColorB, _ColorA, lines);
                
                float3 rgb = finalColor.rgb * totalFade * _Brightness;
                float alpha = finalColor.a * totalFade * _Alpha;

                rgb = MixFog(rgb, IN.fogFactor);

                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}