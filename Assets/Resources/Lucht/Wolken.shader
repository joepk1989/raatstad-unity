// Een laag wolken om de bol (PlanetSky.cs), alleen van ver weg te zien. Het patroon is ruis op de richting in de ruimte van
// de bol zelf (die draait langzaam); meer bewolking bij slechter weer (_Cover). Licht van de zon, de nachtkant is donker.
Shader "Raatstad/Wolken"
{
    Properties { }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Cull Back ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _SunDir;
            float4 _Tint;
            float _Cover, _Fade, _Shift;

            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 op : TEXCOORD0; float3 wn : TEXCOORD1; };

            V vert(A a)
            {
                V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.op = a.pos.xyz;
                o.wn = TransformObjectToWorldDir(a.pos.xyz); return o;
            }

            float Hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }
            float Fbm(float3 p)
            {
                float s = 0, a = 0.5;
                [unroll] for (int k = 0; k < 5; k++) { s += a * Noise(p); p = p * 2.03 + 1.7; a *= 0.5; }
                return s;
            }

            float4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.op);
                // licht verwrongen, zodat er slierten en draaikolken ontstaan; _Shift laat het patroon langzaam veranderen
                float3 w = float3(Noise(n * 2.2 + _Shift), Noise(n * 2.2 + 5.2 - _Shift), Noise(n * 2.2 + 9.7));
                float f = Fbm(n * 4.6 + (w - 0.5) * 1.2);
                float thr = lerp(0.53, 0.36, _Cover);
                float c = smoothstep(thr, thr + 0.16, f);
                float dense = smoothstep(thr + 0.08, thr + 0.32, f);
                float3 wn = normalize(i.wn);
                float l = saturate(dot(wn, _SunDir) * 1.2 + 0.15);
                float3 col = _Tint.rgb * lerp(1, 0.78, dense) * (0.03 + l * 1.05);
                return float4(col, c * 0.93 * _Fade);
            }
            ENDHLSL
        }
    }
}
