#define __ENABLE_COMPLETE_CONTAINS_BRANCH_HANDLE__  // 启用完全包含 枝干 优化的处理
#define __ENABLE_QT_PROFILER__                      // 启用 Profiler

using System;
using System.Collections.Generic;
using UnityEngine;

#if __ENABLE_QT_PROFILER__
using UnityEngine.Profiling;
#endif

// jave.lin : 下面的 ListPool<T> 类可以单独放到另一个通用的工具类下管理
// 便于外部所有地方可以使用，但是如果这么做的话，最好声明为 static 静态类

/// <summary>
/// date    : 2020/11/11
/// author  : jave.lin
/// 外部大量的 new List<T> 也是导致大量 GC.Collect 频繁触发的原因
/// 可以使用 ListPool<T>.FromPool, ToPool 来专门替代外部的 new List<T> 的临时变量
/// 可大大降低：GC.Collect 的触发周期
/// List<T> 的对象池管理，专用于 C# 层的处理，因为 lua 做不了 C# 编译时决定的泛型
/// </summary>
/// <typeparam name="T">元素类型</typeparam>
public class ListPool<T> : IDisposable
{
    private Stack<List<T>> _list_pool = new Stack<List<T>>();
    public List<T> FromPool()
    {
        return _list_pool.Count > 0 ? _list_pool.Pop() : new List<T>();
    }

    public void ToPool(List<T> list)
    {
        list.Clear();
        _list_pool.Push(list);
    }

    public void Clear()
    {
        _list_pool.Clear();
    }

    public void Dispose()
    {
        if (_list_pool != null)
        {
            _list_pool.Clear();
            _list_pool = null;
        }
    }
}

[Serializable]
/// <summary>
/// QuadTree 使用的 AABB（后续完善其实可以尝试支持 OOB）
/// date    : 2021/02/18
/// author  : jave.lin
/// </summary>
public struct QTAABB : IEquatable<QTAABB>
{
    public static readonly QTAABB Zero = new QTAABB();

    /// <summary>
    /// 获取 cam frustum 的 aabb 的垂直方向的 分层级别，默认是 3 个级别
    /// </summary>
    public const int DEFAULT_GET_CAM_FRUSTUM_TO_AABB_LEVEL = 3;
    /// <summary>
    /// 获取 cam frustum 的 aabb 的水平方向的 padding，默认是 unity 的 0 个 units 的大小
    /// </summary>
    public const float DEFAULT_GET_CAM_FRUSTUM_AABB_H_PADDING = 0;

    public float x;
    public float y;
    public float w;
    public float h;
    
    public float left       { get => x; set => x = value; }
    public float top        { get => y; set => y = value; }
    public float right      { get => x + w; set => w = value - x; }
    public float bottom     { get => y + h; set => h = value - y; }

    public float centerX    { get => x + w * 0.5f; set => x = value - (w * 0.5f); }
    public float centerY    { get => y + h * 0.5f; set=> y = value - (h * 0.5f); }
    public Vector2 center   { get => new Vector2(centerX, centerY); set { centerX = value.x; centerY = value.y; } }

    public float extentX    { get => w * 0.5f; set => w = value * 2; }
    public float extentY    { get => y * 0.5f; set => h = value * 2; }
    public Vector2 extent   { get => new Vector2(extentX, extentY); set { extentX = value.x; extentY = value.y; } }

    public Vector2 min { get => new Vector2(left, top); set { left = value.x; top = value.y; } }
    public Vector2 max { get => new Vector2(right, bottom); set { right = value.x; bottom = value.y; } }

    public Vector2 top_left { get => new Vector2(left, top); }
    public Vector2 top_right { get => new Vector2(right, top); }
    public Vector2 bottom_left { get => new Vector2(left, bottom); }
    public Vector2 bottom_right { get => new Vector2(right, bottom); }

    public bool IsZero()
    {
        return left == right || top == bottom;
    }
    public void Set(float x, float y, float w, float h)
    {
        this.x = x;
        this.y = y;
        this.w = w;
        this.h = h;
    }
    /// <summary>
    /// 当前 AABB 与 other 的 AABB 是否有交集，并返回交集的 AABB
    /// </summary>
    /// <param name="other">其他的 AABB</param>
    /// <param name="outAABB">返回交集的 AABB</param>
    /// <returns>如果当前 AABB 与 other 的 AABB 是否有交集，则返回 true</returns>
    public bool IsIntersect(ref QTAABB other, out QTAABB outAABB)
    {
        outAABB         = new QTAABB();
        outAABB.x       = Mathf.Max(left, other.left);
        outAABB.right   = Mathf.Min(right, other.right);
        outAABB.y       = Mathf.Max(top, other.top);
        outAABB.bottom  = Mathf.Min(bottom, other.bottom);
        return !outAABB.IsZero();
    }
    /// <summary>
    /// 当前 AABB 与 other 的 AABB 是否有交集
    /// </summary>
    /// <param name="other">其他的 AABB</param>
    /// <returns>如果当前 AABB 与 other 的 AABB 是否有交集，则返回 true</returns>
    public bool IsIntersect(ref QTAABB other)
    {
        return x < other.right && y < other.bottom && right > other.x && bottom > other.y;
    }
    public bool IsIntersect(QTAABB other)
    {
        return IsIntersect(ref other);
    }
    /// <summary>
    /// 是否完整包含另一个 AABB（做优化用，一般如果整个 AABB 都被另一个 AABB 包含就不用精确检测了）
    /// </summary>
    /// <param name="other">另一个 AABB</param>
    /// <returns>如果完整包含另一个 AABB，则返回 true</returns>
    public bool Contains(ref QTAABB other)
    {
        return other.x >= x && other.y >= y && other.right <= right && other.bottom <= bottom;
    }
    public bool Contains(QTAABB other)
    {
        return Contains(ref other);
    }
    /// <summary>
    /// 是否包含一个 2D 点
    /// </summary>
    /// <param name="x">2D 点 x</param>
    /// <param name="y">2D 点 x</param>
    /// <returns>如果包含 2D 点，则返回 true</returns>
    public bool Contains(float x, float y)
    {
        return x >= left && x < right && y >= top && y < bottom;
    }
    /// <summary>
    /// 是否包含一个 2D 点
    /// </summary>
    /// <param name="pos">2D 点</param>
    /// <returns>如果包含 2D 点，则返回 true</returns>
    public bool Contains(Vector2 pos)
    {
        return Contains(ref pos);
    }
    public bool Contains(ref Vector2 pos)
    {
        return Contains(pos.x, pos.y);
    }
    public void Union(QTAABB aabb)
    {
        Union(ref aabb);
    }
    /// <summary>
    /// 并集一个 AABB
    /// </summary>
    /// <param name="aabb">需要与之并集的 AABB</param>
    public void Union(ref QTAABB aabb)
    {
        Union(aabb.min);
        Union(aabb.max);
    }
    /// <summary>
    /// 并集一个 点
    /// </summary>
    /// <param name="pos"></param>
    public void Union(Vector2 pos)
    {
        Union(ref pos);
    }
    public void Union(ref Vector2 pos)
    {
        Union(pos.x, pos.y);
    }
    public void Union(Vector3 pos)
    {
        Union(ref pos);
    }
    public void Union(ref Vector3 pos)
    {
        Union(pos.x, pos.z);
    }

