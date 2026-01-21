using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class OccluderShadowEdges : MonoBehaviour
{
    public string rootName = "ShadowEdges";
    public Material shadowMat; // 用 Hidden/ShadowVolumeExtrudeSimple
    public bool rebuildInEditor = true;

    struct Edge
    {
        public Vector3 aLocal;
        public Vector3 bLocal;
        public Vector3 nOutLocal;      // y=0
        public MeshRenderer renderer;  // 对应子物体
    }

    BoxCollider _col;
    Transform _root;
    Edge[] _edges;

#if UNITY_EDITOR
    Vector3 _lastPos; Quaternion _lastRot; Vector3 _lastScale;
    Vector3 _lastSize; Vector3 _lastCenter;
#endif

    void OnEnable()
    {
        _col = GetComponent<BoxCollider>();
        EnsureBuilt();
#if UNITY_EDITOR
        CacheState();
#endif
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!rebuildInEditor || Application.isPlaying) return;

        if (IsDirty())
        {
            EnsureBuilt(true);
            CacheState();
        }
    }

    void CacheState()
    {
        _lastPos = transform.position;
        _lastRot = transform.rotation;
        _lastScale = transform.localScale;
        _lastSize = _col.size;
        _lastCenter = _col.center;
    }

    bool IsDirty()
    {
        if (transform.position != _lastPos || transform.rotation != _lastRot || transform.localScale != _lastScale) return true;
        if (_col.size != _lastSize || _col.center != _lastCenter) return true;
        if (transform.Find(rootName) == null) return true;
        return false;
    }
#endif

    [ContextMenu("Rebuild Shadow Edges")]
    public void EnsureBuilt(bool forceRebuild = false)
    {
        if (_col == null) _col = GetComponent<BoxCollider>();

        _root = transform.Find(rootName);
        if (_root == null)
        {
            var go = new GameObject(rootName);
            _root = go.transform;
            _root.SetParent(transform, false);
        }

        if (forceRebuild)
        {
            for (int i = _root.childCount - 1; i >= 0; i--) DestroyImmediate(_root.GetChild(i).gameObject);
        }

        // 计算 CCW 底面四角（局部空间）
        var c = _col.center;
        var s = _col.size;
        float by = c.y - s.y * 0.5f;
        float ex = s.x * 0.5f;
        float ez = s.z * 0.5f;

        // CCW: (-x,-z)->(+x,-z)->(+x,+z)->(-x,+z)
        Vector3[] p =
        {
            new Vector3(c.x - ex, by, c.z - ez),
            new Vector3(c.x + ex, by, c.z - ez),
            new Vector3(c.x + ex, by, c.z + ez),
            new Vector3(c.x - ex, by, c.z + ez),
        };

        _edges = new Edge[4];

        for (int e = 0; e < 4; e++)
        {
            int i0 = e;
            int i1 = (e + 1) & 3;

            Vector3 A = p[i0];
            Vector3 B = p[i1];

            // 局部 XZ 外法线（基于 CCW）
            Vector2 edge = new Vector2(B.x - A.x, B.z - A.z);
            Vector2 n2 = new Vector2(edge.y, -edge.x);
            n2 = n2.sqrMagnitude > 1e-8f ? n2.normalized : new Vector2(1, 0);
            Vector3 nOutLocal = new Vector3(n2.x, 0, n2.y);
            // ✅ 保证 nOutLocal 指向“外侧”，避免 CCW/CW 或边方向导致 front/back 反了
            Vector3 mid = (A + B) * 0.5f;
            Vector3 toOutside = mid - _col.center;
            toOutside.y = 0;
            if (Vector3.Dot(nOutLocal, toOutside) < 0)
                nOutLocal = -nOutLocal;

            // 子物体
            var child = _root.Find($"Edge_{e}");
            if (child == null)
            {
                var go = new GameObject($"Edge_{e}");
                child = go.transform;
                child.SetParent(_root, false);

                go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = shadowMat;
            }

            var mf = child.GetComponent<MeshFilter>();
            var mr2 = child.GetComponent<MeshRenderer>();

            // 生成一条边的 quad（4 顶点）：A(base), B(base), B(extrude), A(extrude)
            var mesh = new Mesh();
            mesh.name = $"ShadowEdge_{e}";
            mesh.vertices = new[] { A, B, B, A };
            mesh.colors  = new[]
            {
                new Color(0,0,0,1), // base
                new Color(0,0,0,1),
                new Color(1,0,0,1), // extrude
                new Color(1,0,0,1),
            };
            mesh.triangles = new[] { 0,1,2, 0,2,3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            mf.sharedMesh = mesh;

            _edges[e] = new Edge
            {
                aLocal = A,
                bLocal = B,
                nOutLocal = nOutLocal,
                renderer = mr2
            };
        }
    }

    // 给管理器调用：根据玩家位置更新哪些边显示
    public void UpdateEdgeVisibility(Vector3 playerWorld, bool disableWhenInside = true)
    {
        if (_edges == null || _edges.Length != 4) return;

        // 可选：玩家在建筑 footprint 内部时，直接不投影（避免"屋内怪影"）
        if (disableWhenInside && IsInsideFootprint(playerWorld))
        {
            for (int i = 0; i < 4; i++) if (_edges[i].renderer) _edges[i].renderer.enabled = false;
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            ref var ed = ref _edges[i];

            // 使用边的中点作为参考
            Vector3 A = transform.TransformPoint(ed.aLocal);
            Vector3 B = transform.TransformPoint(ed.bLocal);
            Vector3 edgeMid = (A + B) * 0.5f;

            // 边的外法线（世界空间）
            Vector3 nW = transform.TransformDirection(ed.nOutLocal);
            nW.y = 0;
            if (nW.sqrMagnitude > 1e-8f) nW.Normalize();

            // 从边中点到玩家的向量
            Vector3 toPlayer = playerWorld - edgeMid;
            toPlayer.y = 0;

            // 如果外法线和"边到玩家"方向反向，说明玩家在内侧，渲染阴影
            bool shouldRender = Vector3.Dot(nW, toPlayer) < -0.01f;
            if (ed.renderer) ed.renderer.enabled = shouldRender;
        }
    }

    bool IsInsideFootprint(Vector3 playerWorld)
    {
        // 简单 AABB in local XZ（够用）
        Vector3 lp = transform.InverseTransformPoint(playerWorld);
        var c = _col.center;
        var s = _col.size;
        float ex = s.x * 0.5f;
        float ez = s.z * 0.5f;

        return (lp.x >= c.x - ex && lp.x <= c.x + ex &&
                lp.z >= c.z - ez && lp.z <= c.z + ez);
    }
}
