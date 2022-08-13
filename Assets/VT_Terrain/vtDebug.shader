Shader "Unlit/VTDebug"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	 
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			sampler2D _VT_IndexTex;
			float4 _MainTex_ST;
		 
			Texture2DArray<float4> _VT_AlbedoTex; SamplerState sampler_VT_AlbedoTex;
 
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
			 
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				
			//	fixed4 col = _VT_IndexTex.Sample(sampler_VT_IndexTex, float2(i.uv));
				float4 indexData = tex2D(_VT_IndexTex,  1-i.uv);
				float2 wpos = (1-i.uv.xy) * 1024;
				float2 localUV = (wpos - indexData.yz) / indexData.w;
			fixed4 col = _VT_AlbedoTex.Sample(sampler_VT_AlbedoTex, float3(localUV, indexData.r));
			return float4(1 - i.uv.xy,0,1);//  indexData.w < 8.5;
			}
			ENDCG
		}
	}
}
