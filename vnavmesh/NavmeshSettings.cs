using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using DotRecast.Recast;
using System;

namespace Navmesh;

public class NavmeshSettings
{
    [Flags]
    public enum Filter
    {
        None = 0,
        LowHangingObstacles = 1 << 0,
        LedgeSpans = 1 << 1,
        WalkableLowHeightSpans = 1 << 2,
        Interiors = 1 << 3,
    }

    public float CellSize = 0.25f;
    public float CellHeight = 0.25f;
    public float AgentHeight = 2.0f;
    public float AgentRadius = 0.5f;
    public float AgentMaxClimb = 0.5f;
    public float AgentMaxSlopeDeg = 55f;
    public Filter Filtering = Filter.LowHangingObstacles | Filter.LedgeSpans | Filter.WalkableLowHeightSpans;
    public float RegionMinSize = 8;
    public float RegionMergeSize = 20;
    public RcPartition Partitioning = RcPartition.WATERSHED;
    public float PolyMaxEdgeLen = 12f;
    public float PolyMaxSimplificationError = 1.5f;
    public int PolyMaxVerts = 6;
    public float DetailSampleDist = 6f;
    public float DetailMaxSampleError = 1f;

    public bool GenerateEdgeClimbLinks = false;
    public bool GenerateEdgeJumpLinks = false;
    public float GroundTolerance = 0.3f;
    public float ClimbDownDistance = 0.4f;
    public float ClimbDownMaxHeight = 3.2f;
    public float ClimbDownMinHeight = 1.5f;
    public float EdgeJumpEndDistance = 2f;
    public float EdgeJumpHeight = 1.8f;
    public float EdgeJumpMaxDrop = 500f;
    public float EdgeJumpMinDrop = 1.5f;

    // we assume that bounds are constant -1024 to 1024 along each axis (since that's the quantization range of position in some packets)
    // there is some code that relies on tiling being power-of-2
    // current values mean 128x128x128 L1 tiles -> 16x16x16 L2 tiles -> 2x2x2 voxels
    public int[] NumTiles = [16, 8, 8];