    public void Union(float _x, float _z)
    {
        var src_x = x;
        var src_y = y;
        var src_r = right;
        var src_b = bottom;

        x = Mathf.Min(src_x,        _x);
        x = Mathf.Min(x,            src_r);

        y = Mathf.Min(src_y,        _z);
        y = Mathf.Min(y,            src_b);

        right = Mathf.Max(src_x,    _x);
        right = Mathf.Max(right,    src_r);

        bottom = Mathf.Max(src_y,   _z);
        bottom = Mathf.Max(bottom,  src_b);
    }

    /// <summary>
    /// 与多个 aabbs 是否有任意的并集
    /// </summary>
    /// <param name="aabbs">多个 aabbs</param>
    /// <returns>如果有任意的并集，返回 true</returns>
    public bool AnyIntersect(List<QTAABB> aabbs)
    {
        foreach (var aabb in aabbs)
        {
            if (IsIntersect(aabb))
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// 是否被 多个 aabbs 中的其中一个全包含
    /// </summary>
    /// <param name="aabbs">多个 aabbs</param>
    /// <returns>如果被其中一个全包含，返回 true</returns>
    public bool AnyContainsBy(List<QTAABB> aabbs)
    {
        foreach (var aabb in aabbs)
        {
            if (aabb.Contains(this))
            {
                return true;
            }
        }
        return false;
    }
    public bool Equals(QTAABB other)
    {
        return x == other.x && y == other.y && w == other.w && h == other.h;
    }
    /// <summary>
    /// jave.lin : 让x,y为最小值，right,bottom 为最大值
    /// 因为部分 w, h 可以为负数，那么再部分计算就不太方便，所以可以统一转换成 x,y < w,h 的格式
    /// </summary>
    public void Reorder()
    {
        var src_x = x;
        var src_y = y;
        var src_r = right;
        var src_b = bottom;

        x = Mathf.Min(src_x, src_r);
        y = Mathf.Min(src_y, src_b);

        x = Mathf.Max(src_x, src_r);
        y = Mathf.Max(src_y, src_b);
    }
    /// <summary>
    /// 获取 Camera 分层 level 的多个 aabb，如果 Camera 是一个正交投影，那么会无视 level 数值，直接返回一个 aabb
    /// </summary>
    /// <param name="cam">要获取多个 aabb 的 Camera</param>
    /// <param name="ret">结果</param>
    /// <param name="level">将 frustum 分解的层级数量</param>
    /// <param name="h_padding">添加水平边界间隔</param>
    public static void GetCameraAABBs(Camera cam, List<QTAABB> ret, 
        int level = DEFAULT_GET_CAM_FRUSTUM_TO_AABB_LEVEL, float h_padding = DEFAULT_GET_CAM_FRUSTUM_AABB_H_PADDING)
    {
        ret.Clear();
        if (cam.orthographic)
        {
            var aabb = new QTAABB();
            GetOrthorCameraAABB(cam, ref aabb, h_padding);
            ret.Add(aabb);
        }
        else
        {
            GetFrustumCameraAABBs(cam, ret, level, h_padding);
        }
    }
    public static void GetOrthorCameraAABB(Camera cam, ref QTAABB aabb, float h_padding = DEFAULT_GET_CAM_FRUSTUM_AABB_H_PADDING)
    {
        System.Diagnostics.Debug.Assert(cam.orthographic == true);
        var far             = cam.farClipPlane;
        var near            = cam.nearClipPlane;
        var delta_fn        = far - near;
        var half_height     = cam.orthographicSize;
        var half_with       = cam.aspect * half_height;
        var forward         = cam.transform.forward;
        var right           = cam.transform.right;
        var up              = cam.transform.up;
        var start_pos       = cam.transform.position + forward * near;
        var top_left        = start_pos + forward * delta_fn + (-right * half_with) + (up * half_height);
        var top_right       = top_left + (right * (2 * half_with));
        var bottom_right    = top_right + (-up * (2 * half_height));
        var bottom_left     = bottom_right + (-right * (2 * half_with));

        var h_padding_vec   = right * h_padding;

        top_left            -= h_padding_vec;
        top_right           += h_padding_vec;
        bottom_right        += h_padding_vec;
        bottom_left         -= h_padding_vec;

        // 重置
        aabb.w = aabb.h = 0;
        aabb.x = start_pos.x;
        aabb.y = start_pos.z;

        // 并集其他点
        aabb.Union(ref top_left);
        aabb.Union(ref top_right);
        aabb.Union(ref bottom_right);
        aabb.Union(ref bottom_left);
    }
    public static void GetFrustumCameraAABBs(Camera cam, List<QTAABB> aabbs, int level = DEFAULT_GET_CAM_FRUSTUM_TO_AABB_LEVEL, float h_padding = DEFAULT_GET_CAM_FRUSTUM_AABB_H_PADDING)
    {
        // 计算椎体分段包围盒
        System.Diagnostics.Debug.Assert(cam.orthographic == false);
        System.Diagnostics.Debug.Assert(level > 0);
        // 相机的 frustum 如果构建，可以参考我以前的一篇文章：https://blog.csdn.net/linjf520/article/details/104761121#OnRenderImage_98
        var far                     = cam.farClipPlane;
        var near                    = cam.nearClipPlane;
        var tan                     = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var far_plane_half_height   = tan * far;
        var far_plane_half_with     = cam.aspect * far_plane_half_height;
        var near_plane_half_height  = tan * near;
        var near_plane_half_with    = cam.aspect * near_plane_half_height;

        var forward                 = cam.transform.forward;
        var right                   = cam.transform.right;
        var up                      = cam.transform.up;

        var far_top_left            = cam.transform.position + forward * far + (-right * far_plane_half_with) + (up * far_plane_half_height);
        var far_top_right           = far_top_left + (right * (2 * far_plane_half_with));
        var far_bottom_right        = far_top_right + (-up * (2 * far_plane_half_height));
        var far_bottom_left         = far_bottom_right + (-right * (2 * far_plane_half_with));

        var near_top_left           = cam.transform.position + forward * near + (-right * near_plane_half_with) + (up * near_plane_half_height);
        var near_top_right          = near_top_left + (right * (2 * near_plane_half_with));
        var near_bottom_right       = near_top_right + (-up * (2 * near_plane_half_height));
        var near_bottom_left        = near_bottom_right + (-right * (2 * near_plane_half_with));

        var n2f_top_left_vec        = far_top_left - near_top_left;
        var n2f_top_right_vec       = far_top_right - near_top_right;
        var n2f_bottom_right_vec    = far_bottom_right - near_bottom_right;
        var n2f_bottom_left_vec     = far_bottom_left - near_bottom_left;

        var h_padding_vec           = right * h_padding;

        for (int i = 0; i < level; i++)
        {
            var rate_start          = (float)i / level;
            var rate_end            = (float)(i + 1) / level;

            // near plane 四个角点
            var top_left_start      = near_top_left     + n2f_top_left_vec      * rate_start;
            var top_right_start     = near_top_right    + n2f_top_right_vec     * rate_start;
            var bottom_right_start  = near_bottom_right + n2f_bottom_right_vec  * rate_start;
            var bottom_left_start   = near_bottom_left  + n2f_bottom_left_vec   * rate_start;

            // 水平 padding
            top_left_start          -= h_padding_vec;
            top_right_start         += h_padding_vec;
            bottom_right_start      += h_padding_vec;
            bottom_left_start       -= h_padding_vec;

            // far plane 四个角点
            var top_left_end        = near_top_left     + n2f_top_left_vec      * rate_end;
            var top_right_end       = near_top_right    + n2f_top_right_vec     * rate_end;
            var bottom_right_end    = near_bottom_right + n2f_bottom_right_vec  * rate_end;
            var bottom_left_end     = near_bottom_left  + n2f_bottom_left_vec   * rate_end;

            // 水平 padding
            top_left_end            -= h_padding_vec;
            top_right_end           += h_padding_vec;
            bottom_right_end        += h_padding_vec;
            bottom_left_end         -= h_padding_vec;

            var aabb = new QTAABB();
            aabb.Set(top_left_start.x, top_left_start.z, 0, 0);

            // 并集其他点
            aabb.Union(ref top_left_start);
            aabb.Union(ref top_right_start);
            aabb.Union(ref bottom_right_start);
            aabb.Union(ref bottom_left_start);
            aabb.Union(ref top_left_end);
            aabb.Union(ref top_right_end);
            aabb.Union(ref bottom_right_end);
            aabb.Union(ref bottom_left_end);
            aabbs.Add(aabb);
        }
    }
    /// <summary>
    /// Unity 的 Bounds 隐式转为 我们自己定义的 QTAABB 便于外部书写
    /// </summary>
    /// <param name="v">Unity 的 Bounds</param>
    public static implicit operator QTAABB(Bounds v)
    {
        var b_min = v.min;
        var b_max = v.max;
        return new QTAABB 
        {
            min = new Vector2(b_min.x, b_min.z),
            max = new Vector2(b_max.x, b_max.z),
        };
    }
    /// <summary>
    /// 便于 VS 中断点调式的简要显示 title 信息
    /// </summary>
    /// <returns>返回：便于 VS 中断点调式的简要显示 title 信息</returns>
    public override string ToString()
    {
        return base.ToString() + $", x:{x}, y:{y}, w:{w}, h:{h}, right:{right}, bottom:{bottom}";
    }
}

/// <summary>
/// QuadTree 类
/// （
///     目前封装的写法适合静态构建四叉树的写法，
///     如果需要动态调整，可能还需要四叉树刷新机制，或是重构树的机制，
///     简单粗暴的重构树，也可以先 Clear 再逐个 Insert，但是会导致 Insert 消耗增加
/// ）
/// 参考另一个：https://github.com/futurechris/QuadTree 该开源库写得还是不错的，可读性也高
/// date    : 2021/02/18
/// author  : jave.lin
/// </summary>
/// <typeparam name="T">四叉树中需要被包裹的实体对象类型</typeparam>
public class QuadTree<T> : IDisposable
{
    /// <summary>
    /// 四叉树的 Leaf 叶子类
    /// </summary>
    public class Leaf
    {
        public Branch branch;                                       // 枝干
        public T value;                                             // 包裹的数据对象
        public QTAABB aabb;                                         // 该叶子的 AABB
    }
    /// <summary>
    /// 四叉树的 Branch 枝干类
    /// </summary>
    public class Branch
    {
        public QuadTree<T> belongTree;                              // 所属的 四叉树
        public Branch parent;                                       // 父枝干
        public QTAABB aabb;                                         // 该枝干
        public QTAABB[] aabbs = new QTAABB[4];                      // 分支的四象限的 AABB       （先创建对象：空间换时间，省去后续的大量 != null 判断）
        public Branch[] branches = new Branch[4];                   // 分支的枝干                （先创建对象：空间换时间，省去后续的大量 != null 判断）
        public List<Leaf> leaves = new List<Leaf>();                // 拥有的叶子                （先创建对象：空间换时间，省去后续的大量 != null 判断）
        public List<Leaf> crossBranchesLeaves = new List<Leaf>();   // 横跨多个 枝干 的叶子      （先创建对象：空间换时间，省去后续的大量 != null 判断）
        public int depth;                                           // 该枝干的深度
        public bool hasSplit;                                       // 有无再次分过枝干
        public bool hasRecycle;                                     // 是否被回收了
    }

    public const int MAX_LIMIT_LEVEL = 32;                          // 四叉树最大可以设置的深度
    
    public const int DEFAULT_MAX_LEVEL = 10;
    public const int DEFAULT_MAX_LEAF_PER_BRANCH = 50;

    public float cullingDistance = float.NaN;

    // 叶子表
    private Dictionary<T, Leaf> leavesDict = new Dictionary<T, Leaf>();
    // 跟枝干
    public Branch root;
    // 最大层级
    private int maxLevel;
    // 叶子到达该数量时就会再次划分出枝干
    private int maxLeafPerBranch;

    private bool[] insert_helper = new bool[4];

    // 枝干、叶子的池子（减少 GC 频繁触发的问题），每个池子在自身的类对象下管理即可，看情况而是否该成 static 的
    private Stack<Branch> branchPool = new Stack<Branch>();
    private Stack<Leaf> leafPool = new Stack<Leaf>();
    private ListPool<T> listDataPool = new ListPool<T>();
    private List<QTAABB> aabbs_helper = new List<QTAABB>();

    /// <summary>
    /// 构建四叉树
    /// </summary>
    /// <param name="aabb">整个QuadTree的最大aabb</param>
    /// <param name="maxLevel">四叉树的最大深度</param>
    /// <param name="maxLeafPerBranch">四叉树单个叶子的最大数量</param>
    public QuadTree(QTAABB aabb, 
        int maxLevel = DEFAULT_MAX_LEVEL, int maxLeafPerBranch = DEFAULT_MAX_LEAF_PER_BRANCH)
        : this(aabb.x, aabb.y, aabb.w, aabb.h, maxLevel, maxLeafPerBranch)
    {

    }
    /// <summary>
    /// 构建四叉树
    /// </summary>
    /// <param name="x">整个QuadTree的最大aabb的x</param>
    /// <param name="y">整个QuadTree的最大aabb的y</param>
    /// <param name="w">整个QuadTree的最大aabb的w</param>
    /// <param name="h">整个QuadTree的最大aabb的h</param>
    /// <param name="maxLevel">四叉树的最大深度</param>
    /// <param name="maxLeafPerBranch">四叉树单个叶子的最大数量</param>
    public QuadTree(float x, float y, float w, float h, 
        int maxLevel = DEFAULT_MAX_LEVEL, int maxLeafPerBranch = DEFAULT_MAX_LEAF_PER_BRANCH)
    {
        _Reset(x, y, w, h, maxLevel, maxLeafPerBranch);
    }
    /// <summary>
    /// 销毁
    /// </summary>
    public void Dispose()
    {
        root = null;
        if (leavesDict != null)
        {
            leavesDict.Clear();
            leavesDict = null;
        }
        if (branchPool != null)
        {
            branchPool.Clear();
            branchPool = null;
        }
        if (leafPool != null)
        {
            leafPool.Clear();
            leafPool = null;
        }
        if (listDataPool != null)
        {
            listDataPool.Clear();
            listDataPool = null;
        }
        if (aabbs_helper != null)
        {
            aabbs_helper.Clear();
            aabbs_helper = null;
        }
    }
    /// <summary>
    /// 清理内部枝干结构到最初的状态
    /// </summary>
    public void Clear()
    {
        if (root != null)
        {
            var src_aabb = root.aabb;
            _RecycleBranchToPool(root);
            leavesDict.Clear();

            root = _GetBranchFromPool(null, 0, ref src_aabb);
        }
    }
    /// <summary>
    /// 插入（目前使用与 静态 QT 树的逻辑写法，所以插入处理成本比较大，但是在 Select 会大大提升性能）
    /// 如果要写动态 QT，那么 Insert 需要重新写一套逻辑，要尽可能的简单分割处理，那么时候可以另起一个测试项目来编写处理
    /// 现在的静态版本 QT 的插入时处理了跨 多枝干 的存放到父级节点的优化处理的，如果动态版本的话就不需要了
    /// 但是需要外部对筛选结果的去重
    /// </summary>
    /// <param name="value">插入的对象</param>
    /// <param name="aabb">插入对象对应的aabb</param>
    /// <returns>插入成功返回 true</returns>
    public bool Insert(T value, QTAABB aabb)
    {
        return Insert(value, ref aabb);
    }
    /// <summary>
    /// 插入（目前使用与 静态 QT 树的逻辑写法，所以插入处理成本比较大，但是在 Select 会大大提升性能）
    /// 如果要写动态 QT，那么 Insert 需要重新写一套逻辑，要尽可能的简单分割处理，那么时候可以另起一个测试项目来编写处理
    /// 现在的静态版本 QT 的插入时处理了跨 多枝干 的存放到父级节点的优化处理的，如果动态版本的话就不需要了
    /// 但是需要外部对筛选结果的去重
    /// </summary>
    /// <param name="value">插入的对象</param>
    /// <param name="aabb">插入对象对应的aabb</param>
    /// <returns>插入成功返回 true</returns>
    public bool Insert(T value, ref QTAABB aabb)
    {
        Leaf leaf;
        if (leavesDict.TryGetValue(value, out leaf))
        {
            leaf.aabb = aabb;
        }
        else
        {
            leaf = _GetLeafFromPool(value, ref aabb);
            leavesDict[value] = leaf;
        }
        return _Insert(root, leaf);
    }
    /// <summary>
    /// aabb范围筛选
    /// </summary>
    /// <param name="aabb">筛选的aabb范围</param>
    /// <param name="ret">筛选的结构存放列表对象</param>
    public void Select(QTAABB aabb, List<T> ret)
    {
        Select(ref aabb, ret);
    }
    /// <summary>
    /// aabb范围筛选
    /// </summary>
    /// <param name="aabb">筛选的aabb范围</param>
    /// <param name="ret">筛选的结构存放列表对象</param>
    /// <param name="moreActuallySelect">是否进行更精准一些的筛选</param>
    public void Select(ref QTAABB aabb, List<T> ret, bool moreActuallySelect = true)
    {
        ret.Clear();
#if __ENABLE_COMPLETE_CONTAINS_BRANCH_HANDLE__

#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._SelectByAABB");
#endif

        List<T> compeleteInAABB_ret = listDataPool.FromPool();
        _SelectByAABB(ref aabb, ret, compeleteInAABB_ret, root);

#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif

        if (moreActuallySelect)
        {
            for (int i = ret.Count - 1; i > -1; i--)
            {
                if (!aabb.IsIntersect(leavesDict[ret[i]].aabb))
                {
                    ret.RemoveAt(i);
                }
            }
        }
        if (compeleteInAABB_ret.Count > 0)
        {
            ret.AddRange(compeleteInAABB_ret);
        }
        listDataPool.ToPool(compeleteInAABB_ret);
#else
        
#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._SelectByAABB");
#endif

        _SelectByAABB(ref aabb, ret, root);

#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif

        if (moreActuallySelect)
        {
            for (int i = ret.Count - 1; i > -1; i--)
            {
                if (!aabb.IsIntersect(leavesDict[ret[i]].aabb))
                {
                    ret.RemoveAt(i);
                }
            }
        }
#endif
    }
    /// <summary>
    /// aabb范围筛选
    /// </summary>
    /// <param name="x">筛选的aabb范围的x</param>
    /// <param name="y">筛选的aabb范围的y</param>
    /// <param name="w">筛选的aabb范围的w</param>
    /// <param name="h">筛选的aabb范围的h</param>
    /// <param name="ret">筛选的结构存放列表对象</param>
    public void Select(float x, float y, float w, float h, List<T> ret)
    {
        Select(new QTAABB { x = x, y = y, w = w, h = h }, ret);
    }
    /// <summary>
    /// 点选
    /// </summary>
    /// <param name="pos">点选坐标</param>
    /// <param name="ret">点选的结构存放列表对象</param>
    public void Select(Vector2 pos, List<T> ret)
    {
        Select(ref pos, ret);
    }
    /// <summary>
    /// 点选
    /// </summary>
    /// <param name="pos">点选坐标</param>
    /// <param name="ret">点选的结构存放列表对象</param>
    /// <param name="moreActuallySelect">是否进行更精准一些的筛选</param>
    public void Select(ref Vector2 pos, List<T> ret, bool moreActuallySelect = true)
    {
#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree.Select(ref Vector2 pos, List<T> ret, bool moreActuallySelect = true)");
#endif
        ret.Clear();
        _SelectByPos(ref pos, ret, root);
        if (moreActuallySelect)
        {
            for (int i = ret.Count - 1; i > -1; i--)
            {
                if (!leavesDict[ret[i]].aabb.Contains(pos))
                {
                    ret.RemoveAt(i);
                }
            }
        }
#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
    }
    /// <summary>
    /// 点选
    /// </summary>
    /// <param name="x">点选坐标x</param>
    /// <param name="y">点选坐标y</param>
    /// <param name="ret">点选的结构存放列表对象</param>
    public void Select(float x, float y, List<T> ret)
    {
        Select(new Vector2(x, y), ret);
    }
    /// <summary>
    /// 多选
    /// </summary>
    /// <param name="aabbs">多个aabb范围</param>
    /// <param name="ret">筛选的结构存放列表对象</param>
    /// <param name="moreActuallySelect">是否进行更精准一些的筛选（注意：还是 AABB 的概要筛选，如果需要精准到自定义的碰撞筛选级别，这里最好传入 false，再让外部去筛选, 会增加消耗）</param>
    public void Select(List<QTAABB> aabbs, List<T> ret, bool moreActuallySelect = true)
    {
        ret.Clear();

#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree.Select(List<QTAABB> aabbs, List<T> ret, bool moreActuallySelect = true)");
#endif


#if __ENABLE_COMPLETE_CONTAINS_BRANCH_HANDLE__

#   if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree.Select.1");
#   endif
        List<T> compeleteInAABB_ret = listDataPool.FromPool();
        _SelectByAABBS(aabbs, ret, compeleteInAABB_ret, root);
#   if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#   endif

#   if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree.Select.2"); // 消耗有点大
#   endif
        if (moreActuallySelect)
        {
            for (int i = ret.Count - 1; i > -1; i--)
            {
                var intersect = false;
                foreach (var aabb in aabbs)
                {
                    if (aabb.IsIntersect(leavesDict[ret[i]].aabb))
                    {
                        intersect = true;
                        break;
                    }
                }
                if (!intersect)
                {
                    ret.RemoveAt(i);
                }
            }
        }
#   if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#   endif

        if (compeleteInAABB_ret.Count > 0)
        {
            ret.AddRange(compeleteInAABB_ret);
        }
        listDataPool.ToPool(compeleteInAABB_ret);
#else

#   if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree.Select.1");
#   endif
        _SelectByAABBS(aabbs, ret, root);
#   if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#   endif

#   if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree.Select.2"); // 消耗有点大
#   endif
        if (moreActuallySelect)
        {
            for (int i = ret.Count - 1; i > -1; i--)
            {
                var intersect = false;
                foreach (var aabb in aabbs)
                {
                    if (aabb.IsIntersect(leavesDict[ret[i]].aabb))
                    {
                        intersect = true;
                        break;
                    }
                }
                if (!intersect)
                {
                    ret.RemoveAt(i);
                }
            }
        }

#   if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#   endif

#endif

#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
    }
    /// <summary>
    /// aabb范围筛选，并根据 relactivePos 点与 QuadTree 的 cullingDistance 做距离剔除
    /// </summary>
    /// <param name="aabb">筛选的aabb范围</param>
    /// <param name="ret">筛选的结构存放列表对象</param>
    /// <param name="selectFromPos">与 QuadTree 的 cullingDistance 做距离剔除 的相关的点</param>
    public void Select(QTAABB aabb, List<T> ret, Vector2 selectFromPos)
    {
        Select(ref aabb, ret, selectFromPos);
    }
    /// <summary>
    /// aabb范围筛选
    /// </summary>
    /// <param name="aabb">筛选的aabb范围</param>
    /// <param name="ret">筛选的结构存放列表对象</param>
    /// <param name="selectFromPos">与 QuadTree 的 cullingDistance 做距离剔除 的相关的点</param>
    /// <param name="moreActuallySelect">是否进行更精准一些的筛选</param>
    public void Select(ref QTAABB aabb, List<T> ret, Vector2 selectFromPos, bool moreActuallySelect = true)
    {
        if (_IsCullingDistance(ref aabb, selectFromPos))
        {
            ret.Clear();
            return;
        }
        Select(ref aabb, ret, moreActuallySelect);
    }
    /// <summary>
    /// aabb范围筛选
    /// </summary>
    /// <param name="x">筛选的aabb范围的x</param>
    /// <param name="y">筛选的aabb范围的y</param>
    /// <param name="w">筛选的aabb范围的w</param>
    /// <param name="h">筛选的aabb范围的h</param>
    /// <param name="ret">筛选的结构存放列表对象</param>
    /// <param name="selectFromPos">与 QuadTree 的 cullingDistance 做距离剔除 的相关的点</param>
    public void Select(float x, float y, float w, float h, List<T> ret, Vector2 selectFromPos)
    {
        if (_IsCullingDistance(new QTAABB { x = x, y = y, w = w, h = h }, selectFromPos))
        {
            ret.Clear();
            return;
        }
        Select(x, y, w, h, ret);
    }
    /// <summary>
    /// 点选
    /// </summary>
    /// <param name="pos">点选坐标</param>
    /// <param name="ret">点选的结构存放列表对象</param>
    /// <param name="selectFromPos">与 QuadTree 的 cullingDistance 做距离剔除 的相关的点</param>
    public void Select(Vector2 pos, List<T> ret, Vector2 selectFromPos)
    {
        if (_IsCullingDistance(pos, selectFromPos))
        {
            ret.Clear();
            return;
        }
        Select(ref pos, ret);
    }
    public void Select(ref Vector2 pos, List<T> ret, Vector2 selectFromPos, bool moreActuallySelect = true)
    {
        if (_IsCullingDistance(pos, selectFromPos))
        {
            ret.Clear();
            return;
        }
        Select(ref pos, ret, moreActuallySelect);
    }
    /// <summary>
    /// 点选
    /// </summary>
    /// <param name="x">点选坐标x</param>
    /// <param name="y">点选坐标y</param>
    /// <param name="ret">点选的结构存放列表对象</param>
    /// <param name="selectFromPos">与 QuadTree 的 cullingDistance 做距离剔除 的相关的点</param>
    public void Select(float x, float y, List<T> ret, Vector2 selectFromPos)
    {
        Select(new Vector2(x, y), ret, selectFromPos);
    }
    /// <summary>
    /// 多选
    /// </summary>
    /// <param name="aabbs">多个aabb范围</param>
    /// <param name="ret">筛选的结构存放列表对象</param>
    /// <param name="selectFromPos">与 QuadTree 的 cullingDistance 做距离剔除 的相关的点</param>
    /// <param name="moreActuallySelect">是否进行更精准一些的筛选（注意：还是 AABB 的概要筛选，如果需要精准到自定义的碰撞筛选级别，这里最好传入 false，再让外部去筛选, 会增加消耗）</param>
    public void Select(List<QTAABB> aabbs, List<T> ret, Vector2 selectFromPos, bool moreActuallySelect = true)
    {
        if (aabbs.Count == 0)
        {
            return;
        }

        aabbs_helper.Clear();
        aabbs_helper.AddRange(aabbs);
        for (int i = 0; i < aabbs.Count; i++)
        {
            if (_IsCullingDistance(aabbs_helper[i], selectFromPos))
            {
                aabbs_helper.RemoveRange(i, aabbs.Count - i);
                break;
            }
        }

        if (aabbs_helper.Count == 0)
        {
            return;
        }

        Select(aabbs_helper, ret, moreActuallySelect);
    }
    private bool _IsCullingDistance(Vector2 selectPos, Vector2 selectFormPos)
    {
        // culling by distance
        return _IsCullingDistance(ref selectPos, selectFormPos);
    }
    private bool _IsCullingDistance(ref Vector2 selectPos, Vector2 selectFormPos)
    {
        // culling by distance
        var cd = cullingDistance;
        if (!float.IsNaN(cd) && cd > 0)
        {
            var pow2cd = cd * cd;
            if (pow2cd < Vector2.SqrMagnitude(selectPos - selectFormPos))
            {
                return true;
            }
        }
        return false;
    }
    private bool _IsCullingDistance(QTAABB aabb, Vector2 relactivePos)
    {
        return _IsCullingDistance(ref aabb, relactivePos);
    }
    private bool _IsCullingDistance(ref QTAABB aabb, Vector2 relactivePos)
    {
        // culling by distance
        var cd = cullingDistance;
        if (!float.IsNaN(cd) && cd > 0)
        {
            var pow2cd = cd * cd;

            var tl = aabb.top_left;
            var tr = aabb.top_right;
            var br = aabb.bottom_right;
            var bl = aabb.bottom_left;

            var culling_count = 0;
            if (pow2cd < Vector2.SqrMagnitude(tl - relactivePos))
            {
                culling_count++;
            }
            if (pow2cd < Vector2.SqrMagnitude(tr - relactivePos))
            {
                culling_count++;
            }
            if (pow2cd < Vector2.SqrMagnitude(br - relactivePos))
            {
                culling_count++;
            }
            if (pow2cd < Vector2.SqrMagnitude(bl - relactivePos))
            {
                culling_count++;
            }
            return (culling_count == 4);
        }
        return false;
    }

#if __ENABLE_COMPLETE_CONTAINS_BRANCH_HANDLE__
    private void _SelectByAABB(ref QTAABB aabb, List<T> ret, List<T> compeleteInAABB_ret, Branch branch)
    {
        if (branch == null)
        {
            return;
        }

#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._SelectByAABB(ref QTAABB aabb, List<T> ret, List<T> compeleteInAABB_ret, Branch branch)");
#endif
        if (branch.aabb.Contains(ref aabb))
        {
            // 完整包含了底下枝干的 aabb
            _SelectAllValues(branch, compeleteInAABB_ret);
        }
        else
        {
            // 与部分的交集
            if (branch.aabb.IsIntersect(ref aabb))
            {
                foreach (var l in branch.leaves)
                {
                    ret.Add(l.value);
                }
                foreach (var l in branch.crossBranchesLeaves)
                {
                    ret.Add(l.value);
                }
                foreach (var b in branch.branches)
                {
                    _SelectByAABB(ref aabb, ret, compeleteInAABB_ret, b);
                }
            }
        }

#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
    }
#else
    private void _SelectByAABB(ref QTAABB aabb, List<T> ret, Branch branch)
    {
        if (branch == null)
        {
            return;
        }

#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._SelectByAABB(ref QTAABB aabb, List<T> ret, Branch branch)");
#endif
        if (branch.aabb.Contains(ref aabb))
        {
            // 完整包含了底下枝干的 aabb
            _SelectAllValues(branch, ret);
        }
        else
        {
            // 与部分的交集
            if (branch.aabb.IsIntersect(ref aabb))
            {
                foreach (var l in branch.leaves)
                {
                    ret.Add(l.value);
                }
                foreach (var l in branch.crossBranchesLeaves)
                {
                    ret.Add(l.value);
                }
                foreach (var b in branch.branches)
                {
                    _SelectByAABB(ref aabb, ret, b);
                }
            }
        }
#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
    }
#endif
    private void _SelectAllValues(Branch branch, List<T> ret)
    {
        if (branch == null)
        {
            return;
        }
#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._SelectAllValues(Branch branch, List<T> ret)");
#endif
        if (branch != null)
        {
            foreach (var l in branch.leaves)
            {
                ret.Add(l.value);
            }
            foreach (var l in branch.crossBranchesLeaves)
            {
                ret.Add(l.value);
            }
            foreach (var b in branch.branches)
            {
                _SelectAllValues(b, ret);
            }
        }
#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
    }
    private void _SelectByPos(ref Vector2 pos, List<T> ret, Branch branch)
    {
        if (branch == null)
        {
            return;
        }

#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._SelectByPos(ref Vector2 pos, List<T> ret, Branch branch)");
#endif
        if (branch.aabb.Contains(ref pos))
        {
            foreach (var l in branch.leaves)
            {
                ret.Add(l.value);
            }
            foreach (var l in branch.crossBranchesLeaves)
            {
                ret.Add(l.value);
            }
            foreach (var b in branch.branches)
            {
                _SelectByPos(ref pos, ret, b);
            }
        }
#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
    }
#if __ENABLE_COMPLETE_CONTAINS_BRANCH_HANDLE__
    private void _SelectByAABBS(List<QTAABB> aabbs, List<T> ret, List<T> compeleteInAABB_ret, Branch branch)
    {
        if (branch == null)
        {
            return;
        }
#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._SelectByAABBS(List<QTAABB> aabbs, List<T> ret, List<T> compeleteInAABB_ret, Branch branch)");
#endif
        if (branch.aabb.AnyContainsBy(aabbs))
        {
            // 完整包含了底下枝干的 aabb
            _SelectAllValues(branch, compeleteInAABB_ret);
        }
        else
        {
            // 与部分的交集
            if (branch.aabb.AnyIntersect(aabbs))
            {
                foreach (var l in branch.leaves)
                {
                    ret.Add(l.value);
                }
                foreach (var l in branch.crossBranchesLeaves)
                {
                    ret.Add(l.value);
                }
                foreach (var b in branch.branches)
                {
                    _SelectByAABBS(aabbs, ret, compeleteInAABB_ret, b);
                }
            }
        }
#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
    }
#else
    private void _SelectByAABBS(List<QTAABB> aabbs, List<T> ret, Branch branch)
    {
        if (branch == null)
        {
            return;
        }
#   if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._SelectByAABBS");
#   endif
        if (branch.aabb.AnyContainsBy(aabbs))
        {
            // 完整包含了底下枝干的 aabb
            _SelectAllValues(branch, ret);
        }
        else
        {
            // 与部分的交集
            if (branch.aabb.AnyIntersect(aabbs))
            {
                foreach (var l in branch.leaves)
                {
                    ret.Add(l.value);
                }
                foreach (var l in branch.crossBranchesLeaves)
                {
                    ret.Add(l.value);
                }
                foreach (var b in branch.branches)
                {
                    _SelectByAABBS(aabbs, ret, b);
                }
            }
        }


#   if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#   endif
    }
#endif

    private bool _Insert(Branch branch, Leaf leaf)
    {
#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._Insert(Branch branch, Leaf leaf)");
#endif
        var ret = false;
        if (!branch.aabb.IsIntersect(ref leaf.aabb))
        {
            // 不在该枝干管理范围外，则不处理插入
            //ret = false;
        }
        else
        {
            if (branch.hasSplit)
            {
                // 将之前 叶子 的插入到 子枝干 上去
                if (branch.leaves.Count > 0)
                {
                    _SrcLeavesInsertToSubBranches(branch);
                }
                ret = _InsertSingleLeaf(branch, leaf);
            }
            else
            {
                if (branch.depth <= maxLevel && (branch.leaves.Count + branch.crossBranchesLeaves.Count) >= maxLeafPerBranch)
                {
                    // 已达最大深度限制，已超过对应的数量，那么再次细分该枝干
                    branch.hasSplit = true;
                    ret = _Insert(branch, leaf);
                }
                else
                {
                    // 未达最大深度限制，未超过对应的数量，那么插入该枝干
                    branch.leaves.Add(leaf);
                    leaf.branch = branch;
                    ret = true;
                }
            }
        }
#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
        return ret;
    }
    private void _SrcLeavesInsertToSubBranches(Branch branch)
    {
#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._SrcLeavesInsertToSubBranches(Branch branch)");
#endif
        for (int i = 0; i < branch.leaves.Count; i++)
        {
            var l = branch.leaves[i];
            _InsertSingleLeaf(branch, l);
        }
        branch.leaves.Clear();
#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
    }

    private bool _InsertSingleLeaf(Branch branch, Leaf leaf)
    {
#if __ENABLE_QT_PROFILER__
        Profiler.BeginSample($"QuadTree._InsertSingleLeaf(Branch branch, Leaf leaf)");
#endif
        var contains_with_branch_count = 0;
        var branch_idx = -1;
        for (int i = 0; i < 4; i++)
        {
            if (branch.aabbs[i].IsIntersect(ref leaf.aabb))
            {
                contains_with_branch_count++;
                insert_helper[i] = true;
                branch_idx = i;
                if (branch.branches[i] == null || branch.branches[i].hasRecycle)
                {
                    branch.branches[i] = _GetBranchFromPool(branch, branch.depth + 1, ref branch.aabbs[i]);
                }
            }
        }

        var ret = false;
        if (contains_with_branch_count > 1)
        {
            // 与多个枝干有交集，就插入到父级的枝干上
            // 这里就不判断 maxLeafPerBranch 的限制了
            branch.crossBranchesLeaves.Add(leaf);
            leaf.branch = branch;
            ret = true;
        }
        else
        {
            // 插入到对应的枝干上
            //System.Diagnostics.Debug.Assert(branch_idx > -1 && branch_idx < 4);
            if (branch_idx < 0 || branch_idx > 3) branch_idx = 3;
            ret = _Insert(branch.branches[branch_idx], leaf);
        }
#if __ENABLE_QT_PROFILER__
        Profiler.EndSample();
#endif
        return ret;
    }

    private Branch _GetBranchFromPool(Branch parent, int depth, ref QTAABB aabb)
    {
        var ret = branchPool.Count > 0 ? branchPool.Pop() : new Branch();
        ret.belongTree = this;
        ret.parent = parent;
        ret.depth = depth;
        ret.aabb = aabb;
        ret.hasRecycle = false;
        float halfW = aabb.w * 0.5f;
        float halfH = aabb.h * 0.5f;
        float midX = aabb.x + halfW;
        float midY = aabb.y + halfH;
        ret.aabbs[0].Set(aabb.x, aabb.y, halfW, halfH); // top-left
        ret.aabbs[1].Set(midX,   aabb.y, halfW, halfH); // top-right
        ret.aabbs[2].Set(midX,   midY,   halfW, halfH); // bottom-right
        ret.aabbs[3].Set(aabb.x, midY,   halfW, halfH); // bottom-left
        return ret;
    }
    private void _RecycleBranchToPool(Branch branch)
    {
        if (branch == null)
        {
            return;
        }
        branch.belongTree = null;
        branch.parent = null;
        branch.hasSplit = false;
        branch.hasRecycle = true;
        branchPool.Push(branch);

        foreach (var l in branch.leaves)
        {
            _RecycleLeafToPool(l);
        }
        branch.leaves.Clear();
        foreach (var l in branch.crossBranchesLeaves)
        {
            _RecycleLeafToPool(l);
        }
        branch.crossBranchesLeaves.Clear();

        for (int i = 0; i < branch.branches.Length; i++)
        {
            _RecycleBranchToPool(branch.branches[i]);
            branch.branches[i] = null;
        }
    }
    private Leaf _GetLeafFromPool(T value, ref QTAABB aabb)
    {
        var ret = leafPool.Count > 0 ? leafPool.Pop() : new Leaf();
        ret.value = value;
        ret.aabb = aabb;
        return ret;
    }
    private void _RecycleLeafToPool(Leaf leaf)
    {
        leaf.branch = null;
        leaf.value = default;
        leafPool.Push(leaf);
    }
    private void _Reset(float x, float y, float w, float h,
        int maxLevel = DEFAULT_MAX_LEVEL, int maxLeafPerBranch = DEFAULT_MAX_LEAF_PER_BRANCH)
    {
        System.Diagnostics.Debug.Assert(w == 0 || h == 0, "QTAABB is Zero");
        System.Diagnostics.Debug.Assert(maxLevel < MAX_LIMIT_LEVEL, $"QuadTree MaxLevel cannot more than : {MAX_LIMIT_LEVEL}");

        this.maxLevel            = maxLevel;
        this.maxLeafPerBranch    = maxLeafPerBranch;

        var aabb = new QTAABB { x = x, y = y, w = w, h = h };
        if (root != null)
        {
            root.aabb = aabb;
        }
        else
        {
            root = _GetBranchFromPool(null, 0, ref aabb);
        }
    }
}