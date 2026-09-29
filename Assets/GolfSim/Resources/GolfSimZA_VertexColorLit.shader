// GolfSim ZA: lit surface coloured by its vertex colours (range mountains, desert ground).
Shader "Hidden/GolfSimZA/VertexColorLit"
{
    Properties
    {
        _MainTex ("Detail (grey)", 2D) = "white" {}
        _DetailScale ("Detail scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        float _DetailScale;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed detail = tex2D(_MainTex, IN.uv_MainTex * _DetailScale).r;
            o.Albedo = IN.color.rgb * lerp(0.82, 1.12, detail);
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
