using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//因为unity 5.6的 computeshader setTexture 还不支持mipmap参数所以 需要创建多张小图做mipmap用 比较麻烦 高版本unity简易用 带mipmap参数的setTexture 重写此类 
public class RVTCompress  {
    public  ComputeShader shader;
 
    int kernelHandle;
    int[] DestRect;
    public RenderTexture []tex0;//array for mipmap
    public RenderTexture [] tex1;//array for mipmap
    private RenderTexture []albedoSmoothMips;//array for mipmap
    private RenderTexture []normalMips;//array for mipmap
    private const int MipmapCount= 4;
    public     RVTCompress(ComputeShader shader, RenderTexture albedoSmooth, RenderTexture normal) {
        this.shader = shader;
        albedoSmoothMips = new RenderTexture[MipmapCount];
        albedoSmoothMips[0] = albedoSmooth;
        for (int i = 1; i < MipmapCount; i++)
        {
            var rt=new RenderTexture(albedoSmooth.width>>i,albedoSmooth.height>>i,0,albedoSmooth.format, RenderTextureReadWrite.sRGB);
            rt.Create();
            albedoSmoothMips[i] = rt;
        }

        normalMips = new RenderTexture[MipmapCount];
        normalMips[0] = normal;
        for (int i = 1; i < MipmapCount; i++)
        {
            var rt = new RenderTexture(normal.width >> i, normal.height >> i, 0, normal.format, RenderTextureReadWrite.Linear);
            rt.Create();
            normalMips[i] = rt;
        }

        DestRect = new int[4] { 0, 0, albedoSmooth.width, albedoSmooth.height };

        kernelHandle = shader.FindKernel("CSMain");


        tex0 = new RenderTexture[MipmapCount];
        for (int i = 0; i < tex0.Length; i++)
        {
            var tex = new RenderTexture((albedoSmooth.width>>i) / 4, (albedoSmooth.height>>i) / 4, 0)
            {
                format = RenderTextureFormat.ARGBFloat,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false
            };
            tex.name = "RVTCompressRT0_mip"+i;
            tex.Create();
            tex0[i] = tex;
        }
        tex1 = new RenderTexture[MipmapCount];
        for (int i = 0; i < tex1.Length; i++)
        {
            var tex = new RenderTexture((albedoSmooth.width>>i) / 4, (albedoSmooth.height>>i) / 4, 0)
            {
                format = RenderTextureFormat.ARGBFloat,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false
            };
            tex.name = "RVTCompressRT1_mip" + i;
            tex.Create();
            tex1[i] = tex;
        }


        shader.SetInts("DestRect", DestRect);


    }
    public void CompressToSmallRT(int mipmapLevel) {
        if (mipmapLevel > 0) {
            Graphics.CopyTexture(albedoSmoothMips[0], 0, mipmapLevel, albedoSmoothMips[mipmapLevel], 0, 0);
            Graphics.CopyTexture(normalMips[0], 0, mipmapLevel, normalMips[mipmapLevel], 0, 0);
        }
        shader.SetTexture(kernelHandle, "Result0", tex0[mipmapLevel]);
        shader.SetTexture(kernelHandle, "RenderTexture0", albedoSmoothMips[mipmapLevel]);

        shader.SetTexture(kernelHandle, "Result1", tex1[mipmapLevel]);
        shader.SetTexture(kernelHandle, "RenderTexture1", normalMips[mipmapLevel]);
        shader.SetInt("mipmapLevel", mipmapLevel);
        shader.Dispatch(kernelHandle, (DestRect[2] / 4/ (1<<mipmapLevel) + 7) / 8, (DestRect[3] / 4/(1 << mipmapLevel) + 7) / 8, 1);
       
    }

    public void release()
    {
        if (tex0 != null) {
            for (int i = 0; i < MipmapCount; i++)
            {
                tex0[i].Release();
            }
        }
        if (tex1 != null)
        {
            for (int i = 0; i < MipmapCount; i++)
            {
                tex1[i].Release();
            }
        }

        if (albedoSmoothMips != null)
        {
            //0 是外部创建,本着谁创建 谁回收原则 这里不做回收
            for (int i = 1; i < MipmapCount; i++)
            {
                albedoSmoothMips[i].Release();
            }
        }
        if (normalMips != null)
        {
            //0 是外部创建,本着谁创建 谁回收原则 这里不做回收
            for (int i = 1; i < MipmapCount; i++)
            {
                normalMips[i].Release();
            }
        }
    }
}
