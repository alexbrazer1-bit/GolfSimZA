// GolfSim ZA rough grass: alpha-tested grass tufts, two-sided, swaying in the wind and
// shrinking into the ground at the edge of the grass distance (no popping).
// Used by GolfSimZA.Visual.GrassField (Settings → VISUAL → COURSE LOOK → ROUGH GRASS).
Shader "Hidden/GolfSimZA/Grass"
{
    Properties
    {
        _MainTex ("Blades", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.45
        _WindStrength ("Wind", Float) = 0.12
        _FadeStart ("Fade start", Float) = 50
        _FadeEnd ("Fade end", Float) = 70
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert alphatest:_Cutoff addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        float _WindStrength;
        float _FadeStart;
        float _FadeEnd;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void vert(inout appdata_full v)
        {
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = v.texcoord.y;          // 0 at the ground, 1 at the tip
            float height = v.texcoord1.x;    // tuft height in metres
            float phase = v.texcoord1.y;

            float3 toCam = world - _WorldSpaceCameraPos;
            float dist = length(toCam.xz);
            float fade = 1.0 - saturate((dist - _FadeStart) / max(1.0, _FadeEnd - _FadeStart));

            float t = _Time.y;
            float sway = sin(t * 1.6 + world.x * 0.31 + world.z * 0.23 + phase) + 0.45 * sin(t * 3.3 + world.x * 0.9 + phase * 2.0);
            world.xz += float2(0.8, 0.55) * sway * _WindStrength * h * h * height;
            world.y -= h * height * (1.0 - fade);

            v.vertex = mul(unity_WorldToObject, float4(world, 1.0));
            // Grass is lit like the ground under it (normal straight up) so tufts do not flicker.
            v.normal = normalize(mul((float3x3)unity_WorldToObject, float3(0, 1, 0)));
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = IN.color.rgb * tex.rgb;
            o.Alpha = tex.a;
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Transparent/Cutout/VertexLit"
}
