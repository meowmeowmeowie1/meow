using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace MaskedCarnivale.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration cfg;
    private Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("Masked Carnivale")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Size = new Vector2(370, 380);
        SizeCondition = ImGuiCond.Always;
        cfg = plugin.cfg;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
    }
    public override void Draw()
    {
        ShowKofi();

        if (ImGui.BeginChild("Options"))
        {
            bool enable = cfg.enable;
            if (ImGui.Checkbox("Enable", ref enable))
                cfg.enable = enable;
            
            ImGui.SameLine();

            bool showUI = cfg.showUI;
            if (ImGui.Checkbox("Show UI", ref showUI))
                cfg.showUI = showUI;


            ImGui.BeginChild("WindowSettings", new Vector2(350, 120), true);

            ImGui.Text("Window order"); ImGui.SameLine();
            int orderStatus = cfg.orderStatus;
            if (ImGui.RadioButton("##orderStatus_0", ref orderStatus, 0))
                cfg.orderStatus = orderStatus;
            ImGui.SameLine(); ImGui.Text("Normal");

            ImGui.SameLine();
            if (ImGui.RadioButton("##orderStatus_1", ref orderStatus, 1))
                cfg.orderStatus = orderStatus;
            ImGui.SameLine(); ImGui.Text("Bottom");

            //if (ImGui.RadioButton("##orderStatus_2", ref orderStatus, 2))
            //    cfg.orderStatus = orderStatus;
            //ImGui.SameLine(); ImGui.Text("Top Most Window");

            int xPosition = cfg.xPosition;
            ImGui.Text("X Position"); ImGui.SameLine();
            if (ImGui.InputInt("##xPosition", ref xPosition))
                cfg.xPosition = xPosition;

            int yPosition = cfg.yPosition;
            ImGui.Text("Y Position"); ImGui.SameLine();
            if (ImGui.InputInt("##yPosition", ref yPosition))
                cfg.yPosition = yPosition;

            if (ImGui.Button("Save"))
            {
                cfg.doUpdate = false;
                cfg.Save();
                cfg.doUpdate = true;
            }
            ImGui.EndChild();

            // ---- Capture / render-target controls ----
            // If the mirror is black after a game patch, the automatic render index is
            // wrong. Turn on "Override index", press "Dump render targets", open /xllog,
            // find the index whose size matches your resolution and is flagged CANDIDATE,
            // then set it here. Changes apply live.
            ImGui.BeginChild("Capture", new Vector2(350, 150), true);

            bool manualIndex = cfg.manualIndex;
            if (ImGui.Checkbox("Override index (advanced)", ref manualIndex))
            {
                cfg.manualIndex = manualIndex;
                cfg.Save();
            }

            if (cfg.manualIndex)
            {
                int renderIndex = cfg.renderIndex;
                if (ImGui.InputInt("Render index", ref renderIndex))
                {
                    if (renderIndex < 0) renderIndex = 0;
                    if (renderIndex > 511) renderIndex = 511;
                    cfg.renderIndex = renderIndex;
                    cfg.Save();
                }

                // Cycle only through full-resolution targets that have a usable view.
                if (ImGui.Button("< Prev candidate"))
                    plugin.StepCandidate(-1);
                ImGui.SameLine();
                if (ImGui.Button("Next candidate >"))
                    plugin.StepCandidate(1);

                ImGui.TextDisabled(plugin.GetCurrentIndexInfo());
            }
            else
            {
                ImGui.TextDisabled("Using automatic index (toggle Show UI above).");
            }

            if (ImGui.Button("Dump render targets to /xllog"))
                plugin.DumpRenderTargets();

            ImGui.EndChild();
        }
        ImGui.EndChild();
    }
    private void ShowKofi()
    {
        ImGui.BeginChild("Support", new Vector2(350, 50), true);

        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);
        if (ImGui.Button("Support via Ko-fi"))
        {
            Process.Start(new ProcessStartInfo { FileName = "https://ko-fi.com/projectmimer", UseShellExecute = true });
        }
        ImGui.PopStyleColor(3);
        ImGui.EndChild();
    }
}
