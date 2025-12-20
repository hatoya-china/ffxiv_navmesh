using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Navmesh;

public class Config
{
    private const int _version = 1;

    public bool AutoLoadNavmesh = true;
    public bool EnableDTR = true;
    public bool ShowQueryStatusInDTR = true;
    public bool AlignCameraToMovement;
    public float AlignCameraHeight = -15;
    public bool ShowWaypoints;
    public bool ForceShowGameCollision;
    public bool CancelMoveOnUserInput;
    public bool StopOnStuck = false;
    public float StuckTolerance = 0.05f;
    public int StuckTimeoutMs = 500;
    public bool RetryOnStuck = true;
    public float RandomnessMultiplier = 1f;
    public bool EnableHumanLikeMovement = true;
    public float RandomCorridorWidth = 0.5f;
    public float RandomNoiseScale = 0.2f;
    public int BuildMaxCores = 1;

    private static readonly int realMaxCores = Environment.ProcessorCount;

    public event Action? Modified;

    public void NotifyModified() => Modified?.Invoke();

    public void Draw()
    {
        if (ImGui.Checkbox("切换区域时自动加载/构建导航数据", ref AutoLoadNavmesh))
            NotifyModified();
        if (ImGui.Checkbox("启用 DTR 状态栏显示", ref EnableDTR))
            NotifyModified();
        if (ImGui.Checkbox("在 DTR 中显示详细查询状态", ref ShowQueryStatusInDTR))
            NotifyModified();
        if (ImGui.Checkbox("视角自动对齐移动方向", ref AlignCameraToMovement))
            NotifyModified();
        using (ImRaii.Disabled(!AlignCameraToMovement))
        {
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderFloat("视角高度 (度)", ref AlignCameraHeight, -75, 75))
                NotifyModified();
        }
        if (ImGui.Checkbox("显示当前寻路点", ref ShowWaypoints))
            NotifyModified();
        if (ImGui.Checkbox("始终可视化游戏碰撞体积", ref ForceShowGameCollision))
            NotifyModified();
        if (ImGui.Checkbox("玩家手动移动时取消寻路", ref CancelMoveOnUserInput))
            NotifyModified();
        if (ImGui.Checkbox("卡住时停止寻路", ref StopOnStuck))
            NotifyModified();

        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderInt("网格构建最大占用核心数", ref BuildMaxCores, -8, realMaxCores))
            NotifyModified();
        ImGuiComponents.HelpMarker("0 = 使用全部；正数 = 使用指定数量；负数 = 保留指定数量空闲");

        if (StopOnStuck)
        {
            if (ImGui.SliderFloat("卡住判定阈值 (米/秒)", ref StuckTolerance, 0.5f, 3f))
                NotifyModified();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("每帧移动距离低于此数值将被判定为卡住。");

            if (ImGui.SliderInt("卡住超时判定 (毫秒)", ref StuckTimeoutMs, 100, 10_000))
                NotifyModified();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("处于卡住阈值下的持续时间超过此数值则停止。");

            if (ImGui.Checkbox("停止后尝试重新寻路", ref RetryOnStuck))
                NotifyModified();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("启用后，在判定卡住停止后将尝试重新规划路径。");
        }

        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderFloat("随机性倍率", ref RandomnessMultiplier, 0f, 1.0f, "%.2f"))
            NotifyModified();

        ImGui.Separator();
        ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1f, 1f), "拟人化移动设置");
        ImGui.TextWrapped("开启后，角色将在寻路网格范围内进行S型随机移动，每个角色的路径轨迹均不相同。");

        if (ImGui.Checkbox("启用拟人化移动 (防检测)", ref EnableHumanLikeMovement))
            NotifyModified();

        using (ImRaii.Disabled(!EnableHumanLikeMovement))
        {
            ImGui.SetNextItemWidth(200);
            if (ImGui.DragFloat("走廊宽度 (米)", ref RandomCorridorWidth, 0.01f, 0.1f, 2.0f, "%.2f"))
                NotifyModified();

            ImGui.SetNextItemWidth(200);
            if (ImGui.DragFloat("路径弯曲频率", ref RandomNoiseScale, 0.01f, 0.1f, 1.0f, "%.2f"))
                NotifyModified();
        }
    }

    public void Save(FileInfo file)
    {
        try
        {
            JObject jContents = new()
            {
                { "Version", _version },
                { "Payload", JObject.FromObject(this) }
            };
            File.WriteAllText(file.FullName, jContents.ToString());
        }
        catch (Exception e)
        {
            Service.Log.Error($"Failed to save config to {file.FullName}: {e}");
        }
    }

    public void Load(FileInfo file)
    {
        try
        {
            var contents = File.ReadAllText(file.FullName);
            var json = JObject.Parse(contents);
            var version = (int?)json["Version"] ?? 0;
            if (json["Payload"] is JObject payload)
            {
                payload = ConvertConfig(payload, version);
                var thisType = GetType();
                foreach (var (f, data) in payload)
                {
                    var thisField = thisType.GetField(f);
                    if (thisField != null)
                    {
                        var value = data?.ToObject(thisField.FieldType);
                        if (value != null)
                        {
                            thisField.SetValue(this, value);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Service.Log.Error($"Failed to load config from {file.FullName}: {e}");
        }
    }

    private static JObject ConvertConfig(JObject payload, int version)
    {
        return payload;
    }
}
