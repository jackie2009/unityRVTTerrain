 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;


public class VT_Terrain : MonoBehaviour
{

    public class Node
    {

        private enum NodeState
        { Branch, Leaf, Hidden };
       public const int patchSize = 8;// size小于patchSize的node 复用共享node 避免内存的巨大开销
        public static Queue<Node> currentAllLeaves;
        private static Queue<Node> nextAllLeaves;


        public static Queue<int> physicEmptyIndexQueue;
        const int sharedNodeCount = 256;
        public static Queue<Node> sharedNodeQueue;



        private static int getPhysicIndex() { return physicEmptyIndexQueue.Count == 0 ? -1 : physicEmptyIndexQueue.Dequeue(); }
        private static void resetPhysicIndex(Node node) { if (node.physicTexIndex > -1) physicEmptyIndexQueue.Enqueue(node.physicTexIndex); }
        private static Action<Node> onLoadData;
        private static int splitCount;
        private const int eventFrameSplitCountMax = 1;

        public int x;
        public int z;
        public int size;

        public Node[] children;
        public Node parent;
        private NodeState nodeState;// 树枝 树叶 或隐藏(树叶的子对象 关系保留但逻辑忽略)


        public int physicTexIndex = -1;
        private int dontMergeOnFrame;
        public Bounds aabb;

        public HashSet<Renderer> decals;
        internal static Func<Node, bool> isInFrustum;
        public static float tanHalfFov;
        public static int CurrentLoopFrame;

        internal void ceateFullChildren()
        {
            nodeState = NodeState.Hidden;
            
 
            if (size <= patchSize)
            {
                children = new Node[4];
                return;
            }

            if (children == null)
            {
                children = new Node[4];
                children[0] = new Node() { x = x, z = z, size = size / 2, parent = this };
                children[1] = new Node() { x = x + size / 2, z = z, size = size / 2, parent = this };
                children[2] = new Node() { x = x, z = z + size / 2, size = size / 2, parent = this };
                children[3] = new Node() { x = x + size / 2, z = z + size / 2, size = size / 2, parent = this };
                for (int i = 0; i < 4; i++)
                {
                    
                    children[i].ceateFullChildren();
                }
            }



        }


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
            root.ceateFullChildren();
            root.nodeState = NodeState.Leaf;
            currentAllLeaves.Enqueue(root);
            sharedNodeQueue = new Queue<Node>();
            for (int i = 0; i < sharedNodeCount; i++)
            {
                sharedNodeQueue.Enqueue(new Node() { children = new Node[4]}) ;
            }



            return root;
        }

        public static void updateAllLeavesState(Vector3 relativeCamPos) {
            splitCount = 0;
            nextAllLeaves.Clear();

            while (currentAllLeaves.Count > 0) {
                var node = currentAllLeaves.Dequeue();
                node.updateState(relativeCamPos);

            }


            var tempList = currentAllLeaves;
            currentAllLeaves = nextAllLeaves;
            nextAllLeaves = tempList;
            CurrentLoopFrame++;

        }

