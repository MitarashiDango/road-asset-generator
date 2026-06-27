Shader "MitarashiDango/RoadAssetGenerator/RoadMarkingDepthBiasedBuiltIn"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        [HideInInspector] _Color ("Legacy Color", Color) = (1,1,1,1)
        [HideInInspector] _Metallic ("Metallic", Range(0,1)) = 0
        [HideInInspector] _Glossiness ("Smoothness", Range(0,1)) = 0.25
        [HideInInspector] _OffsetFactor ("Depth Bias Factor", Float) = -1
        [HideInInspector] _OffsetUnits ("Depth Bias Units", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+20"
        }
        LOD 200

        ZWrite On
        ZTest LEqual
        Cull Back
        Offset [_OffsetFactor], [_OffsetUnits]

        CGPROGRAM
        #pragma target 3.0
        #pragma surface Surf Standard fullforwardshadows
        #pragma multi_compile_instancing

        fixed4 _BaseColor;
        half _Metallic;
        half _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
        };

        void Surf(Input input, inout SurfaceOutputStandard output)
        {
            output.Albedo = _BaseColor.rgb;
            output.Metallic = _Metallic;
            output.Smoothness = _Glossiness;
            output.Alpha = _BaseColor.a;
        }
        ENDCG
    }

    FallBack "Standard"
}
