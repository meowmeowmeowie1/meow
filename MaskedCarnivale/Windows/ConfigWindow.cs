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
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
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

        {
            bool enable = cfg.enable;
            if (ImGui.Checkbox("Enable", ref enable))
                cfg.enable = enable;

            ImGui.SameLine();

            bool showUI = cfg.showUI;
            if (ImGui.Checkbox("Show UI", ref showUI))
            {
                cfg.showUI = showUI;
                cfg.Save();
            }
            ImGui.TextDisabled(cfg.showUI
                ? "ON: game + HUD (backbuffer capture)."
                : "OFF: clean scene, no HUD.");


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

            // ---- Advanced capture tuning (collapsed by default) ----
            // Only needed if the mirror breaks after a game patch: turn on "Override index",
            // use "Dump render targets" (writes to /xllog) or the candidate cycler to find a
            // full-res scene buffer, and set it. Applies only to the no-HUD (Show UI OFF) path.
            if (ImGui.CollapsingHeader("Advanced (capture tuning)"))
            {
                ImGui.TextDisabled("No-HUD / Show UI OFF only. Ignored when Show UI is ON.");

                bool manualIndex = cfg.manualIndex;
                if (ImGui.Checkbox("Override index", ref manualIndex))
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

                if (ImGui.Button("Dump render targets to /xllog"))
                    plugin.DumpRenderTargets();
            }
        }
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
