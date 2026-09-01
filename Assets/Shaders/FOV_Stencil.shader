Shader "Custom/FOV_Stencil"
{
    Properties
    {
        _Color ("Light Color", Color) = (1,1,1,0.5) // เพิ่มตัวแปรสี
    }
    SubShader
    {
        Tags { "Queue" = "Geometry-1" "RenderType" = "Transparent" }
        
        // ลบ ColorMask 0 ออก เพื่อให้ยอมให้วาดสีลงหน้าจอได้
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha // เพิ่มการคำนวณความโปร่งใส

        Stencil
        {
            Ref 1
            Pass Replace
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            half4 _Color;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                return _Color; // แสดงสีตามที่ตั้งค่าใน Material
            }
            ENDHLSL
        }
    }
}