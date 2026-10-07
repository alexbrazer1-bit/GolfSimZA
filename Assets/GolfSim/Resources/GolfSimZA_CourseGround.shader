// GolfSim ZA ground for courses built from OpenStreetMap: one terrain painted with eight
// surfaces (rough, fairway, green, tee, bunker, path, fringe, natural ground) from two
// splat maps that cover the whole course. Surface textures come from a texture array made in
// code. A large-scale variation breaks up repeats, fairways and greens get mowing stripes.
// Used by GolfSimZA.Courses.Osm.OsmCourseScene as the terrain's material.
Shader "Hidden/GolfSimZA/CourseGround"
{
    Properties
    {
        _Splat0 ("Weights 0-3", 2D) = "black" {}
        _Splat1 ("Weights 4-7", 2D) = "black" {}
        _Layers ("Surfaces", 2DArray) = "" {}
        _Macro ("Large variation", 2D) = "gray" {}
        _TileA ("Tiles per metre 0-3", Vector) = (0.5, 0.45, 0.6, 0.5)
        _TileB ("Tiles per metre 4-7", Vector) = (0.35, 0.4, 0.5, 0.25)
        _StripeDir ("Mowing stripe direction (x, z)", Vector) = (0, 1, 0, 0)
        _StripeWidth ("Mowing stripe width (m)", Float) = 7
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-100" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.5
        #pragma require 2darray

        UNITY_DECLARE_TEX2DARRAY(_Layers);
        sampler2D _Splat0;
        sampler2D _Splat1;
        sampler2D _Macro;
        float4 _TileA;
        float4 _TileB;
        float4 _StripeDir;
        float _StripeWidth;

        struct Input
        {
            float2 uv_Splat0;
            float3 worldPos;
        };

        half3 Layer(float2 xz, float tile, float index)
        {
            // Two scales mixed so the repeat is hard to see.
            half3 a = UNITY_SAMPLE_TEX2DARRAY(_Layers, float3(xz * tile, index)).rgb;
            half3 b = UNITY_SAMPLE_TEX2DARRAY(_Layers, float3(xz * tile * 0.23 + 0.37, index)).rgb;
            return lerp(a, b, 0.35);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half4 w0 = tex2D(_Splat0, IN.uv_Splat0);
            half4 w1 = tex2D(_Splat1, IN.uv_Splat0);
            half total = dot(w0, 1) + dot(w1, 1);
            if (total < 0.001) { w0 = half4(1, 0, 0, 0); total = 1; }
            w0 /= total;
            w1 /= total;
            float2 xz = IN.worldPos.xz;

            half3 col = 0;
            if (w0.x > 0.003) col += Layer(xz, _TileA.x, 0) * w0.x;
            if (w0.y > 0.003) col += Layer(xz, _TileA.y, 1) * w0.y;
            if (w0.z > 0.003) col += Layer(xz, _TileA.z, 2) * w0.z;
            if (w0.w > 0.003) col += Layer(xz, _TileA.w, 3) * w0.w;
            if (w1.x > 0.003) col += Layer(xz, _TileB.x, 4) * w1.x;
            if (w1.y > 0.003) col += Layer(xz, _TileB.y, 5) * w1.y;
            if (w1.z > 0.003) col += Layer(xz, _TileB.z, 6) * w1.z;
            if (w1.w > 0.003) col += Layer(xz, _TileB.w, 7) * w1.w;

            // Large-scale colour variation (patches of lusher / drier grass).
            half m1 = tex2D(_Macro, xz / 173.0).r;
            half m2 = tex2D(_Macro, xz / 41.0 + 0.5).r;
            half grassy = 1 - w1.x - w1.y; // not on sand or paths
            col *= lerp(1, lerp(0.86, 1.12, m1) * lerp(0.95, 1.05, m2), grassy);

            // Mowing stripes on fairways, tees and greens.
            float along = dot(xz, normalize(_StripeDir.xy));
            half stripe = step(0.5, frac(along / _StripeWidth));
            half mown = w0.y + w0.z + w0.w + w1.z * 0.5;
            float greenStripe = step(0.5, frac(along / (_StripeWidth * 0.5)));
            half s = lerp(stripe, greenStripe, saturate(w0.z * 1.5));
            col *= 1 + (s - 0.5) * 0.11 * mown;

            o.Albedo = col;
            o.Metallic = 0;
            o.Smoothness = 0.08 + 0.10 * w0.z + 0.04 * w0.y;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