        private void updateState(Vector3 relativeCamPos)
        {
            if (nodeState == NodeState.Hidden) return;

            if (parent != null && nodeState == NodeState.Leaf && parent.dontMergeOnFrame != CurrentLoopFrame)
            {
                int parent_lodSize = parent.calculateLodSize(relativeCamPos);
                bool allBrothersAreLeaf = true;
                for (int i = 0; i < 4; i++)
                {
                    if (parent.children[i].nodeState != NodeState.Leaf) allBrothersAreLeaf = false;
                }
                if (parent.size <= parent_lodSize && allBrothersAreLeaf)
                {
                    parent.merge();
                    return;
                }


            }
            if (parent != null) {
                parent.dontMergeOnFrame = CurrentLoopFrame;
            }
            int lodSize = calculateLodSize(relativeCamPos);

            //当前尺寸刚好符合  lod需要的尺寸 自己保持为叶子 不动
            if (size == lodSize)

            {
                nextAllLeaves.Enqueue(this);
            }
            //当前尺寸 大于 lod需要的尺寸 需要细分出4个子对象
            else if (size > lodSize)
            {
                //不马上细分 而是合并完了再细分 这样 同时存在的叶子数就比较小 否则需要更多的对象数量
                if (splitCount++ < eventFrameSplitCountMax && physicEmptyIndexQueue.Count >= 3&& sharedNodeQueue.Count>=4 )
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
            //  print("split:" + physicTexIndex);
            resetPhysicIndex(this);
           
            nodeState = NodeState.Branch;
            if (size <= patchSize) {
              
                for (int i = 0; i < 4; i++)
                {
                    createSharedChildNode(i);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                children[i].nodeState = NodeState.Leaf;
                children[i].physicTexIndex = getPhysicIndex();
                nextAllLeaves.Enqueue(children[i]);
                onLoadData(children[i]);

            }



        }

        private void  createSharedChildNode(int i)
        {
            var cnode = sharedNodeQueue.Dequeue();
            cnode.x = x + (i % 2) * size/2;
            cnode.z = z + (i / 2) * size/2;
            cnode.size = size / 2;
            cnode.parent = this;
            //绘制时判断贴花aabb是否 在该格子内 
            cnode.decals = decals;



            cnode.aabb =  new Bounds(new Vector3(cnode.x + cnode.size / 2.0f, aabb.center.y, cnode.z + cnode.size / 2.0f), new Vector3(cnode.size, aabb.size.y, cnode.size));
            children[i] = cnode;
            
        }

        // 合并node  队列放入parent 不放自己，并跳过后面3个同级node计算 也就不会放入队列
        private void merge()
        {




            nextAllLeaves.Enqueue(this);

            //var tempParent = parent;
            for (int i = 0; i < 4; i++)
            {
                resetPhysicIndex(children[i]);
                if(size<= patchSize)
                sharedNodeQueue.Enqueue( children[i]);
        
                children[i].nodeState = NodeState.Hidden;



            }
            physicTexIndex = getPhysicIndex();
            // print("merge:" + physicTexIndex);
            onLoadData(this);
            // children = null;
            nodeState = NodeState.Leaf;



        }
        private int calculateLodSize(Vector3 relativeCamPos)
        {
            var closedPt = aabb.ClosestPoint(relativeCamPos);
            var dis = Vector3.Distance(relativeCamPos, closedPt);
            // 用最近点计算lod 或 用注释里细分一次后 最近那块的近似中心点计算        
            // if(dis>0)dis+= Vector3.Distance(aabb.center, closedPt)/2; 

            int lod = Mathf.Max(0, (int)(Mathf.Log(tanHalfFov * dis, 2) - 0.5f));


            if (isInFrustum(this) == false)
            {
                //0.577f  为 60fov的 tan 半角值,这样做是因为 现有项目不开镜的时候一般都是60 这样转身的时候就不用变化太多级lod和多次加载了,可根据实际项目决定 视锥外的这个lod
                int lodMax = Mathf.Max(0, (int)(Mathf.Log(dis * 0.577f, 2))) + 1;
                lod = Mathf.Max(lod, lodMax);

            }

            return 1 << lod;
        }



        //因为远处不绘制贴花 否则一大块要画一堆性能不好 所以 用 nodeSizeLimit做限制
        internal void insertDecal(int vx, int vz, Renderer decal, int nodeSizeLimit)
        {

            if (size == patchSize)
            {
                if (decals == null) {
                    decals = new HashSet<Renderer>();
                }
                decals.Add(decal);
                return;
            }
            if (size <= nodeSizeLimit) {
                if (decals == null)
                {
                    decals = new HashSet<Renderer>();
                }
                decals.Add(decal);
            }

            int offset = 0;
            if (vx >= x + size / 2) offset++;
            if (vz >= z + size / 2) offset += 2;
            children[offset].insertDecal(vx, vz, decal, nodeSizeLimit);
        }


        internal void insertHeights(int vx, int vz, Vector2 heights)
        {
            // return;
            var points = new Vector3[]{
                 new Vector3(vx, 0, vz) + Vector3.up * heights.x,
                 new Vector3(vx +1, 0, vz + 1)  + Vector3.up * heights.y
            };
            if (aabb.size.sqrMagnitude == 0)
            {
                aabb = new Bounds(points[0], Vector3.one * 0.1f);
            }
            else {
                aabb.Encapsulate(points[0]);
            }
            aabb.Encapsulate(points[1]);

            if (size == patchSize)
            {

                return;
            }

            int offset = 0;
            if (vx >= x + size / 2) offset++;
            if (vz >= z + size / 2) offset += 2;
            children[offset].insertHeights(vx, vz, heights);
        }






    }
    Node root;
#if !RVT_COMPRESS_ON
    public RenderTexture clipRTAlbedoArray;
    public RenderTexture clipRTNormalArray;
#else
    public Texture2DArray clipRTAlbedoArray;
    public Texture2DArray clipRTNormalArray;
#endif

    public int rootSize = 1024;
    private const int PhysicalTexCount = 385;
    public Vector3 terrainOffset;
    public ComputeShader indexGenerator;
    public RenderTexture indexRT;

    private VirtualCapture virtualCapture;
    public Transform decalsRoot;


    private Thread threadTerrainLod;
    private Vector3 Camera_main_position;
    private Vector3 Camera_main_forward;
    private float Camera_main_aspect;
    private float Camera_main_fov;
    private Queue<Node> waitingLoadQueue;
    private float rotFrustun;

    void Start()
    {

        waitingLoadQueue = new Queue<Node>();
        virtualCapture = GetComponent<VirtualCapture>();
        indexRT = new RenderTexture(rootSize, rootSize, 0, RenderTextureFormat.RG16, RenderTextureReadWrite.Linear);
        indexRT.useMipMap = false;
        indexRT.autoGenerateMips = false;
        indexRT.enableRandomWrite = true;
        indexRT.name = "indexRT";
        indexRT.filterMode = FilterMode.Point;
        indexRT.Create();
        indexGenerator.SetTexture(0, "Result", indexRT);

        int mipmapCount = (int)Mathf.Log(indexRT.width, 2);
        print(mipmapCount);




#if !RVT_COMPRESS_ON
        clipRTAlbedoArray = new RenderTexture(VirtualCapture.virtualTextArraySize, VirtualCapture.virtualTextArraySize, 0, RenderTextureFormat.ARGB32);
        clipRTAlbedoArray.volumeDepth = PhysicalTexCount;
        clipRTAlbedoArray.wrapMode = TextureWrapMode.Clamp;
        clipRTAlbedoArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        clipRTAlbedoArray.useMipMap = true;
        clipRTAlbedoArray.autoGenerateMips = false;
        clipRTAlbedoArray.Create();


        clipRTNormalArray = new RenderTexture(VirtualCapture.virtualTextArraySize, VirtualCapture.virtualTextArraySize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        clipRTNormalArray.volumeDepth = PhysicalTexCount;
        clipRTNormalArray.wrapMode = TextureWrapMode.Clamp;
        clipRTNormalArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        clipRTNormalArray.useMipMap = true;
        clipRTNormalArray.autoGenerateMips = false;
        clipRTNormalArray.Create();
#else
        clipRTAlbedoArray = new  Texture2DArray(VirtualCapture.virtualTextArraySize, VirtualCapture.virtualTextArraySize, PhysicalTexCount, TextureFormat.DXT5,true,false);
        clipRTAlbedoArray.wrapMode = TextureWrapMode.Clamp;
        clipRTAlbedoArray.Apply(false, true);
        clipRTNormalArray = new Texture2DArray(VirtualCapture.virtualTextArraySize, VirtualCapture.virtualTextArraySize, PhysicalTexCount, TextureFormat.BC5, true, true);
        clipRTNormalArray.wrapMode = TextureWrapMode.Clamp;
        clipRTNormalArray.Apply(false, true);
#endif







        Node.isInFrustum = isInFrustum;
     long usedM1=Profiler.GetMonoUsedSizeLong() / 1024 / 1024;
        root = Node.createRoot(rootSize, PhysicalTexCount, onLoadNodeData);
        Shader.SetGlobalInt("VT_RootSize", rootSize);
        //  root =   Node.createRoot(16,100, onLoadNodeData);
        if (decalsRoot != null)
        {
            foreach (var item in decalsRoot.GetComponentsInChildren<Renderer>())
            {
                int xMin = (int)(item.bounds.min.x-terrainOffset.x)/Node.patchSize;
                int xMax = Mathf.CeilToInt(item.bounds.max.x - terrainOffset.x) / Node.patchSize;
                int zMin = (int)(item.bounds.min.z- terrainOffset.z) / Node.patchSize;
                int zMax = Mathf.CeilToInt(item.bounds.max.z- terrainOffset.z) / Node.patchSize;
                for (int x = xMin; x <= xMax; x++)

                {
                    for (int z = zMin; z <= zMax; z++)
                    {

                        root.insertDecal(x* Node.patchSize, z* Node.patchSize, item, 256);
                    }

                }
            }
            decalsRoot.gameObject.SetActive(false);
        }
        long usedM2 = Profiler.GetMonoUsedSizeLong() / 1024 / 1024;
        print(usedM1);
        print(usedM2);
        var terrain = FindObjectOfType<Terrain>();


       
        //如果要最高性能 先插入到 最小格子 1mx1m 然后向上合计出parent的数据性能更高些 但是初始化时性能不敏感 这样写可读性更好
        for (int x = 0; x < rootSize; x++)
        {
            for (int z = 0; z < rootSize; z++)
            {
                Vector4 cornerHeights = new Vector4(
                terrain.terrainData.GetHeight(x / 2, z / 2),
                terrain.terrainData.GetHeight(x / 2 + 1, z / 2),
                terrain.terrainData.GetHeight(x / 2, z / 2 + 1),
                terrain.terrainData.GetHeight(x / 2 + 1, z / 2 + 1));
                Vector2 heightRange = new Vector2(Mathf.Min(cornerHeights.x, cornerHeights.y, cornerHeights.z, cornerHeights.w),
                    Mathf.Max(cornerHeights.x, cornerHeights.y, cornerHeights.z, cornerHeights.w));
                root.insertHeights(x, z, heightRange);
            }
        }
 
        threadTerrainLod = new Thread(threadTerrainLodLoop);
        threadTerrainLod.Start();
    }
    void threadTerrainLodLoop() {

        while (true)
        {
            Thread.Sleep(10);
            float halfH = Mathf.Tan(Camera_main_fov / 2 * Mathf.Deg2Rad);
            float halfW = halfH * Camera_main_aspect;
            float halfDis = Mathf.Sqrt(halfH * halfH + halfW * halfW);
            rotFrustun = Mathf.Atan(halfDis);
#if UNITY_EDITOR
            lock (Node.currentAllLeaves)
            {
#endif
                Node.updateAllLeavesState(Camera_main_position - terrainOffset);
#if UNITY_EDITOR
            }
#endif

        }

    }
    bool isInFrustum(Node item) {

        //这部分 常规的 视锥剔除 性能是严重不足的 所以用视锥的外接圆锥 夹角做更保守剔除
        Vector3 dpos = item.aabb.center - Camera_main_position + terrainOffset;
        float dis = dpos.magnitude;

        float rAll = Mathf.Acos(Vector3.Dot(dpos.normalized, Camera_main_forward));
        float rNode = Mathf.Atan(item.aabb.extents.magnitude / dis);

        bool inF = rAll < rNode + rotFrustun;


        return inF;

    }



    void OnDestroy()
    {
        if (threadTerrainLod != null) threadTerrainLod.Abort();
        if (indexRT != null) indexRT.Release();
#if !RVT_COMPRESS_ON
        if (clipRTAlbedoArray != null) clipRTAlbedoArray.Release();
        if (clipRTNormalArray != null) clipRTNormalArray.Release();
#else
        if (clipRTAlbedoArray != null) Destroy( clipRTAlbedoArray);
        if (clipRTNormalArray != null) Destroy(clipRTNormalArray);
#endif
    }

#if UNITY_EDITOR
    
    void OnDrawGizmos()
    {
         
     
        if (root == null) return;

        Gizmos.color = Color.green;


        lock (Node.currentAllLeaves) { 
           
 
            foreach (var item in Node.currentAllLeaves)
            {

                if (item == null) continue;

                Gizmos.color = isInFrustum(item) ? Color.green : Color.red;
               
                Gizmos.DrawWireCube(terrainOffset + item.aabb.center, item.aabb.size);
                //   UnityEditor.Handles.Label(terrainOffset + new Vector3(item.x + item.size / 2.0f, 0, item.z + item.size / 2.0f), item.physicTexIndex + "");
           
            }
          
          

        }
        
        
    }
#endif
   
    // Update is called once per frame
    void Update()
    {
        Camera_main_position = Camera.main.transform.position;
        Camera_main_forward = Camera.main.transform.forward;
        Camera_main_aspect = Camera.main.aspect;
        Camera_main_fov = Camera.main.fieldOfView;
        Node.tanHalfFov = Mathf.Tan(Camera.main.fieldOfView / 2 * Mathf.Deg2Rad);




    Shader.SetGlobalTexture("_VT_AlbedoTex", clipRTAlbedoArray);
        Shader.SetGlobalTexture("_VT_NormalTex", clipRTNormalArray);
        Shader.SetGlobalTexture("_VT_IndexTex", indexRT);


        loadQueue();

    }

    private void onLoadNodeData(Node item)
    {
        lock (waitingLoadQueue)
        {
            waitingLoadQueue.Enqueue(item);
        }
        // print("loadata:" + item.physicTexIndex);


 

    }
    private void loadQueue() {
        lock (waitingLoadQueue)
        {
            while (waitingLoadQueue.Count > 0)
            {
                var item = waitingLoadQueue.Dequeue();

                

                virtualCapture.virtualCapture_MRT(item, clipRTAlbedoArray, clipRTNormalArray,terrainOffset);
             
                int level =Mathf.RoundToInt( Mathf.Log(item.size, 2));
                indexGenerator.SetInt("value",   item.physicTexIndex*16 +level );

                //   只处理 mipmap0 , 也可以选择写入每一级mipmap 根据实际开销对比 选择创建mipmaps开销 还是选择 shader采样的缓存命中低
                int rectSize = item.size;
                indexGenerator.SetInt("offsetX", item.x);
                indexGenerator.SetInt("offsetZ", item.z);
                indexGenerator.Dispatch(0, rectSize, rectSize, 1);
            }
        }
    }

}
 