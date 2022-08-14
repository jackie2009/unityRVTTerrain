 
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Profiling;
 

public class VT_Terrain : MonoBehaviour
{

    public class Node
    {
    
       
    
        public static Queue<Node> currentAllLeaves ;
        private static Queue<Node> nextAllLeaves ;
      

        public static Queue<int> physicEmptyIndexQueue;

   

        private static int getPhysicIndex() {   return physicEmptyIndexQueue.Count == 0?-1:physicEmptyIndexQueue.Dequeue(); }
        private static void resetPhysicIndex(Node node) { if(node.physicTexIndex>-1)  physicEmptyIndexQueue.Enqueue(node.physicTexIndex); }
        private static Action<Node> onLoadData;
        private static int splitCount;
        private   const int eventFrameSplitCountMax= 1;

        public int x;
        public int z;
        public int size;

        public Node[] children;
        public Node parent;
        public bool isLeaf { get { return children == null; } }
        public bool parentMerged;
        public int physicTexIndex =-1;
  
       
        public static Node createRoot(int rootSize, int physicIndexCount, Action<Node> onLoadData) {
            Node.onLoadData = onLoadData;
            currentAllLeaves = new Queue<Node>();
            nextAllLeaves = new Queue<Node>();
            physicEmptyIndexQueue = new Queue<int>();
       
            for (int i = 0; i < physicIndexCount; i++)
            {
                physicEmptyIndexQueue.Enqueue(i);
            }
           var root = new Node();
            root.physicTexIndex = getPhysicIndex();
            root.size = rootSize;
            currentAllLeaves.Enqueue(root);




            return root;
        }

        public static void updateAllLeavesState(Vector2 camPos) {
            splitCount = 0;
            nextAllLeaves.Clear();
         
            while (currentAllLeaves.Count > 0) {
                var node = currentAllLeaves.Dequeue();
                node.updateState(camPos);
               
            }
       

            var tempList = currentAllLeaves;
            currentAllLeaves = nextAllLeaves;
            nextAllLeaves = tempList;
            

        }

        private void updateState(Vector2 camPos)
        {
            if (this.parentMerged) return;

            if (parent != null)
            {
                int parent_lodSize = parent.calculateLodSize(camPos);
                bool allBrothersAreLeaf = true;
                for (int i = 0; i < 4; i++)
                {
                    if (parent.children[i].isLeaf == false) allBrothersAreLeaf = false;
                }
                if (parent.size <= parent_lodSize&& allBrothersAreLeaf)
                {
                    parent.merge();
                    return;
                }
                

            }
            
            int lodSize = calculateLodSize(camPos);

            //当前尺寸刚好符合  lod需要的尺寸 自己保持为叶子 不动
            if (size == lodSize)

            {
                nextAllLeaves.Enqueue(this);
            }
            //当前尺寸 大于 lod需要的尺寸 需要细分出4个子对象
            else if (size > lodSize)
            {
                //不马上细分 而是合并完了再细分 这样 同时存在的叶子数就比较小 否则需要更多的对象数量
                if (splitCount++ < eventFrameSplitCountMax &&    physicEmptyIndexQueue.Count >=3)
                {
                    split();

                }
                else {
                    nextAllLeaves.Enqueue(this);
                }
             



            }
            else {
                nextAllLeaves.Enqueue(this);
            }
          


        }

        //细分node 给自己增加4个子node 但自己不算做叶子 所以不放队列
        private void split()
        {
            resetPhysicIndex(this);
        
            children = new Node[4];

            //为了可读性 这里坐标设置 不写循环里, 也不做成对象池， 后面最好做下
            children[0] = new Node() { x = x, z = z };
            children[1] = new Node() { x = x + size / 2, z = z};
            children[2] = new Node() { x = x, z = z + size / 2 };
            children[3] = new Node() { x = x + size / 2, z = z + size / 2};
            for (int i = 0; i < 4; i++)
            {
                children[i].parent = this;
                children[i].size = size / 2;
                children[i].physicTexIndex = getPhysicIndex();
                nextAllLeaves.Enqueue(children[i]);
                onLoadData(children[i]);

            }
         
          

        }

        // 合并node  队列放入parent 不放自己，并跳过后面3个同级node计算 也就不会放入队列
        private void merge()
        {

            

            
            nextAllLeaves.Enqueue(this);
         
            //var tempParent = parent;
            for (int i = 0; i < 4; i++)
            {
                resetPhysicIndex(children[i]);
                children[i].parent = null;
                children[i].parentMerged = true;

            }
             physicTexIndex = getPhysicIndex();
            onLoadData(this);
            children = null;

       

            //Node[] brothers = parent.children;

            //parent.children = null;

            //nextAllLeaves.Enqueue(parent);
            //onLoadData(parent);
            //var tempParent = parent;
            //for (int i = 0; i < 4; i++)
            //{
            //    resetPhysicIndex(brothers[i]);
            //    brothers[i].parent = null;
            //}
            //tempParent.physicTexIndex = getPhysicIndex();

            //currentAllLeaves.Dequeue();
            //currentAllLeaves.Dequeue();
            //currentAllLeaves.Dequeue();

        }
        private int calculateLodSize(Vector2 camPos)
        {
            var dis = CalculateClosestPoint(camPos, new Vector2(x + size / 2, z + size / 2), new Vector2(size / 2, size / 2));
            dis = Mathf.Max(1, Mathf.Sqrt(dis));
         // int lod = Mathf.Max(0, (int)(Mathf.Log(dis, 2) + 0.5));
              int lod = Mathf.Max(0, (int)(Mathf.Log(dis, 2) + 0.5));
          
          
            return 1 << lod;
        }

