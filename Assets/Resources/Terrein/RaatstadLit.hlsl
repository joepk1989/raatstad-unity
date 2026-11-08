// Gedeelde belichting voor de eigen URP-shaders van Raatstad (Bodem en Natuur): hetzelfde licht als URP/Lit (PBR, schaduwen,
// Forward+, mist), maar de kleur komt uit een functie Surface(...) die de shader zelf na deze include definieert.
#ifndef RAATSTAD_LIT_INCLUDED
#define RAATSTAD_LIT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct RAttr
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 color      : COLOR;
    float4 extra      : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct RVary
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float4 color      : TEXCOORD2;
    half   fogFactor  : TEXCOORD3;
    float4 extra      : TEXCOORD5;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD4;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

RVary RVert(RAttr a)
{
    RVary o = (RVary)0;
    UNITY_SETUP_INSTANCE_ID(a);
    UNITY_TRANSFER_INSTANCE_ID(a, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs vp = GetVertexPositionInputs(a.positionOS.xyz);
    o.positionCS = vp.positionCS;
    o.positionWS = vp.positionWS;
    o.normalWS = TransformObjectToWorldNormal(a.normalOS);
    o.color = a.color;
    o.extra = a.extra;
    o.fogFactor = ComputeFogFactor(vp.positionCS.z);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    o.shadowCoord = GetShadowCoord(vp);
#endif
    return o;
}

// triplanair: drie projecties, gewogen naar de normaal (scherp, zodat een vlak meestal één projectie heeft)
float3 TriW(float3 n)
{
    float3 w = abs(n); w = w * w; w = w * w; return w / (w.x + w.y + w.z);
}
#define TRI(tex, p, w) (SAMPLE_TEXTURE2D(tex, sampler_BodemA, (p).zy) * (w).x + SAMPLE_TEXTURE2D(tex, sampler_BodemA, (p).xz) * (w).y + SAMPLE_TEXTURE2D(tex, sampler_BodemA, (p).xy) * (w).z)

static float3 gLightN;   // de normaal van de belichting (RAATSTAD_FACE_SURFACE: Surface krijgt de normaal van de driehoek)
void Surface(float3 pWS, float3 n, float4 vc, float4 ex, out half3 albedo, out half smooth);

half4 RFrag(RVary i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 nWS = normalize(i.normalWS);
    half3 albedo; half smooth;
#ifdef RAATSTAD_FACE_SURFACE
    // de soort (gras, rots, aarde, sneeuw) naar de helling van de driehoek zelf (low poly), of naar de zachte normaal (RAATSTAD_SOIL_SMOOTH: zacht en rond)
    float3 nF = normalize(cross(ddy(i.positionWS), ddx(i.positionWS))); if (dot(nF, nWS) < 0) nF = -nF;
    gLightN = nWS;
    Surface(i.positionWS, normalize(lerp(nF, nWS, RAATSTAD_SOIL_SMOOTH)), i.color, i.extra, albedo, smooth);
#else
    Surface(i.positionWS, nWS, i.color, i.extra, albedo, smooth);
#endif

    InputData d = (InputData)0;
    d.positionWS = i.positionWS;
    d.positionCS = i.positionCS;
    d.normalWS = nWS;
    d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    d.shadowCoord = i.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
#else
    d.shadowCoord = float4(0, 0, 0, 0);
#endif
    d.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
    d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    d.bakedGI = SampleSH(nWS);
    d.shadowMask = half4(1, 1, 1, 1);

    SurfaceData s = (SurfaceData)0;
    s.albedo = albedo;
    s.alpha = 1;
    s.smoothness = smooth;
    s.occlusion = 1;
    s.metallic = 0;
    s.specular = half3(0, 0, 0);
    half4 c = UniversalFragmentPBR(d, s);
    c.rgb = MixFog(c.rgb, d.fogCoord);
    c.a = 1;
    return c;
}
#endif
