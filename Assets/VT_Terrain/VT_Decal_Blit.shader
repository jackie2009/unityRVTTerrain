﻿Shader "VT_Decal_Blit"
{
	Properties
	{  
		_MainTex("MainTex", 2D) = "white" {}
		_BumpMap("BumpMap", 2D) = "bump" {}

	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		
		Pass
		{
			cull off
			ztest always
		zwrite off
		blend srcAlpha oneMinusSrcAlpha
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
          

			#include "UnityCG.cginc"
 
			sampler2D _MainTex;
		float4 _MainTex_ST;

		sampler2D _BumpMap;
 
		float4  blitOffsetScale;
 
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
 
			};
			struct PixelOutput {
				float4 col0 : COLOR0;
				float4 col1 : COLOR1;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
			 
				float4 vertex : SV_POSITION;
			};

		 
			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				
				//v.uv.xy *= blitOffsetScale.zw;
				//v.uv.xy += blitOffsetScale.xy;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
			  
				return o;
			}
 

			PixelOutput frag(v2f i) 
			{
 
			PixelOutput po;
			po.col0 =  tex2D(_MainTex, i.uv);
			clip(po.col0 - 0.001);
			po.col1 =   tex2D(_BumpMap,i.uv);
 
			return po;
			}
 
 
				ENDCG
			}
	}
}