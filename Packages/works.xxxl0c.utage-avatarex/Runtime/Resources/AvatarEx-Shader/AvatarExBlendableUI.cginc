// AvatarEx 用 uGUI シェーダー本体
// Utage 本体の UtageUIOutline.cginc（UI-Default 準拠、最新世代）を雛形にしている

#include "UnityCG.cginc"
#include "UnityUI.cginc"

#pragma multi_compile __ UNITY_UI_CLIP_RECT
#pragma multi_compile __ UNITY_UI_ALPHACLIP

struct appdata_t
{
	float4 vertex   : POSITION;
	float4 color    : COLOR;
	float2 texcoord : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
	float4 vertex   : SV_POSITION;
	fixed4 color    : COLOR;
	float2 texcoord : TEXCOORD0;
	float4 worldPosition : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};

sampler2D _MainTex;
fixed4 _Color;
fixed4 _TextureSampleAdd;
float4 _ClipRect;
float4 _MainTex_ST;

// ブレンドモードの色前処理（0=なし 1=プリマルチプライ 2=白へlerp）
float _ColorOp;

// メインスプライトのアトラス内位置（オフセットxy・スケールzw、0〜1正規化）
float4 _MainUvRect;

// クリッピングマスク（最大3ソース）
sampler2D _MaskTex0;
sampler2D _MaskTex1;
sampler2D _MaskTex2;
float4 _MaskXform0; // (scale.xy, offset.zw)
float4 _MaskXform1;
float4 _MaskXform2;
float4 _MaskBounds0; // (min.xy, max.zw)
float4 _MaskBounds1;
float4 _MaskBounds2;
float _MaskCount;
float _MaskInvert;

v2f vert(appdata_t v)
{
	v2f OUT;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

	OUT.worldPosition = v.vertex;
	OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
	OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
	OUT.color = v.color * _Color;
	return OUT;
}

// マスク1スロット分のアルファを取得する。範囲外は0（隣接アトラス領域を誤って拾わないようにクランプ）
half SampleMaskAlpha(sampler2D tex, float2 localUv, float4 xform, float4 bounds)
{
	float2 uv = localUv * xform.xy + xform.zw;
	half inside = step(bounds.x, uv.x) * step(uv.x, bounds.z) * step(bounds.y, uv.y) * step(uv.y, bounds.w);
	return tex2D(tex, uv).a * inside;
}

fixed4 frag(v2f IN) : SV_Target
{
	half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

	// ブレンドモードごとの色前処理
	// ストレートアルファの透過パーツ画像に固定Blendステートをそのまま適用すると
	// 透明部分が黒くなる（Multiply）等の破綻が起きるため、rgbを事前加工する
	if (_ColorOp > 1.5)
	{
		// Multiply: 透明部分を白へ寄せてから乗算ブレンドに渡す
		color.rgb = lerp(half3(1, 1, 1), color.rgb, color.a);
	}
	else if (_ColorOp > 0.5)
	{
		// Additive / Screen: アルファをrgbへプリマルチプライ
		color.rgb *= color.a;
	}

	// クリッピングマスク（最大3ソースのOR=max合成、UVはアトラス位置・サイズ差を吸収してリマップ済み）
	if (_MaskCount > 0.5)
	{
		float2 local = (IN.texcoord - _MainUvRect.xy) / _MainUvRect.zw;
		half m = SampleMaskAlpha(_MaskTex0, local, _MaskXform0, _MaskBounds0);
		if (_MaskCount > 1.5)
		{
			m = max(m, SampleMaskAlpha(_MaskTex1, local, _MaskXform1, _MaskBounds1));
		}
		if (_MaskCount > 2.5)
		{
			m = max(m, SampleMaskAlpha(_MaskTex2, local, _MaskXform2, _MaskBounds2));
		}
		m = lerp(m, 1.0 - m, _MaskInvert);
		color.a *= m;
	}

	#ifdef UNITY_UI_CLIP_RECT
	color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
	#endif

	#ifdef UNITY_UI_ALPHACLIP
	clip(color.a - 0.001);
	#endif

	return color;
}
