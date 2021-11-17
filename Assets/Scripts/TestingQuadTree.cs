using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 测试自己编写的 静态 QuadTree 的功能
/// 如果要编写 动态版本的 QuadTree，那么再 Insert 的逻辑要尽可能简要
/// 后续有空我可以再写另一个 动态版本的 QuadTree
/// date    : 2021/02/20
/// author  : jave.lin
/// </summary>
public class TestingQuadTree : MonoBehaviour
{
    public enum eShowGOType
    {
        None = 0,
        ShowInFrustum = 1,
        ShowNotInFrustum = 2,
        All = -1,
    }

    [Header("镜头")]
    public Camera cam;

    [Header("四叉树内的对象")]
    public GameObject[] goes;

    [Header("原始 四叉树 的根对象")]
    public GameObject goes_root;

    [Header("绘制镜头的 aabb 的颜色")]
    public Color cam_aabb_color = Color.red;
    [Header("绘制镜头视锥的 aabb 的颜色")]
    public Color cam_frustum_color = Color.yellow;
    [Header("绘制四叉树的枝干 aabb 的颜色")]
    public Color qt_branches_color = Color.cyan;
    [Header("绘制四叉树的叶子 aabb 的颜色")]
    public Color qt_leaves_color = Color.green;
    [Header("绘制镜头视锥内的 aabb 叉树的叶子的 aabb 的颜色")]
    public Color qt_leaves_in_frustum_color = Color.blue;

    [Header("是否绘制镜头的 aabb")]
    public bool draw_gizmos_cam_aabb = true;
    [Header("是否绘制镜头视锥的 wireframe")]
    public bool draw_gizmos_cam_wireframe = true;
    [Header("是否绘制四叉树的枝干 aabb")]
    public bool draw_gizmos_qt_branches = true;
    [Header("是否绘制四叉树的叶子 aabb")]
    public bool draw_gizmos_qt_leaves = true;
    [Header("是否绘制镜头视锥内的 aabb 叉树的叶子的 aabb")]
    public bool draw_gizmos_qt_leaves_in_frustum_aabb = true;

    [Header("控制 GameObject 的显示类型")]
    public eShowGOType show_GO_type = eShowGOType.ShowInFrustum;

    [Header("视锥水平剔除空间扩大的 unit 单位")]
    public float frustum_h_padding = 5;

    [Header("视锥分段的多层 AABB 的级别")]
    [Range(1, 20)]
    public int frustum_AABB_level = 3;

    [Header("开启的话，结果会再精准一些，但会增加部分计算量（也可以关闭后，让外部来处理：稍微精确一些的筛选）")]
    public bool more_actually_select = true;

    [Header("测试时实时重插四叉树(暴力重构会导致很卡，消耗 CPU 很大，3456 个对象就需要花费 8.5ms-+)")]
    public bool reconstrct_qt_runtime = false;

    private QuadTree<GameObject> qt;
    private List<GameObject> qt_select_ret_helper;
    private List<QTAABB> cam_aabbs = new List<QTAABB>();

    private void Start()
    {
        // 计算整个场景的 bounds
        var scene_bounds = CalculateSceneAABB();

        // 创建四叉树对象
        qt = new QuadTree<GameObject>(scene_bounds);
        qt_select_ret_helper = new List<GameObject>();

        // 给 qt 插入每一个
        InsertQTObjs();
    }

    private void Update()
    {
        QT_Select();
        UpdateGoVisible();

        if (reconstrct_qt_runtime)
        {
            ReconstructQT();
        }
    }

