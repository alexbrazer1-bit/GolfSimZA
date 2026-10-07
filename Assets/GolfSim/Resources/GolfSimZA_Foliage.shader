// GolfSim ZA tree foliage (built-in trees, palms, bushes, reeds): leaf-cluster cards with
// see-through gaps (alpha cut-out), the mesh's vertex colour for light/dark parts of the crown,
// soft wrap lighting with light coming through the leaves, both sides of each card, and a sway
// in the wind that grows with height. Drawn with GPU instancing. Used by GolfSimZA.Physics.TreeField.
Shader "Hidden/GolfSimZA/Foliage"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Leaves (RGB) Alpha (A)", 2D) = "white" {}
        _Cutoff ("Alpha cut-off", Range(0, 1)) = 0.45
        _Translucency ("Light through the leaves", Range(0, 2)) = 0.9
        _WindStrength ("Wind", Float) = 0.05
        _Ambient ("Sky light", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off
        LOD 300

        CGPROGRAM
        #pragma surface surf Foliage vertex:vert alphatest:_Cutoff addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Translucency;
        float _WindStrength;
        half _Ambient;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void vert(inout appdata_full v)
        {
            float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = max(0.0, world.y - origin.y);
            float t = _Time.y;
            float sway = sin(t * 1.1 + origin.x * 0.13 + origin.z * 0.11) * 0.7 + 0.3 * sin(t * 2.7 + world.x * 0.6 + world.z * 0.45);
            // Leaves flutter a little more than the branches (vertex colour alpha = how loose).
            world.xz += float2(0.8, 0.5) * sway * _WindStrength * h * (0.04 + 0.06 * v.color.a);
            v.vertex = mul(unity_WorldToObject, float4(world, 1.0));
        }

        half4 LightingFoliage(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half wrap = saturate((ndl + 0.5) / 1.5);
            half back = pow(saturate(dot(viewDir, -lightDir)), 4.0) * _Translucency;
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (wrap + back) * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = tex.rgb * _Color.rgb * IN.color.rgb * _Ambient;
            o.Alpha = tex.a;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}
