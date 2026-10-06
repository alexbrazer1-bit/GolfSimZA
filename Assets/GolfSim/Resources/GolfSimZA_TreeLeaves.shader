// GolfSim ZA tree leaves for imported courses: the course trees' own leaf textures drawn with
// light coming through the leaves (translucency), soft wrap lighting, both sides of each leaf
// card and a gentle sway in the wind. Replaces course tree shaders that only work inside the
// program the course was made for (Arboreum, NatureManufacture, CTI ...).
// Used by GolfSimZA.Courses.ShaderRepair.
Shader "Hidden/GolfSimZA/TreeLeaves"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Leaves (RGB) Alpha (A)", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.4
        _Translucency ("Light through the leaves", Range(0, 2)) = 0.8
        _WindStrength ("Wind", Float) = 0.06
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off
        LOD 300

        CGPROGRAM
        #pragma surface surf Leaves vertex:vert alphatest:_Cutoff addshadow fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Translucency;
        float _WindStrength;

        struct Input
        {
            float2 uv_MainTex;
            float facing : VFACE;
        };

        void vert(inout appdata_full v)
        {
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float h = max(0.0, world.y - origin.y);
            float t = _Time.y;
            float sway = sin(t * 1.3 + origin.x * 0.21 + origin.z * 0.17) + 0.4 * sin(t * 2.9 + world.x * 0.7 + world.z * 0.5);
            world.xz += float2(0.7, 0.45) * sway * _WindStrength * h * 0.08;
            v.vertex = mul(unity_WorldToObject, float4(world, 1.0));
        }

        half4 LightingLeaves(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half wrap = saturate((ndl + 0.45) / 1.45);
            // Sun behind the leaves: they glow with the light coming through.
            half back = pow(saturate(dot(viewDir, -lightDir)), 3.0) * saturate(0.35 - ndl) * _Translucency;
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (wrap + back * 1.6) * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = tex.rgb;
            o.Alpha = tex.a;
            float3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            // The back of a leaf card faces the other way.
            n.z *= IN.facing > 0 ? 1.0 : -1.0;
            o.Normal = n;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}