    public void Draw()
    {
        DrawConfigFloat(ref CellSize, 0.1f, 1.0f, 0.01f, "光栅化：单元格大小 (#cs)", """
            【体素精度】(Cell Size) 核心分辨率。决定将物理世界切分为多细小的方块。
            数值越小 = 贴合度极高，能识别栏杆缝隙，但构建时间指数级暴涨。
            建议：野外 0.25，复杂副本/迷宫 0.15。
            """);
        DrawConfigFloat(ref CellHeight, 0.1f, 1.0f, 0.01f, "光栅化：单元格高度 (#ch)", """
            【垂直精度】(Cell Height) Y轴切分粒度。
            决定了系统能识别多微小的地面起伏（如地毯厚度）。
            建议：保持 0.25 默认，过小会导致平地产生噪点。
            """);
        DrawConfigFloat(ref AgentHeight, 0.1f, 5.0f, 0.1f, "代理：高度 (Height)", """
            【通行高度】(Height) 判定空气墙的垂直阈值。
            只有净空高度 > 此值的通道，才会被标记为可行走。
            设太大 = 进不去拉拉肥能进的洞；设太小 = 容易卡头。
            """);
        DrawConfigFloat(ref AgentRadius, 0.0f, 5.0f, 0.1f, "代理：半径 (Radius)", """
            【碰撞半径】(Radius) 决定网格边缘的安全红线。
            数值 = 角色模型半径 + 安全余量。
            调大 = 远离墙壁（防卡死）；调小 = 贴墙极限走位（可能穿模）。
            """);
        DrawConfigFloat(ref AgentMaxClimb, 0.1f, 5.0f, 0.1f, "代理：最大攀爬高度 (Max Climb)", """
            【最大跨步】(Max Climb) 决定台阶判定的阈值。
            角色能直接走上去的最大高度差。
            若在楼梯处卡顿，通常是因为此值 < 台阶实际高度。
            """);
        DrawConfigFloat(ref AgentMaxSlopeDeg, 0.0f, 90.0f, 1.0f, "代理：最大坡度 (Max Slope)", """
            【爬坡极限】(Max Slope) 决定斜坡是否为空气墙。
            超过此角度的表面将被剔除出导航网格。
            建议：55度可覆盖绝大多数游戏地形。
            """);
        DrawConfigFilteringCombo(ref Filtering, "过滤 (Filtering)", """
            【障碍过滤】(Filtering) 决定哪些特殊地形被剔除。
            LowHanging = 剔除低矮悬垂物；LedgeSpans = 处理悬崖边缘。
            建议：全选，保证生成的网格最干净。
            """);
        DrawConfigFloat(ref RegionMinSize, 0.0f, 150.0f, 1.0f, "区域：最小尺寸", """
            【孤岛剔除】(Min Size) 噪点过滤器。
            小于此体素数的独立区域（如浮空石、柱子顶）将被直接抹除。
            防止寻路算法计算出无法抵达的无效路径。
            """);
        DrawConfigFloat(ref RegionMergeSize, 0.0f, 150.0f, 1.0f, "区域：合并尺寸", """
            【碎片合并】(Merge Size) 拓扑优化参数。
            过小的区域会被强行融合进邻近的主区域，减少多边形碎片。
            """);
        DrawConfigPartitioningCombo(ref Partitioning, "分区算法", """
            【分区算法】(Partitioning) 网格切割逻辑。
            Watershed (分水岭) = 质量最高，计算最慢；Monotone = 速度快，质量差。
            建议：FFXIV 地形复杂，请锁死 Watershed。
            """);
        DrawConfigFloat(ref PolyMaxEdgeLen, 0.0f, 50.0f, 1.0f, "多边形化：最大边长", """
            【最大边长】防止生成细长型多边形。
            网格边缘超过此长度时会被强制切分。数值越小，网格边缘越贴合曲线墙壁。
            """);
        DrawConfigFloat(ref PolyMaxSimplificationError, 0.1f, 3.0f, 0.1f, "多边形化：最大简化误差", """
            【简化误差】决定网格边缘的"圆滑度"。
            允许网格轮廓偏离原始体素的最大距离。
            数值越小 = 边缘越精准（锯齿越少）；数值越大 = 顶点越少（性能越好）。
            """);
        DrawConfigInt(ref PolyMaxVerts, 3, 12, 1, "多边形化：每多边形最大顶点数", """
            【多边形顶点数】决定网格的几何复杂度。
            默认 6 (六边形)。增加此值可生成更复杂的多边形，但也增加了寻路算法的负担。
            """); // TODO: fix the limit to make it always suitable for detour
        DrawConfigFloat(ref DetailSampleDist, 0.0f, 16.0f, 1.0f, "细节网格：采样距离", """
            【高度采样率】决定地面的平整度。
            每隔多少距离采样一次高度。
            数值越小 = 完美还原楼梯坡度；数值越大 = 楼梯变成斜坡。
            """);
        DrawConfigFloat(ref DetailMaxSampleError, 0.0f, 16.0f, 1.0f, "细节网格：最大采样误差", """
            【高度拟合误差】允许网格表面偏离真实地面的最大高度差。
            设得太大 = 角色看起来像是在离地漂浮或陷入地下。
            """);
        DrawConfigInt(ref NumTiles[0], 1, 32, 1, "L1 瓦片计数", """
            【一级瓦片】地图切分的第一层级。
            必须是 2 的幂次方。影响导航网格和导航体积。
            """);
        DrawConfigInt(ref NumTiles[1], 1, 32, 1, "L2 瓦片计数", """
            【二级瓦片】地图切分的第二层级。
            必须是 2 的幂次方。仅影响导航体积（飞行寻路）。
            """);
        DrawConfigInt(ref NumTiles[2], 1, 32, 1, "L3 体素计数", """
            【叶节点体素】每个瓦片内的体素数量。
            必须是 2 的幂次方。仅影响导航体积（飞行寻路）。
            """);

        ImGui.Checkbox("生成爬下连接点", ref GenerateEdgeClimbLinks);
        ImGuiComponents.HelpMarker("""
            【自动生成路径】是否允许算法自动计算爬墙的路径点。
            勾选后，插件会尝试在断崖间建立隐形连接线。
            """);
        ImGui.Checkbox("生成跳下连接点", ref GenerateEdgeJumpLinks);
        ImGuiComponents.HelpMarker("""
            【自动生成路径】是否允许算法自动计算跳崖的路径点。
            勾选后，插件会尝试在断崖间建立隐形连接线。
            """);
        DrawConfigFloat(ref GroundTolerance, 0, 50, 0.1f, "地面容差", """
            【地面吸附容差】判定起跳点是否"贴地"的宽容度。
            如果有些跳跃点生成失败，尝试调大此值。
            """);
        DrawConfigFloat(ref ClimbDownDistance, 0, 100, 0.1f, "爬下距离", """
            【爬行水平距离】边缘爬行采样的水平距离。
            """);
        DrawConfigFloat(ref ClimbDownMaxHeight, 0, 100, 0.5f, "爬下最大高度", """
            【爬行物理限制】角色敢从多高爬下来的上限。
            超出此高度的断崖将被视为"死路"，不会生成连接线。
            """);
        DrawConfigFloat(ref ClimbDownMinHeight, 0, 100, 0.5f, "爬下最小高度", """
            【爬行物理限制】角色敢从多高爬下来的下限。
            低于此高度的落差会被视为普通台阶，不生成爬行连接。
            """);
        DrawConfigFloat(ref EdgeJumpEndDistance, 0, 100, 0.5f, "边缘跳跃结束距离", """
            【跳跃物理限制】角色最远能跳多远。
            超出此距离的断崖将被视为"死路"，不会生成连接线。
            """);
        DrawConfigFloat(ref EdgeJumpHeight, 0, 10, 0.1f, "边缘跳跃高度", """
            【跳跃物理限制】跳跃时的最大上升高度。
            """);
        DrawConfigFloat(ref EdgeJumpMaxDrop, 0, 100, 0.1f, "边缘跳跃最大落差", """
            【跳跃物理限制】角色敢从多高跳下来的上限。
            超出此高度的断崖将被视为"死路"，不会生成连接线。
            """);
        DrawConfigFloat(ref EdgeJumpMinDrop, 0, 100, 0.1f, "边缘跳跃最小落差", """
            【跳跃物理限制】角色敢从多高跳下来的下限。
            低于此高度的落差会被视为普通台阶，不生成跳跃连接。
            """);
    }

