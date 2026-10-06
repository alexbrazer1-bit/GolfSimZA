// GolfSim ZA water for imported courses: lakes and ponds with moving ripples, sky and
// surroundings reflected more at a glancing angle (fresnel), a sun glint and a deep water
// colour. Replaces course water shaders that need the reflection scripts of the program the
// course was made for (AQUAS, Lux Water, OPCD water ...). Reflections come from the GolfSim ZA
// sky reflection probe. Used by GolfSimZA.Courses.ShaderRepair.
Shader "Hidden/GolfSimZA/Water"
{
    Properties
    {
        _Color ("Deep water", Color) = (0.06, 0.20, 0.24, 1)
        _BumpMap ("Ripples (normal map)", 2D) = "bump" {}
        _WaveScale ("Ripple size (m)", Float) = 6
        _WaveSpeed ("Ripple speed", Float) = 0.35
        _WaveStrength ("Ripple strength", Range(0, 2)) = 0.6
        _Glossiness ("Smoothness", Range(0, 1)) = 0.96
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows
        #pragma target 3.0

        sampler2D _BumpMap;
        fixed4 _Color;
        float _WaveScale;
        float _WaveSpeed;
        half _WaveStrength;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandardSpecular o)
        {
            // Two ripple layers moving in different directions, in world space so every lake
            // shares the same ripple size whatever its mesh.
            float2 uv = IN.worldPos.xz / max(0.5, _WaveScale);
            float t = _Time.y * _WaveSpeed;
            float3 n1 = UnpackNormal(tex2D(_BumpMap, uv + float2(t * 0.11, t * 0.07)));
            float3 n2 = UnpackNormal(tex2D(_BumpMap, uv * 1.7 + float2(-t * 0.08, t * 0.13)));
            float3 n = normalize(float3((n1.xy + n2.xy) * _WaveStrength, n1.z * n2.z));
            o.Normal = n;
            o.Albedo = _Color.rgb;
            o.Specular = half3(0.03, 0.03, 0.03);
            o.Smoothness = _Glossiness;
            o.Occlusion = 1;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
