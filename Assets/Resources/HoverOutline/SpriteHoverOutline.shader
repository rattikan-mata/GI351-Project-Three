// ขอบสีรอบสไปรต์ตอนเมาส์ชี้ (URP 2D) ขอบตามรูปทรงของสไปรต์จริง (ดูค่า alpha ของพิกเซลรอบๆ)
// ใช้ผ่าน HoverOutlineManager: สลับ Material นี้ให้ SpriteRenderer ชั่วคราวตอนเมาส์ชี้
// ขอบวาดคร่อมเส้นขอบของรูป (ครึ่งหนึ่งในรูป ครึ่งหนึ่งนอกรูป) จึงเห็นขอบแม้สไปรต์ไม่มีขอบโปร่งใสเหลือ
Shader "Sprites/Hover Outline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        _OutlineColor ("Outline Color", Color) = (1,1,1,1)

        // ค่าด้านล่างนี้ HoverOutlineManager ตั้งให้ต่อ SpriteRenderer (MaterialPropertyBlock)
        [HideInInspector] _UVRect ("Sprite UV Rect (minU,minV,maxU,maxV)", Vector) = (0,0,1,1)
        [HideInInspector] _OutlineUV ("Outline UV Radius (u,v)", Vector) = (0.02,0.02,0,0)

        // Legacy properties (ให้ SpriteRenderer เข้ากันได้กับ shader sprite ปกติ)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment OutlineFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                float4 _UVRect;
                float4 _OutlineUV;
            CBUFFER_END

            static const float2 kDirs[8] =
            {
                float2(1.0, 0.0), float2(-1.0, 0.0), float2(0.0, 1.0), float2(0.0, -1.0),
                float2(0.7071, 0.7071), float2(-0.7071, 0.7071), float2(0.7071, -0.7071), float2(-0.7071, -0.7071)
            };

            // alpha ของพิกเซลที่ uv นี้ (นอกขอบรูปของสไปรต์ = โปร่งใส ทำให้ขอบรูปทึบเต็มก็เกิดขอบได้)
            half SpriteAlpha(float2 uv)
            {
                if (uv.x < _UVRect.x || uv.y < _UVRect.y || uv.x > _UVRect.z || uv.y > _UVRect.w)
                    return 0;
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
            }

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                half4 baseColor = CommonUnlitFragment(input, input.color);

                float2 uv = input.uv;
                half self = SpriteAlpha(uv);

                half maxN = 0;
                half minN = 1;

                // วัด 2 วง (ครึ่งรัศมี และเต็มรัศมี) x 8 ทิศ
                [unroll]
                for (int ring = 1; ring <= 2; ring++)
                {
                    float radiusScale = ring * 0.5;
                    [unroll]
                    for (int i = 0; i < 8; i++)
                    {
                        half a = SpriteAlpha(uv + kDirs[i] * _OutlineUV.xy * radiusScale);
                        maxN = max(maxN, a);
                        minN = min(minN, a);
                    }
                }

                half outer = (1.0 - self) * maxN;   // พิกเซลโปร่งใสที่อยู่ติดรูป
                half inner = self * (1.0 - minN);   // พิกเซลของรูปที่อยู่ติดขอบ
                half edge = saturate(max(outer, inner));

                half3 rgb = lerp(baseColor.rgb, _OutlineColor.rgb, edge);
                half alpha = max(baseColor.a, edge * _OutlineColor.a);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