        float CalculateClosestPoint(Vector2 pos, Vector2 centerPos, Vector2 aabbExt)
        {

            // compute coordinates of point in box coordinate system
            Vector2 closestPos = pos - centerPos;

            // project test point onto box
            float fSqrDistance = 0;
            float fDelta = 0;

            for (int i = 0; i < 2; i++)
            {
                if (closestPos[i] < -aabbExt[i])
                {
                    fDelta = closestPos[i] + aabbExt[i];
                    fSqrDistance += fDelta * fDelta;
                    closestPos[i] = -aabbExt[i];
                }
                else if (closestPos[i] > aabbExt[i])
                {
                    fDelta = closestPos[i] - aabbExt[i];
                    fSqrDistance += fDelta * fDelta;
                    closestPos[i] = aabbExt[i];
                }
            }

            return fSqrDistance;
        }

         

  


        




    }
    Node root;
    RenderTexture clipRTAlbedoArray;
    RenderTexture clipRTNormalArray;
    public int rootSize = 1024;
    public Vector3 terrainOffset ;
    public ComputeShader indexGenerator;
    public RenderTexture indexRT;

    private VirtualCapture virtualCapture;
   
    void Start()
    {

        virtualCapture = GetComponent<VirtualCapture>();
        indexRT = new RenderTexture(rootSize, rootSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        indexRT.useMipMap = false;
        indexRT.autoGenerateMips = false;
        indexRT.enableRandomWrite = true;
        indexRT.filterMode = FilterMode.Point;
        indexRT.Create();
        indexGenerator.SetTexture(0, "Result", indexRT);

        int mipmapCount = (int)Mathf.Log(indexRT.width, 2);
        print(mipmapCount);





        clipRTAlbedoArray = new RenderTexture(VirtualCapture.virtualTextArraySize, VirtualCapture.virtualTextArraySize, 0, RenderTextureFormat.ARGB32);
        clipRTAlbedoArray.volumeDepth = 256 + 128;
        clipRTAlbedoArray.wrapMode = TextureWrapMode.Clamp;
        clipRTAlbedoArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        clipRTAlbedoArray.useMipMap = true;
        clipRTAlbedoArray.autoGenerateMips = false;
        clipRTAlbedoArray.Create();


        clipRTNormalArray = new RenderTexture(VirtualCapture.virtualTextArraySize, VirtualCapture.virtualTextArraySize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        clipRTNormalArray.volumeDepth = clipRTAlbedoArray.volumeDepth;
        clipRTNormalArray.wrapMode = TextureWrapMode.Clamp;
        clipRTNormalArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        clipRTNormalArray.useMipMap = true;
        clipRTNormalArray.autoGenerateMips = false;
        clipRTNormalArray.Create();




     

      
     

         root =   Node.createRoot(rootSize, clipRTAlbedoArray.volumeDepth, onLoadNodeData);
        Shader.SetGlobalInt("VT_RootSize", rootSize);
        //  root =   Node.createRoot(16,100, onLoadNodeData);
       

    }

 

    void OnDestroy()
    {
        if (indexRT != null) indexRT.Release();
        if (clipRTAlbedoArray != null) clipRTAlbedoArray.Release();
        if (clipRTNormalArray != null) clipRTNormalArray.Release();

    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (root == null) return;

        Gizmos.color = Color.green;

      

        int leafCount = 0;
        foreach (var item in Node.currentAllLeaves)
        {

        
           
            Gizmos.DrawWireCube(terrainOffset+new Vector3(item.x + item.size / 2.0f, 0, item.z + item.size / 2.0f), new Vector3(1, 0, 1) * item.size);
            UnityEditor.Handles.Label(terrainOffset + new Vector3(item.x + item.size / 2.0f, 0, item.z + item.size / 2.0f), item.physicTexIndex + "");
            leafCount++;
        }
     print("leafCount:" + leafCount);
        print("freeIndexCount:" + (clipRTAlbedoArray.volumeDepth- Node.physicEmptyIndexQueue.Count));
 


    }
#endif
   
    // Update is called once per frame
    void Update()
    {

       
      
        Shader.SetGlobalTexture("_VT_AlbedoTex", clipRTAlbedoArray);
        Shader.SetGlobalTexture("_VT_NormalTex", clipRTNormalArray);
        Shader.SetGlobalTexture("_VT_IndexTex", indexRT);

 
        Profiler.BeginSample("updateAllLeavesState");
       
        Node.updateAllLeavesState(new Vector2(Camera.main.transform.position.x- terrainOffset.x, Camera.main.transform.position.z- terrainOffset.z));
        Profiler.EndSample();

    }

    private void onLoadNodeData(Node item)
    {

        
        Profiler.BeginSample("onLoadNodeData");
     
        RenderTexture albedoRT, normalRT;

        virtualCapture.virtualCapture_MRT(new Vector2(item.x + item.size / 2.0f, item.z + item.size / 2.0f), item.size, out albedoRT, out normalRT);
        for (int i = 0; i < 4; i++)
        {
            Graphics.CopyTexture(albedoRT, 0, i, clipRTAlbedoArray, item.physicTexIndex, i);
            Graphics.CopyTexture(normalRT, 0, i, clipRTNormalArray, item.physicTexIndex, i);
        }

        indexGenerator.SetVector("value", new Vector4(item.physicTexIndex, item.x, item.z, item.size));

        //   只处理 mipmap0 , 也可以选择写入每一级mipmap 根据实际开销对比 选择创建mipmaps开销 还是选择 shader采样的缓存命中低
        int rectSize = item.size;
        indexGenerator.SetInt("offsetX", item.x);
        indexGenerator.SetInt("offsetZ", item.z);
        indexGenerator.Dispatch(0, rectSize, rectSize, 1);
        Profiler.EndSample();

    }

}
 