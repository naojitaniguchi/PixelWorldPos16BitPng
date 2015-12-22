Shader "Custom/CopyFrameAndCalcWorldPosition" {


	Properties{
	// この辺にマトリクスを入れる
	
	}

	CGINCLUDE
#include "UnityCG.cginc"
		//#pragma multi_compile ___ UNITY_HDR_ON
#pragma multi_compile ___ OFFSCREEN



	sampler2D _TmpFrameBuffer;				// これは何
	sampler2D _CameraGBufferTexture0;
	sampler2D _CameraGBufferTexture1;
	sampler2D _CameraGBufferTexture2;
	sampler2D _CameraGBufferTexture3;
	sampler2D_float _CameraDepthTexture;
	sampler2D _TmpRenderTarget;
	int _InvertY;
	float4x4 mat_vp_inv;



	struct v2f {
		float4 pos : POSITION;
		float4 spos : TEXCOORD0;
	};

	v2f vert(appdata_img v)
	{
		v2f o;
		o.pos = o.spos = v.vertex;
		return o;
	}


	float2 get_texcoord(v2f i)
	{
		float2 t = i.spos.xy * 0.5 + 0.5;
		return t;
	}

	float2 get_texcoord_gb(v2f i)
	{
		float2 t = i.spos.xy * 0.5 + 0.5;
#if !defined(UNITY_UV_STARTS_AT_TOP)
		t.y = 1.0 - t.y;
#endif
		return t;
	}


	half4 copy_framebuffer(v2f i) : SV_Target
	{
		float2 t = get_texcoord(i);
#if !defined(OFFSCREEN) || !defined(UNITY_UV_STARTS_AT_TOP)
		t.y = 1.0 - t.y;
#endif
		half4 r = tex2D(_TmpFrameBuffer, t);
		r.a = 1.0;
		return r;
	}

	// ここはUnityの定義に合わせて定義してある
	// diffuse & occlusion ここの occlusion は計算されている？
	// カメラのocclusion cullingで使われる？
	// G-bufferの構成
	// emission 自己発光
	// 
	struct gbuffer_out
	{
		half4 diffuse           : SV_Target0; // RT0: diffuse color (rgb), occlusion (a)
		half4 spec_smoothness   : SV_Target1; // RT1: spec color (rgb), smoothness (a)
		half4 normal            : SV_Target2; // RT2: normal (rgb), --unused, very low precision-- (a) 
		half4 emission          : SV_Target3; // RT3: emission (rgb), --unused-- (a)
	};

	// ここでつなげている
	// 別のG-Bufferにコピーしている？
	gbuffer_out copy_gbuffer(v2f i)
	{
		float2 t = get_texcoord_gb(i);
		gbuffer_out o;
		o.diffuse = tex2D(_CameraGBufferTexture0, t);
		o.spec_smoothness = tex2D(_CameraGBufferTexture1, t);
		o.normal = tex2D(_CameraGBufferTexture2, t);
		o.emission = tex2D(_CameraGBufferTexture3, t);
		return o;
	}

	float4 copy_depth(v2f i) : SV_Target
	{
		return tex2D(_CameraDepthTexture, get_texcoord_gb(i)).rrrr;
	}

	float4 calc_pixel_pos(v2f i) : SV_Target
	{
		float4 screen_pos;
		screen_pos.x = i.spos.x;
		screen_pos.y = i.spos.y;
		screen_pos.z = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, get_texcoord_gb(i)));
		screen_pos.w = 1.0f;

		float4 world_pos = mul(mat_vp_inv, screen_pos);

		world_pos.x /= world_pos.w;
		world_pos.y /= world_pos.w;
		world_pos.z /= world_pos.w;

		// inverse X Y
		world_pos.x *= -1.0f;
		//world_pos.y *= -1.0f;

		return world_pos;
	}

	float4 calc_mask_by_z(v2f i) : SV_Target
	{
		float z = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, get_texcoord_gb(i)));

		float4 mask ;
		if (z >= 1.0f ) {
			mask.x = 0.0f;
			mask.y = 0.0f;
			mask.z = 0.0f;
			mask.w = 0.0f;
		}
		else {
			mask.x = 1.0f;
			mask.y = 1.0f;
			mask.z = 1.0f;
			mask.w = 1.0f;
		}

		return mask;
	}

		half4 copy_rendertarget(v2f i) : SV_Target
	{
		return tex2D(_TmpRenderTarget, get_texcoord_gb(i));
	}
		ENDCG

		Subshader {
		// Pass 0: copy_framebuffer
		Pass{
			Blend Off Cull Off ZTest Off ZWrite Off
			CGPROGRAM
#pragma vertex vert
#pragma fragment copy_framebuffer
			ENDCG
		}

			// Pass 1: copy_gbuffer
			Pass{
			Blend Off Cull Off ZTest Off ZWrite Off
			CGPROGRAM
#pragma vertex vert
#pragma fragment copy_gbuffer
			ENDCG
		}

			// Pass 2: copy_depth
			Pass{
			Blend Off Cull Off ZTest Off ZWrite Off
			CGPROGRAM
#pragma vertex vert
#pragma fragment copy_depth
			ENDCG
		}


			// Pass 3: copy_rendertarget
			Pass{
			Blend Off Cull Off ZTest Off ZWrite Off
			CGPROGRAM
#pragma vertex vert
#pragma fragment copy_rendertarget
			ENDCG
		}

			// Pass 4: calc pixel potition
			Pass{
			Blend Off Cull Off ZTest Off ZWrite Off
			CGPROGRAM
#pragma vertex vert
#pragma fragment calc_pixel_pos
			ENDCG
		}

			// Pass 5: calc mask
			Pass{
			Blend Off Cull Off ZTest Off ZWrite Off
			CGPROGRAM
#pragma vertex vert
#pragma fragment calc_mask_by_z
			ENDCG
		}

	}

	Fallback off
}
