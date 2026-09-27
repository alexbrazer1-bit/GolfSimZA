// GolfSim ZA colour boost: richer greens (fairway / rough), bluer sky, a little contrast.
// Used by GolfSimZA.Visual.ColorGrade on the play camera (Settings → VISUAL → COURSE LOOK).
Shader "Hidden/GolfSimZA/ColorGrade"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Saturation;
            float _Vibrance;
            float _Contrast;
            float _Brightness;
            float _GreenBoost;
            float _GreenShift;
            float _GreenLift;
            float _BlueBoost;

            float3 RgbToHsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float4 source = tex2D(_MainTex, i.uv);
                float3 c = saturate(source.rgb);
                float3 hsv = RgbToHsv(c);

                // Grass: hue roughly yellow-green to green (0.14 .. 0.45).
                float green = smoothstep(0.12, 0.20, hsv.x) * (1.0 - smoothstep(0.42, 0.50, hsv.x)) * smoothstep(0.05, 0.18, hsv.y);
                hsv.x += _GreenShift * green;
                hsv.y = saturate(hsv.y * lerp(1.0, _GreenBoost, green));
                hsv.z = saturate(hsv.z * lerp(1.0, _GreenLift, green));

                // Sky: blue hues.
                float blue = smoothstep(0.50, 0.56, hsv.x) * (1.0 - smoothstep(0.68, 0.76, hsv.x));
                hsv.y = saturate(hsv.y * lerp(1.0, _BlueBoost, blue));

                c = HsvToRgb(hsv);

                float luma = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(luma.xxx, c, _Saturation);

                float mx = max(c.r, max(c.g, c.b));
                float mn = min(c.r, min(c.g, c.b));
                float vib = 1.0 + _Vibrance * (1.0 - (mx - mn));
                luma = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(luma.xxx, c, vib);

                c = (c - 0.5) * _Contrast + 0.5;
                c *= _Brightness;
                return float4(saturate(c), source.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
