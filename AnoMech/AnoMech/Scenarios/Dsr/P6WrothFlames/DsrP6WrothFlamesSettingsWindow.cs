using System;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Dsr.P6WrothFlames;

public sealed class DsrP6WrothFlamesSettingsWindow
{
    public DsrP6WrothFlamesStateOverrides Overrides { get; } = new();

    private PartyRole editingSeat = PartyRole.MainTank;

    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.HraesvelgrLane = null;
            Overrides.HraesvelgrFromNorth = null;
            Overrides.SecondOrbSet = null;
            Overrides.HotWing = null;
            if (solo)
            {
                Overrides.Role.Mine = null;
                Overrides.AkhMornTarget.Mine = null;
            }
        }
        if (SettingsGrid.Begin("##dsrwroth"))
        {
            SettingsGrid.Row("Hraesvelgr dives from:");
            if (ImGui.RadioButton("Auto##hside", Overrides.HraesvelgrFromNorth == null)) Overrides.HraesvelgrFromNorth = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("North##hside", Overrides.HraesvelgrFromNorth == true)) Overrides.HraesvelgrFromNorth = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("South##hside", Overrides.HraesvelgrFromNorth == false)) Overrides.HraesvelgrFromNorth = false;

            SettingsGrid.Row("Hraesvelgr lane:");
            if (ImGui.RadioButton("Auto##hlane", Overrides.HraesvelgrLane == null)) Overrides.HraesvelgrLane = null;
            foreach (var lane in Enum.GetValues<DiveLane>())
            {
                ImGui.SameLine();
                if (ImGui.RadioButton($"{lane}##hlane", Overrides.HraesvelgrLane == lane)) Overrides.HraesvelgrLane = lane;
            }

            SettingsGrid.Row("Second orb set:");
            if (ImGui.RadioButton("Auto##orbs", Overrides.SecondOrbSet == null)) Overrides.SecondOrbSet = null;
            foreach (var d in Direction.Intercardinal)
            {
                ImGui.SameLine();
                if (ImGui.RadioButton($"{d.Name()}##orbs", Overrides.SecondOrbSet == d)) Overrides.SecondOrbSet = d;
            }

            SettingsGrid.Row("Nidhogg cleave:");
            if (ImGui.RadioButton("Auto##hot", Overrides.HotWing == null)) Overrides.HotWing = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("Hot Wing##hot", Overrides.HotWing == true)) Overrides.HotWing = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("Hot Tail##hot", Overrides.HotWing == false)) Overrides.HotWing = false;

            if (solo) DrawPlayerRows();
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.Role.Clear();
            Overrides.AkhMornTarget.Clear();
        }
        if (SettingsGrid.Begin("##dsrwrothplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##dsrwrothseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Debuffs set:", Overrides.Role);
            SettingsGrid.ForcedRecapRow("Akh Morn set:", Overrides.AkhMornTarget);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var role = Overrides.Role.Effective(editingSeat);
        SettingsGrid.PlayerRow("debuff:");
        if (ImGui.RadioButton("Auto##wrole", role == null)) Overrides.Role.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Spreading##wrole", role == WrothRole.Spread)) Overrides.Role.Set(editingSeat, WrothRole.Spread);
        ImGui.SameLine();
        if (ImGui.RadioButton("Entangled##wrole", role == WrothRole.Stack)) Overrides.Role.Set(editingSeat, WrothRole.Stack);
        ImGui.SameLine();
        if (ImGui.RadioButton("None##wrole", role == WrothRole.None)) Overrides.Role.Set(editingSeat, WrothRole.None);

        var target = Overrides.AkhMornTarget.Effective(editingSeat);
        SettingsGrid.PlayerRow("akh morn target:");
        if (ImGui.RadioButton("Auto##akh", target == null)) Overrides.AkhMornTarget.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##akh", target == true)) Overrides.AkhMornTarget.Set(editingSeat, true);
        ImGui.SameLine();
        if (ImGui.RadioButton("No##akh", target == false)) Overrides.AkhMornTarget.Set(editingSeat, false);
    }
}
