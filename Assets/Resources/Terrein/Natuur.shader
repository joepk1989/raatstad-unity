// Bomen, struiken, heide, palmen, cactussen en riet: de effen kleur van het materiaal met een lichte variatie. Grote vlekken over
// de wereld (de ene boom wat frisser of geler dan de andere, een bos niet overal even groen) en een fijn reliëf (bladeren, schors).
Shader "Raatstad/Natuur"
{
    Properties
    {
        _BaseColor ("Kleur", Color) = (0.2, 0.5, 0.22, 1)
        [NoScaleOffset] _BodemA ("Reliëf A", 2D) = "gray" {}
        [NoScaleOffset] _BodemB ("Reliëf B", 2D) = "gray" {}
        _Fine ("Fijn reliëf", Range(0, 1)) = 0.3
        _Vary ("Variatie", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Fine, _Vary;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RVert
            #pragma fragment RFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            TEXTURE2D(_BodemA); SAMPLER(sampler_BodemA);
            TEXTURE2D(_BodemB);
            #include "RaatstadLit.hlsl"

            void Surface(float3 pWS, float3 n, float4 vc, float4 ex, out half3 albedo, out half smooth)
            {
                float3 w = TriW(n);
                half M = TRI(_BodemB, pWS * 0.045, w).a;    // per boom en per bosje anders (vlekken van zo'n 20 m)
                half M2 = TRI(_BodemB, pWS * 0.013, w).a;   // en over een heel bos
                half f = TRI(_BodemA, pWS * 0.9, w).r;      // fijn: blad of schors
                half3 c = _BaseColor.rgb;
                half v = (M - 0.5) * 0.7 + (M2 - 0.5) * 0.5;
                c *= 1 + v * 0.45 * _Vary;
                c = lerp(c, c * half3(1.12, 1.04, 0.70), saturate(v * 1.6) * _Vary);    // geler
                c = lerp(c, c * half3(0.88, 1.0, 1.10), saturate(-v * 1.6) * _Vary);   // blauwer en donkerder
                c *= 1 + (f - 0.5) * _Fine;
                albedo = c; smooth = 0.12;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
