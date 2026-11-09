using System;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Dsr.P5DeathOfTheHeavens;

public sealed class DsrP5DeathOfTheHeavensSettingsWindow
{
    public DsrP5DeathOfTheHeavensStateOverrides Overrides { get; } = new();

    private PartyRole editingSeat = PartyRole.MainTank;

    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.GuerriqueDirection = null;
            Overrides.EyeDirection = null;
            if (solo)
            {
                Overrides.Doom.Mine = null;
                Overrides.Symbol.Mine = null;
            }
        }
        if (SettingsGrid.Begin("##dsrdoth"))
        {
            DrawDirectionRow("Guerrique (relative N):", "##guerrique", Overrides.GuerriqueDirection, Direction.Cardinal, d => Overrides.GuerriqueDirection = d);
            DrawDirectionRow("Dragon's Eye:", "##eye", Overrides.EyeDirection, Direction.All, d => Overrides.EyeDirection = d);
            if (solo) DrawPlayerRows();
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.Doom.Clear();
            Overrides.Symbol.Clear();
        }
        if (SettingsGrid.Begin("##dsrdothplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##dsrdothseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Dooms set:", Overrides.Doom);
            SettingsGrid.ForcedRecapRow("Symbols set:", Overrides.Symbol);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var doom = Overrides.Doom.Effective(editingSeat);
        SettingsGrid.PlayerRow("doom:");
        if (ImGui.RadioButton("Auto##doom", doom == null)) Overrides.Doom.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##doom", doom == true)) Overrides.Doom.Set(editingSeat, true);
        ImGui.SameLine();
        if (ImGui.RadioButton("No##doom", doom == false)) Overrides.Doom.Set(editingSeat, false);

        var symbol = Overrides.Symbol.Effective(editingSeat);
        SettingsGrid.PlayerRow("symbol:");
        if (ImGui.RadioButton("Auto##symbol", symbol == null)) Overrides.Symbol.Set(editingSeat, null);
        foreach (var s in Enum.GetValues<PlaystationSymbol>())
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{s}##symbol", symbol == s)) Overrides.Symbol.Set(editingSeat, s);
        }
    }

    private static void DrawDirectionRow(string label, string id, Direction? current, System.Collections.Generic.IReadOnlyList<Direction> options, Action<Direction?> set)
    {
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"Auto{id}", current == null)) set(null);
        foreach (var d in options)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}{id}", current == d)) set(d);
        }
    }
}
