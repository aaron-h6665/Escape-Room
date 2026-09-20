Shader "EscapeRoom/World Stone"
{
    Properties
    {
        _BaseMap("Included stone texture", 2D) = "white" {}
        _BaseColor("Tint", Color) = (0.8,0.8,0.8,1)
        _Tiling("Repeats per meter", Float) = 0.35
        _Mortar("Masonry joints", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float _Tiling;
            float _Mortar;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half3 Stone(float2 uv)
            {
                half3 textureColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _Tiling).rgb;
                if (_Mortar < 0.5) return dot(textureColor, half3(0.3, 0.59, 0.11)).xxx;
                float2 grid = uv / float2(0.95, 0.44);
                grid.x += fmod(floor(grid.y), 2) * 0.5;
                float2 edge = min(frac(grid), 1 - frac(grid));
                float nearestJoint = min(edge.x, edge.y);
                float antialiasWidth = max(fwidth(nearestJoint), 0.002);
                float joint = smoothstep(0.012 - antialiasWidth, 0.035 + antialiasWidth, nearestJoint);
                half stone = dot(textureColor, half3(0.3, 0.59, 0.11));
                return lerp(half3(0.12, 0.13, 0.14), half3(0.50, 0.49, 0.46) * (0.7 + stone), joint);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                // Some legacy ProBuilder walls contain smoothed or malformed vertex
                // normals on otherwise flat faces. Derive the mapping normal from the
                // rendered triangle so adjacent coplanar pieces use the same projection.
                float3 interpolatedNormal = normalize(input.normalWS);
                float3 faceNormal = normalize(cross(ddy(p), ddx(p)));
                faceNormal *= dot(faceNormal, interpolatedNormal) < 0 ? -1 : 1;
                float3 weight = pow(abs(faceNormal), 16);
                weight /= max(dot(weight, 1), 0.0001);
                half3 albedo = Stone(p.zy) * weight.x
                    + Stone(p.xz) * weight.y
                    + Stone(p.xy) * weight.z;
                float3 normal = faceNormal;
                Light main = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 light = max(SampleSH(normal), half3(0.32,0.32,0.32));
                light += main.color * saturate(dot(normal, main.direction)) * main.shadowAttenuation;
                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0; i < count; i++)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    light += extra.color * saturate(dot(normal, extra.direction)) * extra.distanceAttenuation;
                }
                #endif
                return half4(albedo * _BaseColor.rgb * light, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
