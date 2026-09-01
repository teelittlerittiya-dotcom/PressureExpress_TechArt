Shader "PressureExpress/Sprite 3D Lit"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color("Tint", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.25
        _AmbientColor("Ambient Color", Color) = (1, 1, 1, 1)
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.12
        _NormalInfluence("Surface Direction Influence", Range(0, 1)) = 0.15
        _LightExposure("Light Exposure", Range(0, 4)) = 1
        _LightSteps("Light Steps (0 = Smooth)", Range(0, 32)) = 12
        [Toggle] _ZWrite("Z Write", Float) = 1

        // SpriteRenderer compatibility properties.
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip("Flip", Vector) = (1, 1, 1, 1)
        [PerRendererData] [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [PerRendererData] [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]
        ZTest LEqual

        Pass
        {
            Name "Sprite3DForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SpriteLitVertex
            #pragma fragment SpriteLitFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _AmbientColor;
                half _Cutoff;
                half _AmbientStrength;
                half _NormalInfluence;
                half _LightExposure;
                half _LightSteps;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 normalOS : NORMAL;
                UNITY_SKINNED_VERTEX_INPUTS
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 EvaluateSpriteLight(Light light, half3 normalWS)
            {
                // Most ship sprites have no authored normals and may represent floors,
                // walls, or props. Keep light reach orientation-independent while still
                // allowing a small amount of directional shaping.
                half directional = saturate(dot(normalWS, light.direction));
                half surfaceFactor = lerp(1.0h, directional, saturate(_NormalInfluence));
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                return light.color * (attenuation * surfaceFactor);
            }

            Varyings SpriteLitVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();

                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color * _Color * unity_SpriteColor;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 SpriteLitFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(sprite.a - _Cutoff);

                half faceSign = IS_FRONT_VFACE(face, 1.0h, -1.0h);
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS) * faceSign;
                half3 illumination = _AmbientColor.rgb * _AmbientStrength;
                half4 shadowMask = half4(1, 1, 1, 1);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                illumination += EvaluateSpriteLight(mainLight, normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();

                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                            illumination += EvaluateSpriteLight(additionalLight, normalWS);
                        }
                    #endif

                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        illumination += EvaluateSpriteLight(additionalLight, normalWS);
                    LIGHT_LOOP_END
                #endif

                illumination *= _LightExposure;

                // Several overlapping ship lights can produce a large HDR sum. Compress
                // its brightness before posterizing so the light color remains readable
                // instead of turning the sprite (and bloom) into a white block.
                half peak = max(illumination.r, max(illumination.g, illumination.b));
                half mappedPeak = peak / (1.0h + peak);
                if (_LightSteps > 1.0h)
                    mappedPeak = floor(mappedPeak * _LightSteps + 0.5h) / _LightSteps;

                illumination *= mappedPeak / max(peak, 0.0001h);

                half3 finalColor = MixFog(sprite.rgb * illumination, input.fogFactor);
                return half4(finalColor, sprite.a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