    private Bounds CalculateSceneAABB()
    {
        var ret = new Bounds();
        foreach (var go in goes)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                ret.Encapsulate(renderer.bounds);
            }
        }
        return ret;
    }

    private void QT_Select()
    {
        // 概要筛选
        qt.Select(cam_aabbs, qt_select_ret_helper, more_actually_select);
    }

    private void UpdateGoVisible()
    {
        switch (show_GO_type)
        {
            case eShowGOType.None:
                goes_root.SetAct(false);
                break;
            case eShowGOType.ShowInFrustum:
                goes_root.SetAct(true);
                foreach (var go in goes)
                {
                    go.SetAct(false);
                }
                if (qt != null && qt_select_ret_helper != null)
                {
                    // 概要筛选
                    qt.Select(cam_aabbs, qt_select_ret_helper, more_actually_select);
                    foreach (var go in qt_select_ret_helper)
                    {
                        go.SetAct(true);
                    }
                }
                break;
            case eShowGOType.ShowNotInFrustum:
                goes_root.SetAct(true);
                foreach (var go in goes)
                {
                    go.SetAct(true);
                }
                if (qt != null && qt_select_ret_helper != null)
                {
                    // 概要筛选
                    qt.Select(cam_aabbs, qt_select_ret_helper, more_actually_select);
                    foreach (var go in qt_select_ret_helper)
                    {
                        go.SetAct(false);
                    }
                }
                break;
            case eShowGOType.All:
                goes_root.SetAct(true);
                foreach (var go in goes)
                {
                    go.SetAct(true);
                }
                break;
            default:
                break;
        }
    }

    private void InsertQTObjs()
    {
        // 给 qt 插入每一个
        foreach (var go in goes)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                qt.Insert(go, renderer.bounds);
            }
        }
    }
    private void ReconstructQT()
    {
        // 暴力重构法

        // 先全部重新清理
        qt.Clear();
        // 全部重新插入
        InsertQTObjs();

        // 动态四叉树，暂时不处理（后续需要再查阅资料，学习后，再制作）
    }

    private void OnDrawGizmos()
    {
        // 绘制 cam 的 aabb
        if (draw_gizmos_cam_aabb) _DrawCameraAABB();
        // 绘制 cam 的 wireframe
        if (draw_gizmos_cam_wireframe) _DrawCameraWireframe();
        // 绘制 qt 中每个枝干 的 aabb
        if (draw_gizmos_qt_branches) _DrawQTBranchesAABBs();
        // 绘制 qt 中每个 leaf 的 aabb
        if (draw_gizmos_qt_leaves) _DrawQTLeavesAABBs();
        // 绘制 qt 中每个在 frustum aabb 内的 leaf 的 aabb
        if (draw_gizmos_qt_leaves_in_frustum_aabb) _DrawQTInFrustumLeavesAABBs();
    }

    private void _DrawCameraAABB()
    {
        QTAABB.GetCameraAABBs(cam, cam_aabbs, frustum_AABB_level, frustum_h_padding);

        foreach (var aabb in cam_aabbs)
        {
            _DrawQTAABB(aabb, cam_aabb_color);
        }
    }

    private void _DrawCameraWireframe()
    {
        Gizmos.color = cam_frustum_color;

        // 可参考我以前的一篇文章：https://blog.csdn.net/linjf520/article/details/104994304#SceneGizmos_35
        Matrix4x4 temp = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(cam.transform.position, cam.transform.rotation, Vector3.one);
        if (!cam.orthographic)
        {
            // 透视视锥
            Gizmos.DrawFrustum(Vector3.zero, cam.fieldOfView, cam.farClipPlane, cam.nearClipPlane, cam.aspect);
        }
        else
        {
            // 正交 cube
            var far = cam.farClipPlane;
            var near = cam.nearClipPlane;
            var delta_fn = far - near;

            var half_height = cam.orthographicSize;
            var half_with = cam.aspect * half_height;
            var pos = Vector3.forward * (delta_fn * 0.5f + near);
            var size = new Vector3(half_with * 2, half_height * 2, delta_fn);

            Gizmos.DrawWireCube(pos, size);
        }
        Gizmos.matrix = temp;
    }

    private void _DrawQTBranchesAABBs()
    {
        if (qt != null)
        {
            _DrawBranch(qt.root, qt_branches_color);
        }
    }

    private void _DrawQTLeavesAABBs()
    {
        if (qt != null)
        {
            _DrawLeafsOfBrances(qt.root, qt_leaves_color);
        }
    }

    private void _DrawQTInFrustumLeavesAABBs()
    {
        if (qt != null && qt_select_ret_helper != null)
        {
            foreach (var go in qt_select_ret_helper)
            {
                var renderer = go.GetComponent<Renderer>();
                _DrawBoundsXZ(renderer.bounds, qt_leaves_in_frustum_color);
            }
        }
    }

    private void _DrawBranch(QuadTree<GameObject>.Branch branch, Color color)
    {
        if (branch == null)
        {
            return;
        }

        // draw this branch
        _DrawQTAABB(branch.aabb, color);

        // draw sub branches
        foreach (var b in branch.branches)
        {
            _DrawBranch(b, color);
        }
    }

    private void _DrawLeafsOfBrances(QuadTree<GameObject>.Branch branch, Color color)
    {
        if (branch == null)
        {
            return;
        }
        foreach (var b in branch.branches)
        {
            if (b == null)
            {
                continue;
            }
            foreach (var l in b.leaves)
            {
                _DrawQTAABB(l.aabb, color);
            }
            _DrawLeafsOfBrances(b, color);
        }
    }

    private void _DrawBoundsXZ(Bounds bounds, Color color)
    {
        Gizmos.color = color;

        var min = bounds.min;
        var max = bounds.max;

        var start_pos = min;
        var end_pos = min;
        end_pos.x = max.x;

        Gizmos.DrawLine(start_pos, end_pos);

        start_pos = end_pos;
        end_pos = start_pos;
        end_pos.z = max.z;

        Gizmos.DrawLine(start_pos, end_pos);

        start_pos = end_pos;
        end_pos = start_pos;
        end_pos.x = min.x;

        Gizmos.DrawLine(start_pos, end_pos);

        start_pos = end_pos;
        end_pos = start_pos;
        end_pos.z = min.z;

        Gizmos.DrawLine(start_pos, end_pos);
    }

    private void _DrawQTAABB(QTAABB aabb, Color color)
    {
        Gizmos.color = color;

        var min = aabb.min;
        var max = aabb.max;

        var start_pos = new Vector3(min.x, 0, min.y);
        var end_pos = start_pos;
        end_pos.x = max.x;

        Gizmos.DrawLine(start_pos, end_pos);

        start_pos = end_pos;
        end_pos = start_pos;
        end_pos.z = max.y;

        Gizmos.DrawLine(start_pos, end_pos);

        start_pos = end_pos;
        end_pos = start_pos;
        end_pos.x = min.x;

        Gizmos.DrawLine(start_pos, end_pos);

        start_pos = end_pos;
        end_pos = start_pos;
        end_pos.z = min.y;

        Gizmos.DrawLine(start_pos, end_pos);
    }
}

public static class MyExt
{
    public static void SetAct(this GameObject go, bool value)
    {
        if (go.activeSelf != value)
        {
            go.SetActive(value);
        }
    }
}