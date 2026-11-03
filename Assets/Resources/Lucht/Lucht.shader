// De lucht van de planeet (skybox, PlanetSky.cs): een verloop van horizon naar zenit rond de plaatselijke 'boven', met de
// horizon op de echte kim van de bol (die zakt als je hoger komt), de zon met een gloed, oranje bij zonsopgang en -ondergang,
// sterren 's nachts. Van ver weg (_Space = 1) wordt het de zwarte ruimte met sterren en de zon.
Shader "Raatstad/Lucht"
{
    Properties { }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _Up, _SunDir;
            float4 _Zenith, _Horizon, _Dusk, _SunCol;
            float _HorizonMu, _Space, _Stars, _SunVis;

            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            V vert(A a) { V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.dir = a.pos.xyz; return o; }

            float3 Hash3(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yxz + 19.19);
                return frac((p.xxy + p.yzz) * p.zyx);
            }

            // sterren: per cel van een 3D-raster misschien een ster, met een eigen helderheid en kleur
            float3 StarLayer(float3 d, float scale, float chance)
            {
                float3 p = d * scale, c = floor(p), r = Hash3(c);
                if (r.x > chance) return 0;
                float3 sp = c + 0.25 + 0.5 * Hash3(c + 7.1);
                float b = saturate(1 - length(p - sp) / 0.32); b *= b;
                float3 tint = lerp(float3(1.0, 0.85, 0.7), float3(0.75, 0.85, 1.0), r.y);
                return tint * b * (0.35 + 1.4 * r.z * r.z);
            }
            float3 Stars(float3 d) { return StarLayer(d, 170, 0.05) + StarLayer(d, 340, 0.035) * 0.6; }

            float4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float mu = dot(d, _Up);
                float t = saturate((mu - _HorizonMu) / (1 - _HorizonMu));   // 0 op de kim, 1 recht omhoog
                float sd = dot(d, _SunDir);

                // dichtbij: blauwe lucht met een verloop
                float3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(t, 0.5));
                float toward = saturate(sd * 0.5 + 0.5);
                col = lerp(col, _Dusk.rgb, _Dusk.a * (0.3 + 0.7 * pow(toward, 4)) * pow(1 - t, 2.5));
                float halo = pow(saturate(sd), 400) * 0.8 + pow(saturate(sd), 18) * 0.18;
                float disc = smoothstep(0.99955, 0.99975, sd);
                col += _SunCol.rgb * (halo + disc * 6) * _SunVis;
                col += Stars(d) * _Stars * smoothstep(0, 0.12, t);

                // ver weg: de ruimte
                float3 space = float3(0.004, 0.005, 0.012) + Stars(d) * 1.2;
                space += float3(1, 0.96, 0.9) * (smoothstep(0.99962, 0.99978, sd) * 12 + pow(saturate(sd), 900) * 1.5 + pow(saturate(sd), 60) * 0.06);
                col = lerp(col, space, _Space);
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
