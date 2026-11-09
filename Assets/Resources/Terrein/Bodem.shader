// Het terrein van de planeet in één materiaal: bodemsoorten die zacht in elkaar overlopen, elk met een eigen reliëf
// (triplanaire textures BodemA en BodemB uit Tools/terreintextures.py). Het low-poly blijft: elke driehoek houdt zijn eigen
// normaal, alleen de kleur loopt door (met Terrein zacht krijgt de belichting gedeelde normalen; de soort volgt de driehoek).
// Wat de shader zelf weet (per pixel): de hoogte boven de bol (gras, dieper groen, grijsgroen, rots boven de 30 m, sneeuw boven
// de 54 m), de helling van de driehoek (aarde op steile flanken, rots boven de 22 m) en de waterlijn (zand, zeebodem).
// Wat per hoekpunt meekomt (vertexkleur, gemiddeld over de vakken eromheen, PlanetCity.SoilColor): r ijs, g woestijn, b heide,
// a nat (een vak met water); en in uv0: x roetdeken, y kraterkom, z kraterrand (een verse meteoorkrater), w de helling van het vak.
// Een overgang ligt zo op de rand tussen twee vakken en rafelt met het reliëf van de nieuwe soort.
Shader "Raatstad/Bodem"
{
    Properties
    {
        [NoScaleOffset] _BodemA ("Reliëf A (gras, aarde, rots, zand)", 2D) = "gray" {}
        [NoScaleOffset] _BodemB ("Reliëf B (sneeuw, heide, woestijn, vlekken)", 2D) = "gray" {}
        _Scale ("Schaal (per meter)", Float) = 0.22
        _MacroScale ("Schaal vlekken", Float) = 0.011
        _Center ("Middelpunt planeet", Vector) = (0, 0, 0, 0)
        _PlanetR ("Straal", Float) = 600
        _Sea ("Zeeniveau", Float) = -1
        _Snow ("Sneeuwgrens", Float) = 54
        _SoilSmooth ("Soort naar zachte normaal (Terrein zacht/rond)", Float) = 0
        _SteepBias ("Steiler rekenen bij zachte normaal", Float) = 0.06
        _Gras0 ("Gras", Color) = (0.64, 0.78, 0.45, 1)
        _Gras1 ("Gras diep", Color) = (0.373, 0.58, 0.278, 1)
        _Gras3 ("Grijsgroen", Color) = (0.604, 0.643, 0.541, 1)
        _Aarde ("Aarde", Color) = (0.545, 0.482, 0.345, 1)
        _Rots ("Rots", Color) = (0.50, 0.48, 0.46, 1)
        _Zand ("Zand", Color) = (0.86, 0.79, 0.55, 1)
        _Sneeuw ("Sneeuw", Color) = (0.94, 0.95, 0.97, 1)
        _IJs ("IJs", Color) = (0.84, 0.91, 0.96, 1)
        _Woestijn ("Woestijn", Color) = (0.86, 0.71, 0.47, 1)
        _Heide ("Heide", Color) = (0.47, 0.39, 0.40, 1)
        _HeideBloem ("Heidebloem", Color) = (0.60, 0.36, 0.58, 1)
        _Zeebodem ("Zeebodem", Color) = (0.47, 0.45, 0.36, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _Scale, _MacroScale, _PlanetR, _Sea, _Snow, _SoilSmooth, _SteepBias;
            float4 _Center;
            half4 _Gras0, _Gras1, _Gras3, _Aarde, _Rots, _Zand, _Sneeuw, _IJs, _Woestijn, _Heide, _HeideBloem, _Zeebodem;
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
            #define RAATSTAD_FACE_SURFACE   // de soort naar de driehoek, de belichting naar de normaal (zacht terrein: PlanetSoil.Smooth.cs)
            #define RAATSTAD_SOIL_SMOOTH _SoilSmooth   // zacht en rond: de soort ook naar de zachte normaal (vloeiende overgangen)
            #include "RaatstadLit.hlsl"

            // de nieuwe soort komt erbij waar zijn gewicht w plus zijn eigen reliëf d boven de helft komt: een rafelige rand
            half Mix(half w, half d, half k)
            {
                return saturate((w + (d - 0.5) * 0.55 - 0.5) * k + 0.5);
            }

            void Surface(float3 pWS, float3 n, float4 vc, float4 ex, out half3 albedo, out half smooth)
            {
                half sliver = step(50, ex.w); ex.w -= 100 * sliver;   // een splinter (PlanetSoil.SoilMesh): de soort naar de grond eromheen, geen kale aarde
                n = normalize(lerp(n, gLightN, sliver));
                float3 P = pWS - _Center.xyz; float r = length(P); float3 up = P / r; float h = r - _PlanetR;
                float3 w = TriW(gLightN);   // de textures mengen met de normaal van de belichting (zacht terrein: geen naad per driehoek); de soort volgt de driehoek (n)
                float3 q = P * _Scale;
                half4 A = TRI(_BodemA, q, w);
                half4 B = TRI(_BodemB, q * 0.7, w);
                half M = TRI(_BodemB, P * _MacroScale, w).a;   // grote vlekken: wat droger of frisser gras, lichtere en donkere plekken
                half upd = dot(n, up) - _SteepBias * _SoilSmooth;   // een zachte normaal is een gemiddelde en dus minder steil: iets steiler rekenen, zodat rots rots blijft

                // gras naar de hoogte (zoals TerrColor): fris, dieper groen tot 5 m, grijsgroen boven de 16 m
                half3 g = lerp(_Gras0.rgb, _Gras1.rgb, smoothstep(0, 5, h));
                g = lerp(g, _Gras3.rgb, smoothstep(16, 28, h) * 0.6);
                g = lerp(g, g * half3(1.10, 1.02, 0.72), saturate(M * 1.6 - 0.7) * 0.6);   // droge plekken
                g *= 0.80 + 0.40 * A.r;
                g *= 0.90 + 0.20 * M;
                half3 col = g; smooth = 0.08;

                half3 rots = _Rots.rgb * (0.62 + 0.76 * A.b) * (0.92 + 0.16 * M);
                // boven de 30 m loopt het gras over in rots (MountainBand)
                col = lerp(col, rots, Mix(smoothstep(30, 52, h + (M - 0.5) * 8), A.b, 3));

                // landschappen van het vak: heide, woestijn, ijs
                half3 heide = lerp(_Heide.rgb * (0.80 + 0.40 * A.r), _HeideBloem.rgb * (0.85 + 0.3 * B.g), smoothstep(0.28, 0.62, B.g) * 0.85);
                col = lerp(col, heide, Mix(vc.b, B.g, 4));
                half3 woest = _Woestijn.rgb * (0.84 + 0.28 * B.b) * (0.94 + 0.12 * M);
                col = lerp(col, woest, Mix(vc.g, B.b, 4));

                // steile flanken: kale aarde, hoger kale rots (niet op ijs). Steil is de driehoek zelf (alleen als hij echt steil is) of
                // het vak als geheel (ex.w, doorlopend over de hoekpunten): zo is een bergkam in één keer kaal, zonder losse groene driehoekjes
                half steep = max(smoothstep(0.74, 0.62, upd), smoothstep(0.7, 1.1, ex.w));
                half rk = smoothstep(10, 30, h + (M - 0.5) * 14 + (A.b - 0.5) * 8);   // van aarde naar rots: rafelig, geen horizontale band
                half3 aarde = _Aarde.rgb * (0.74 + 0.52 * A.g);
                half3 flank = lerp(aarde, rots, rk);
                col = lerp(col, flank, Mix(steep, lerp(A.g, A.b, rk), 3));

                half ijsW = Mix(vc.r, B.r, 4);
                col = lerp(col, _IJs.rgb * (0.90 + 0.16 * B.r), ijsW); smooth = lerp(smooth, 0.55, ijsW);

                // sneeuw boven de sneeuwgrens, op steile flanken pas hoger
                half sn = smoothstep(_Snow - 2, _Snow + 3, h + (B.r - 0.5) * 6) * max(smoothstep(0.52, 0.68, upd), smoothstep(_Snow + 16, _Snow + 20, h));
                sn = Mix(sn, B.r, 3);
                col = lerp(col, _Sneeuw.rgb * (0.93 + 0.09 * B.r), sn); smooth = lerp(smooth, 0.35, sn);

                // een verse krater: puin op de rand en in de kom, een donkere kom, en een roetdeken die naar buiten in vlekken rafelt
                // (vaste kleuren in sRGB, zoals ScorchColor: omzetten; de Color-eigenschappen zijn al lineair)
                half3 uitwerp = SRGBToLinear(half3(0.55, 0.49, 0.41)) * (0.78 + 0.44 * A.g);
                half3 bodem = SRGBToLinear(half3(0.30, 0.27, 0.24)) * (0.75 + 0.5 * A.b);
                half3 roet = SRGBToLinear(lerp(half3(0.10, 0.09, 0.085), half3(0.24, 0.22, 0.20), A.r * A.r)) * (0.85 + 0.3 * A.g);
                col = lerp(col, uitwerp, Mix(saturate(ex.y + ex.z), A.g, 3));
                col = lerp(col, bodem, Mix(ex.y, A.b, 3));
                half sootN = TRI(_BodemB, P * 0.06, w).a * 0.6 + A.b * 0.4;   // vlekken van een paar meter
                half sootA = saturate((ex.x * 1.25 - sootN) * 5) * lerp(0.9, 0.55, ex.z);
                col = lerp(col, roet, sootA);
                col = lerp(col, uitwerp, saturate((A.g - 0.72) * 6) * smoothstep(0.02, 0.15, ex.x) * smoothstep(0.7, 0.35, ex.x));   // gruis aan de rafelrand
                smooth = lerp(smooth, 0.04, saturate(ex.x));

                // langs het water: zand op de waterlijn, donkere bodem in het diepe
                half wet = saturate(vc.a * 2.5);
                half3 zand = _Zand.rgb * (0.86 + 0.26 * A.a);
                half beach = wet * Mix(smoothstep(_Sea + 0.9, _Sea + 0.2, h), A.a, 3);
                col = lerp(col, zand * lerp(1, 0.82, smoothstep(_Sea + 0.1, _Sea - 0.4, h)), beach);
                half deep = wet * smoothstep(_Sea - 1.0, _Sea - 2.2, h + (A.g - 0.5));
                col = lerp(col, _Zeebodem.rgb * (0.78 + 0.32 * A.g), deep);

                albedo = col;
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
