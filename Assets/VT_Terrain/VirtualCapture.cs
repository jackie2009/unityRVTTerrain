using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public class VirtualCapture : MonoBehaviour {
 
	private Mesh decalQuadMesh;
	private Mesh terrainQuadMesh;
 
	private CommandBuffer drawBuffer;
	private Material captureMat;
	public Shader captureShader;
	public TerrainData terrainData;

	public Texture2DArray albedoAtlas;
	public Texture2DArray normalAtlas;
	public RenderTexture[] clipRTs;
	private RenderBuffer[] mrtRB = new RenderBuffer[2];
	public int mipmapCount;
	public const int virtualTextArraySize = 256;
	// Use this for initialization
	public Shader decalBlitShader;
	private Material captureDecalMat;
	private RVTCompress rVTCompress;
	public ComputeShader compressShader;
	public RenderTexture debugTex0;
	public RenderTexture debugTex1;
	void Awake () {
		terrainQuadMesh = new Mesh();
		terrainQuadMesh.vertices = new Vector3[] { new Vector3(0.0f, 0.0f, 0.1f), new Vector3(1.0f, 0.0f, 0.1f), new Vector3(1.0f, 1.0f, 0.1f), new Vector3(0.0f, 1.0f, 0.1f) };
		terrainQuadMesh.uv = new Vector2[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f) };
		terrainQuadMesh.SetIndices(new int []{ 0, 1, 2, 3 }, MeshTopology.Quads, 0);
		terrainQuadMesh.UploadMeshData(true);

 
		decalQuadMesh = new Mesh();
		decalQuadMesh.vertices = new Vector3[] { new Vector3(-0.5f, -0.5f, 0.1f), new Vector3(0.5f, -0.5f, 0.1f), new Vector3(0.5f, 0.5f, 0.1f), new Vector3(-0.5f, 0.5f, 0.1f) };
		decalQuadMesh.uv = new Vector2[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f) };
		decalQuadMesh.SetIndices(new int[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0);
		decalQuadMesh.UploadMeshData(true);

		captureDecalMat = new Material(decalBlitShader);
		captureDecalMat.enableInstancing = true;
		mipmapCount = (int)Mathf.Log(virtualTextArraySize, 2);
		clipRTs = new RenderTexture[2];// 
		drawBuffer = new CommandBuffer();
		for (int i = 0; i < clipRTs.Length; i++)
		{
 
	    clipRTs[i] = new RenderTexture(virtualTextArraySize, virtualTextArraySize, 0, i == 0 ? RenderTextureFormat.ARGB32 : RenderTextureFormat.ARGB32, i == 0 ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
			clipRTs[i].useMipMap = true;
			clipRTs[i].autoGenerateMips =false;

		clipRTs[i].Create();

		}








		captureMat = new Material(captureShader);
            for (int k = 0; k < terrainData.alphamapTextures.Length; k++)
            {
			captureMat.SetTexture("_Control"+k, terrainData.alphamapTextures[k]);
			}
			
 

		 

		var tileData = new Vector4[terrainData.splatPrototypes.Length];
		for (int i = 0; i < tileData.Length; i++)
		{
			tileData[i] = new Vector4(terrainData.size.x / terrainData.splatPrototypes[i].tileSize.x, terrainData.size.z / terrainData.splatPrototypes[i].tileSize.y, 0, 0);


		}
 
		Shader.SetGlobalTexture("albedoAtlas", albedoAtlas);
		Shader.SetGlobalTexture("normalAtlas", normalAtlas);
		Shader.SetGlobalVectorArray("tileData", tileData);
		Shader.SetGlobalInt("virtualTextArraySize", virtualTextArraySize);

		//mrt mode
		mrtRB = new RenderBuffer[] { clipRTs[0].colorBuffer, clipRTs[1].colorBuffer };
#if RVT_COMPRESS_ON
		rVTCompress = new RVTCompress(compressShader, clipRTs[0], clipRTs[1]);
		debugTex0 = rVTCompress.tex0[0];
		debugTex1 = rVTCompress.tex1[0];
#endif

	}
	void OnDestroy()
	{
		if (clipRTs != null)
		{
			for (int i = 0; i < clipRTs.Length; i++)
			{

				clipRTs[i].Release();


			}
		}
		if (rVTCompress != null) rVTCompress.release();
 

	}
 
#if !RVT_COMPRESS_ON
	public void  virtualCapture_MRT(VT_Terrain.Node item , RenderTexture clipRTAlbedoArray, RenderTexture clipRTNormalArray)
#else
	public void virtualCapture_MRT(VT_Terrain.Node item, Texture2DArray clipRTAlbedoArray, Texture2DArray clipRTNormalArray,Vector3 terrainOffset)
#endif
	{
		MaterialPropertyBlock mpb = new MaterialPropertyBlock();
		Vector2 center = new Vector2(item.x + item.size / 2.0f, item.z + item.size / 2.0f);
		float size = item.size;
		HashSet<Renderer> decals = item.decals;
		int terrainSize = (int)terrainData.size.x;

	//	Shader.SetGlobalVector("blitOffsetScale", new Vector4((center.x - size / 2) / terrainSize, (center.y - size / 2) / terrainSize, (size) / terrainSize, (size) / terrainSize));

		RenderTexture oldRT = RenderTexture.active;

		Graphics.SetRenderTarget(mrtRB,clipRTs[0].depthBuffer);
		
		GL.Clear(false, true, Color.clear);

		GL.PushMatrix();
	    GL.LoadOrtho();
 
		drawBuffer.Clear();
		mpb.SetVector("blitOffsetScale", new Vector4((center.x - size / 2) / terrainSize, (center.y - size / 2) / terrainSize, (size) / terrainSize, (size) / terrainSize));
		drawBuffer.DrawMesh(terrainQuadMesh, Matrix4x4.identity, captureMat, 0, 0, mpb);
		if (decals != null)
		{
			//print("decals:" + item.size + "," + item.physicTexIndex);
			foreach (var decalRender in decals)
			{
				if (item.size < VT_Terrain.Node.patchSize)
				{
 
				 if (decalRender.bounds.max.x - terrainOffset.x < item.aabb.min.x || decalRender.bounds.min.x - terrainOffset.x> item.aabb.max.x) continue;
				 if (decalRender.bounds.max.z - terrainOffset.z< item.aabb.min.z || decalRender.bounds.min.z- terrainOffset.z> item.aabb.max.z) continue;
				}
			 
					var r = decalRender.transform.localRotation;
				var s = decalRender.transform.localScale;
				var re = r.eulerAngles;
				re.x -= 90;
				re.z = -re.y;
				re.y = 0;
				r.eulerAngles = re;
				var t = (decalRender.transform.position - terrainOffset - new Vector3(item.x, decalRender.transform.position.y, item.z)) / size;
			 
				t.y = t.z;
				t.z = 0;


				mpb.SetVector("_MainTex_ST", new Vector4(decalRender.sharedMaterial.mainTextureScale.x, decalRender.sharedMaterial.mainTextureScale.y, decalRender.sharedMaterial.mainTextureOffset.x, decalRender.sharedMaterial.mainTextureOffset.y));

				mpb.SetTexture("_MainTex", decalRender.sharedMaterial.GetTexture("_MainTex"));
				mpb.SetTexture("_BumpMap", decalRender.sharedMaterial.GetTexture("_BumpMap"));
				drawBuffer.DrawMesh(decalQuadMesh, Matrix4x4.TRS(t, r, s / size), captureDecalMat, 0, 0, mpb);
 
			}
		}

		Graphics.ExecuteCommandBuffer(drawBuffer);
		GL.PopMatrix();
		RenderTexture.active = oldRT;

		clipRTs[0].GenerateMips();
		clipRTs[1].GenerateMips();

#if RVT_COMPRESS_ON
		//Graphics.CopyTexture 在5.x 不能支持不同格式之间拷贝 需要用高版本(2019看过是支持的) 我这里是拿2019的源码 修改了5.x引擎源码,所以普通的5.x不能直接用 需要升级引擎 
		for (int i = 0; i < 4; i++)
		{
			rVTCompress.CompressToSmallRT(i);
			Graphics.CopyTexture(rVTCompress.tex0[i], 0, 0,0,0, rVTCompress.tex0[i].width, rVTCompress.tex0[i].height , clipRTAlbedoArray, item.physicTexIndex, i,0,0);
			Graphics.CopyTexture(rVTCompress.tex1[i], 0, 0, 0, 0, rVTCompress.tex1[i].width, rVTCompress.tex1[i].height, clipRTNormalArray, item.physicTexIndex, i,0,0);
		}
#else

		
		for (int i = 0; i < 4; i++)
		{
			Graphics.CopyTexture(clipRTs[0], 0, i, clipRTAlbedoArray, item.physicTexIndex, i);
			Graphics.CopyTexture(clipRTs[1], 0, i, clipRTNormalArray, item.physicTexIndex, i);
		}

#endif

	}

#if UNITY_EDITOR
	[ContextMenu("MakeAlbedoAtlas")]
	// Update is called once per frame
	void MakeAlbedoAtlas()
	{
		var arrayLen= terrainData.splatPrototypes.Length;
	 
		int wid = terrainData.splatPrototypes[0].texture.width;
		int hei = terrainData.splatPrototypes[0].texture.height;

		int widNormal = terrainData.splatPrototypes[0].normalMap.width;
		int heiNormal = terrainData.splatPrototypes[0].normalMap.height;
		albedoAtlas = new Texture2DArray(wid, hei, arrayLen, terrainData.splatPrototypes[0].texture.format, true, false);
		normalAtlas = new Texture2DArray(widNormal, heiNormal, arrayLen, terrainData.splatPrototypes[0].normalMap.format, true, true);

		for (int index = 0; index < arrayLen; index++)
		{
		  

				if (index >= terrainData.splatPrototypes.Length) break;
				print(index);
				for (int k = 0; k < terrainData.splatPrototypes[index].texture.mipmapCount; k++)
				{
					Graphics.CopyTexture(terrainData.splatPrototypes[index].texture, 0, k, albedoAtlas, index, k);

				}
				for (int k = 0; k < terrainData.splatPrototypes[index].normalMap.mipmapCount; k++)
				{
					Graphics.CopyTexture(terrainData.splatPrototypes[index].normalMap, 0, k, normalAtlas, index, k);

				}

			 
		}


	}
#endif
}
