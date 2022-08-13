// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

Shader "VT_Terrain" {
 
		Properties{
			[HideInInspector] _Control("Control (RGBA)", 2D) = "red" {}
	 
		}

			CGINCLUDE
#pragma surface surf Lambert vertex:SplatmapVert2 finalcolor:SplatmapFinalColor finalprepass:SplatmapFinalPrepass finalgbuffer:SplatmapFinalGBuffer noinstancing
#pragma multi_compile_fog
#include "TerrainSplatmapCommon.cginc"
 
			sampler2D _VT_IndexTex;
		int VT_RootSize;
		UNITY_DECLARE_TEX2DARRAY(_VT_AlbedoTex);
		UNITY_DECLARE_TEX2DARRAY(_VT_NormalTex);
 
				 float3 mainCamPos;
				 int virtualTextArraySize;
			 
			void SplatmapVert2(inout appdata_full v, out Input data)
			{
				UNITY_INITIALIZE_OUTPUT(Input, data);
				 

				data.tc_Control = TRANSFORM_TEX(v.texcoord, _Control);  // Need to manually transform uv here, as we choose not to use 'uv' prefix for this texcoord.

				float4 pos = UnityObjectToClipPos(v.vertex);
				UNITY_TRANSFER_FOG(data, pos);

				 
				v.tangent.xyz = cross(v.normal, float3(0, 0, 1));
				v.tangent.w = -1;
 
			}
				void surf(Input IN, inout SurfaceOutput o)
			{
				half4 splat_control;
				half weight=1;
				fixed4 mixedDiffuse;
				 
				float4 indexData = tex2D(_VT_IndexTex,  (IN.tc_Control));
				float2 wpos = IN.tc_Control * VT_RootSize;
				int lod = (int)(log2(indexData.w) + 0.5);
				float2 localUV =saturate( (wpos - indexData.yz ) / indexData.w);
				//float lodBiasScale = 0.5;
				//float2 dx_vtc = ddx(wpos* virtualTextArraySize * lodBiasScale);
				//float2 dy_vtc = ddy(wpos* virtualTextArraySize * lodBiasScale);

				 float lodBias  =-0.65;
				 float2 dx_vtc = ddx(wpos* virtualTextArraySize);
				 float2 dy_vtc = ddy(wpos* virtualTextArraySize );
				float md = max(dot(dx_vtc, dx_vtc), dot(dy_vtc, dy_vtc));
				float mipmap= clamp( 0.5 * log2(md)-lod+ lodBias,0,3);
				 
			//	  localUV = lerp((1<< (5 - lod))/ 256.0, 1 - (1 << (5 - lod)) / 256.0, localUV);

				fixed4 albedo = UNITY_SAMPLE_TEX2DARRAY_LOD(_VT_AlbedoTex, float3(localUV, indexData.r), mipmap);
				float3 normal = UNITY_SAMPLE_TEX2DARRAY_LOD(_VT_NormalTex, float3(localUV, indexData.r), mipmap);
				normal =  normal * 2 - 1;

 

				mixedDiffuse = albedo;// mipmap / 4;
					o.Normal = normal;
 

					o.Albedo =  mixedDiffuse.rgb;//  +lod * 0.05;
				  o.Gloss = 0;
				o.Alpha = weight;
			}
			ENDCG

				Category{
					Tags {
						"Queue" = "Geometry-99"
						"RenderType" = "Opaque"
					}
				// TODO: Seems like "#pragma target 3.0 _TERRAIN_NORMAL_MAP" can't fallback correctly on less capable devices?
				// Use two sub-shaders to simulate different features for different targets and still fallback correctly.
				SubShader { // for sm3.0+ targets
					CGPROGRAM
						#pragma target 3.0
						#pragma multi_compile __ _TERRAIN_NORMAL_MAP
					ENDCG
				}
				SubShader { // for sm2.0 targets
					CGPROGRAM
					ENDCG
				}
			}

		 

				Fallback "Diffuse"
	}
