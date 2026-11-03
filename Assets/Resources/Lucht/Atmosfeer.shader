// De dunne blauwe rand om de planeet (PlanetSky.cs): een bol iets groter dan de planeet, getekend met de achterkant, zodat
// alleen de pixels naast de planeet hem krijgen (de planeet ligt ervoor). Per pixel een korte mars door de lucht met een
// dichtheid die met de hoogte afneemt; de zonkant licht op, de nachtkant blijft donker.
Shader "Raatstad/Atmosfeer"
{
    Properties { }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Cull Front ZWrite Off ZTest LEqual
        Blend One One
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _SunDir;
            float4 _Color;
            float _R, _Ra, _H, _Fade, _Strength;

            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };

            V vert(A a) { V o; float3 w = TransformObjectToWorld(a.pos.xyz); o.wp = w; o.pos = TransformWorldToHClip(w); return o; }

            // afstanden waar de straal ro + t·rd de bol met straal r raakt (x > y: geen raakpunt)
            float2 Hit(float3 ro, float3 rd, float r)
            {
                float b = dot(ro, rd), c = dot(ro, ro) - r * r, h = b * b - c;
                if (h < 0) return float2(1, 0);
                h = sqrt(h); return float2(-b - h, -b + h);
            }

            float4 frag(V i) : SV_Target
            {
                float3 ro = _WorldSpaceCameraPos, rd = normalize(i.wp - ro);
                float2 a = Hit(ro, rd, _Ra);
                float t0 = max(a.x, 0), t1 = a.y;
                float2 p = Hit(ro, rd, _R);
                if (p.x <= p.y && p.x > 0) t1 = min(t1, p.x);
                if (t1 <= t0) return 0;
                float dt = (t1 - t0) / 10, od = 0, lit = 0;
                [unroll] for (int k = 0; k < 10; k++)
                {
                    float3 q = ro + rd * (t0 + dt * (k + 0.5));
                    float r = length(q), dens = exp(-max(r - _R, 0) / _H) * dt;
                    od += dens;
                    lit += dens * saturate(dot(q / r, _SunDir) * 1.6 + 0.35);
                }
                float alpha = 1 - exp(-od * _Strength);
                float light = lit / max(od, 1e-4);
                float3 col = _Color.rgb * alpha * (0.04 + light * 1.1);
                return float4(col * _Fade, 0);
            }
            ENDHLSL
        }
    }
}
