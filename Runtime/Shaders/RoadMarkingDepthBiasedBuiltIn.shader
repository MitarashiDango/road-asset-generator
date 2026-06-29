Shader "MitarashiDango/RoadAssetGenerator/RoadMarkingDepthBiasedBuiltIn"
{
    Properties
    {
        [HideInInspector] _MainTex ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        [HideInInspector] _Color ("Legacy Color", Color) = (1,1,1,1)
        [HideInInspector] _Metallic ("Metallic", Range(0,1)) = 0
        [HideInInspector] _Glossiness ("Smoothness", Range(0,1)) = 0.25
        [HideInInspector] _LineTexture ("Line Texture", 2D) = "white" {}
        [HideInInspector] _LineTextureStrength ("Line Texture Strength", Range(0,1)) = 0
        [HideInInspector] _LineTextureTileLengthMeters ("Line Texture Tile Length Meters", Float) = 10
        [HideInInspector] _LineTextureColorInfluence ("Line Texture Color Influence", Range(0,1)) = 0
        [HideInInspector] _WearMask ("Wear Mask", 2D) = "black" {}
        [HideInInspector] _WearMaskStrength ("Wear Mask Strength", Range(0,1)) = 0
        [HideInInspector] _WearMaskTiling ("Wear Mask Tiling", Float) = 0
        [HideInInspector] _WearMaskTileLengthMeters ("Wear Mask Tile Length Meters", Float) = 10
        [HideInInspector] _WearMaskInvert ("Invert Wear Mask", Float) = 0
        [HideInInspector] _WearMaskClipThreshold ("Wear Mask Clip Threshold", Range(0,1)) = 0.72
        [HideInInspector] _MarkingStartDistanceMeters ("Marking Start Distance Meters", Float) = 0
        [HideInInspector] _MarkingLengthMeters ("Marking Length Meters", Float) = 1
        [HideInInspector] _WornSmoothness ("Worn Smoothness", Range(0,1)) = 0.08
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
        sampler2D _LineTexture;
        half _LineTextureStrength;
        float _LineTextureTileLengthMeters;
        half _LineTextureColorInfluence;
        sampler2D _WearMask;
        half _WearMaskStrength;
        half _WearMaskTiling;
        float _WearMaskTileLengthMeters;
        half _WearMaskInvert;
        half _WearMaskClipThreshold;
        float _MarkingStartDistanceMeters;
        float _MarkingLengthMeters;
        half _WornSmoothness;

        struct Input
        {
            float2 uv_MainTex;
        };

        float2 BuildLineTextureUv(float2 uv)
        {
            return float2(uv.x, uv.y / max(_LineTextureTileLengthMeters, 0.05));
        }

        float2 BuildWearMaskUv(float2 uv)
        {
            float stretchV = saturate((uv.y - _MarkingStartDistanceMeters) / max(_MarkingLengthMeters, 0.05));
            float repeatV = uv.y / max(_WearMaskTileLengthMeters, 0.05);
            return float2(uv.x, lerp(stretchV, repeatV, step(0.5, _WearMaskTiling)));
        }

        half SampleWearMask(float2 uv)
        {
            half mask = tex2D(_WearMask, BuildWearMaskUv(uv)).r;
            mask = lerp(mask, 1.0h - mask, saturate(_WearMaskInvert));
            return saturate(mask * _WearMaskStrength);
        }

        half3 ApplyLineTexture(half3 color, float2 uv)
        {
            half3 sampleColor = tex2D(_LineTexture, BuildLineTextureUv(uv)).rgb;
            half sampleLuminance = dot(sampleColor, half3(0.2126h, 0.7152h, 0.0722h));
            half3 tintedDetail = color * sampleLuminance;
            half3 detailColor = lerp(
                tintedDetail,
                sampleColor,
                saturate(_LineTextureColorInfluence));
            return lerp(color, detailColor, saturate(_LineTextureStrength));
        }

        half3 ApplyWearMask(half3 color, half wear)
        {
            return color * lerp(1.0h, 0.35h, wear);
        }

        void Surf(Input input, inout SurfaceOutputStandard output)
        {
            half wear = SampleWearMask(input.uv_MainTex);
            clip(_WearMaskClipThreshold - wear);

            output.Albedo = ApplyWearMask(ApplyLineTexture(_BaseColor.rgb, input.uv_MainTex), wear);
            output.Metallic = _Metallic;
            output.Smoothness = lerp(_Glossiness, _WornSmoothness, wear);
            output.Alpha = _BaseColor.a;
        }
        ENDCG
    }

    FallBack "Standard"
}
