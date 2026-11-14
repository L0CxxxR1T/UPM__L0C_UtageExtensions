// AvatarEx のパーツ単位ブレンドモード・クリッピングマスク用シェーダー
// Utage 本体の Assets/Utage/Resources/Utage-Shader/UI/UI-Outline9Pass.shader を雛形にしている
// Resources 配下に置くことで Shader.Find が常に解決できるようにする（ビルドで剥がれない）
Shader "UtageExtension/AvatarEx/BlendableUI"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		// ブレンドモードに応じて C# 側から Material.SetFloat で設定する（Inspector非表示）
		[HideInInspector] _SrcBlendRGB ("__srcRGB", Float) = 5
		[HideInInspector] _DstBlendRGB ("__dstRGB", Float) = 10
		[HideInInspector] _SrcBlendA ("__srcA", Float) = 1
		[HideInInspector] _DstBlendA ("__dstA", Float) = 10
		[HideInInspector] _ColorOp ("__colorOp", Float) = 0


		_MaskTex0 ("Clip Mask 0", 2D) = "black" {}
		_MaskTex1 ("Clip Mask 1", 2D) = "black" {}
		_MaskTex2 ("Clip Mask 2", 2D) = "black" {}
		_MaskXform0 ("Mask UV Xform 0", Vector) = (1,1,0,0)
		_MaskXform1 ("Mask UV Xform 1", Vector) = (1,1,0,0)
		_MaskXform2 ("Mask UV Xform 2", Vector) = (1,1,0,0)
		_MaskBounds0 ("Mask UV Bounds 0", Vector) = (0,0,1,1)
		_MaskBounds1 ("Mask UV Bounds 1", Vector) = (0,0,1,1)
		_MaskBounds2 ("Mask UV Bounds 2", Vector) = (0,0,1,1)
		_MaskCount ("Mask Count", Float) = 0
		_MaskInvert ("Invert Mask", Float) = 0

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}

		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		// RGBとAlphaで別々のブレンド式を使う（RenderTexture併用時のUtage独自アルファ規約に対応するため）
		Blend [_SrcBlendRGB] [_DstBlendRGB], [_SrcBlendA] [_DstBlendA]
		ColorMask [_ColorMask]

		Pass
		{
			Name "Default"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#include "AvatarExBlendableUI.cginc"
			ENDCG
		}
	}
}
