// GolfSim ZA water for courses built from OpenStreetMap: lakes, ponds and rivers.
//  * Clear at the edges and deeper colour further in (from the depth of the ground under the water).
//  * Sky reflection that grows at a glancing angle (fresnel), from the GolfSim ZA sky reflection probe.
//  * Moving ripples (three layers), a sun glint, a soft light line where the water meets the bank.
// Needs the camera depth texture (the course loader turns it on); a camera without it (the
// top-down hole map) gets an even water colour.
Shader "Hidden/GolfSimZA/CourseWater"
{
    Properties
    {
        _ShallowColor ("Shallow water", Color) = (0.17, 0.40, 0.38, 1)
        _DeepColor ("Deep water", Color) = (0.03, 0.14, 0.19, 1)
        _DepthScale ("Depth to deep colour (m)", Float) = 2.2
        _ClearDepth ("Depth to opaque (m)", Float) = 1.1
        _BumpMap ("Ripples (normal map)", 2D) = "bump" {}
        _WaveScale ("Ripple size (m)", Float) = 11
        _WaveSpeed ("Ripple speed", Float) = 0.30
        _WaveStrength ("Ripple strength", Range(0, 2)) = 0.35
        _Reflection ("Reflection", Range(0, 1.5)) = 1
        _ShoreLine ("Bank line", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _BumpMap;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _CameraDepthTexture_TexelSize;
            fixed4 _ShallowColor, _DeepColor;
            float _DepthScale, _ClearDepth, _WaveScale, _WaveSpeed;
            half _WaveStrength, _Reflection, _ShoreLine;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float4 screen : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screen = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screen.z);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float3 Ripples(float2 xz)
            {
                float2 uv = xz / max(0.5, _WaveScale);
                float t = _Time.y * _WaveSpeed;
                float3 n1 = UnpackNormal(tex2D(_BumpMap, uv + float2(t * 0.11, t * 0.07)));
                float3 n2 = UnpackNormal(tex2D(_BumpMap, uv * 1.73 + float2(-t * 0.09, t * 0.12)));
                float3 n3 = UnpackNormal(tex2D(_BumpMap, uv * 4.1 + float2(t * 0.21, -t * 0.17)));
                float2 slope = (n1.xy + n2.xy * 0.8 + n3.xy * 0.35) * _WaveStrength;
                // Tangent space xy -> world xz (the surface faces up).
                return normalize(float3(slope.x, 1.0, slope.y));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float distance = length(_WorldSpaceCameraPos - i.world);
                float3 n = normalize(lerp(Ripples(i.world.xz), float3(0, 1, 0), saturate(distance / 450.0) * 0.7));
                float ndv = saturate(dot(n, view));

                // Water thickness under this point (along the view, turned into depth below the surface).
                float depth = _DepthScale * 2.0;
                bool ortho = unity_OrthoParams.w > 0.5;
                if (!ortho)
                {
                    float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                    float thickness = max(0.0, scene - i.screen.z);
                    // A depth texture that is not there reads as "far away": treat as deep.
                    depth = thickness * saturate(view.y + 0.15);
                    if (scene > 5000.0) depth = _DepthScale * 2.0;
                }

                float deep = 1.0 - exp(-depth / max(0.05, _DepthScale));
                float3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, deep);
                // Lit by the sky and the sun from above.
                float3 sky = ShadeSH9(float4(0, 1, 0, 1));
                float sunUp = saturate(_WorldSpaceLightPos0.y);
                body *= sky * 0.55 + _LightColor0.rgb * (0.22 + 0.3 * sunUp);

                // Sky / surroundings reflection, stronger at a glancing angle.
                float3 r = reflect(-view, n);
                half4 probe = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, r, 1.0);
                float3 reflection = DecodeHDR(probe, unity_SpecCube0_HDR);
                float fresnel = 0.025 + 0.975 * pow(1.0 - ndv, 5.0);
                float3 colour = lerp(body, reflection, saturate(fresnel * _Reflection));

                // Sun glint on the ripples.
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 h = normalize(l + view);
                float glint = pow(saturate(dot(n, h)), 600.0) * 6.0 + pow(saturate(dot(n, h)), 90.0) * 0.25;
                colour += _LightColor0.rgb * glint;

                // Clear at the very edge, soft light line where it meets the bank.
                float alpha = ortho ? 0.9 : lerp(0.25, 0.94, 1.0 - exp(-depth / max(0.05, _ClearDepth)));
                alpha = saturate(alpha + fresnel * 0.5);
                float edge = ortho ? 0.0 : (1.0 - saturate(depth / 0.18));
                float sparkle = 0.6 + 0.4 * sin(i.world.x * 2.3 + i.world.z * 1.7 + _Time.y * 1.5);
                colour = lerp(colour, float3(0.85, 0.88, 0.86) * (sky * 0.5 + _LightColor0.rgb * 0.5), edge * _ShoreLine * sparkle);
                alpha = max(alpha, edge * _ShoreLine * 0.8);

                fixed4 c = fixed4(colour, alpha);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