    private void DrawConfigFloat(ref float value, float min, float max, float increment, string label, string help)
    {
        ImGui.SetNextItemWidth(300);
        ImGui.InputFloat(label, ref value);
        ImGuiComponents.HelpMarker(help);
    }

    private void DrawConfigInt(ref int value, int min, int max, int increment, string label, string help)
    {
        ImGui.SetNextItemWidth(300);
        ImGui.InputInt(label, ref value);
        ImGuiComponents.HelpMarker(help);
    }

    private void DrawConfigFilteringCombo(ref Filter value, string label, string help)
    {
        ImGui.SetNextItemWidth(300);
        using var combo = ImRaii.Combo(label, value.ToString());
        if (!combo)
        {
            ImGuiComponents.HelpMarker(help);
            return;
        }
        DrawConfigFilteringEnum(ref value, Filter.LowHangingObstacles, "低垂障碍物", """
            【低矮悬垂物过滤】将非可行走区域标记为可行走（如果它们的最大高度在下方区域的攀爬范围内）。
            移除小障碍物和光栅化伪影，如路缘石。也允许角色走上楼梯等阶梯结构。
            """);
        DrawConfigFilteringEnum(ref value, Filter.LedgeSpans, "边缘跨度 (Ledge Spans)", """
            【悬崖边缘过滤】将悬崖边缘标记为不可行走。
            悬崖是指一个或多个相邻区域的最大高度超出当前区域攀爬范围的区域。
            防止生成的网格在悬崖上方悬空。
            """);
        DrawConfigFilteringEnum(ref value, Filter.WalkableLowHeightSpans, "可行走低高度跨度", """
            【低净空过滤】将净空高度不足的可行走区域标记为不可行走。
            净空高度 = 当前区域顶部到同一列中下一个更高区域底部的距离。
            防止角色卡头。
            """);
        DrawConfigFilteringEnum(ref value, Filter.Interiors, "内部区域 (Interiors)", """
            【内部区域过滤】将流形几何体内部（或非流形几何体下方）的区域标记为不可行走。
            """);
    }

    private void DrawConfigFilteringEnum(ref Filter value, Filter mask, string label, string help)
    {
        bool set = value.HasFlag(mask);
        if (ImGui.Checkbox(label, ref set))
            value ^= mask;
        ImGuiComponents.HelpMarker(help);
    }

    private void DrawConfigPartitioningCombo(ref RcPartition value, string label, string help)
    {
        ImGui.SetNextItemWidth(300);
        using var combo = ImRaii.Combo(label, value switch
        {
            RcPartition.WATERSHED => "Watershed",
            RcPartition.MONOTONE => "Monotone",
            RcPartition.LAYERS => "Layer",
            _ => "???"
        });
        if (!combo)
        {
            ImGuiComponents.HelpMarker(help);
            return;
        }

        DrawConfigPartitioningEnum(ref value, RcPartition.WATERSHED, "分水岭算法 (Watershed)", """
            【分水岭分区】经典 Recast 分区算法。
            - 生成最漂亮的三角剖分
            - 通常最慢
            - 将高度场分割成无孔洞、无重叠的区域
            - 极少数情况下可能产生孔洞（小障碍物靠近大开阔区域）或重叠（狭窄螺旋走廊如楼梯）
            建议：FFXIV 地形复杂，推荐使用此算法。
            """);
        DrawConfigPartitioningEnum(ref value, RcPartition.MONOTONE, "单调分区 (Monotone)", """
            【单调分区】最快的分区算法。
            - 速度最快
            - 保证无孔洞、无重叠
            - 生成细长多边形，有时导致路径绕远
            适用于需要快速生成导航网格的场景。
            """);
        DrawConfigPartitioningEnum(ref value, RcPartition.LAYERS, "分层 (Layer)", """
            【分层分区】折中方案。
            - 速度较快
            - 将高度场分割成不重叠的区域
            - 依赖三角剖分代码处理孔洞
            - 比单调分区生成更好的三角形
            - 没有分水岭的边角问题
            适用于中小型瓦片的导航网格。
            """);
    }

    private void DrawConfigPartitioningEnum(ref RcPartition value, RcPartition choice, string label, string help)
    {
        if (ImGui.RadioButton(label, value.Equals(choice)))
            value = choice;
        ImGuiComponents.HelpMarker(help);
    }
}
