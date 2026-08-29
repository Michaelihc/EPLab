using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace ScriptedWarheadEPLab.Bridge;

/// <summary>
/// Owns the reversible native facility mutation. The Easy source decides when it runs.
/// </summary>
internal sealed class FacilityOmegaService
{
    private static readonly Color OmegaLightColor = new Color32(0, 200, 255, 255);
    private const string KillReason = "死于Omega核弹引爆";

    private readonly Dictionary<LightsController, LightState> previousLights = new();
    private readonly Dictionary<Door, bool> previousDoorStates = new();

    public void Apply(out int openedDoors, out int untouchedElevatorDoors, out int changedLights)
    {
        previousLights.Clear();
        previousDoorStates.Clear();
        openedDoors = 0;
        untouchedElevatorDoors = 0;

        foreach (Door door in Door.List)
        {
            if (door?.Base == null)
                continue;

            // Elevator motion owns these doors. Omega must never alter or restore them.
            if (door is ElevatorDoor)
            {
                untouchedElevatorDoors++;
                continue;
            }

            try
            {
                previousDoorStates[door] = door.IsOpened;
                door.IsOpened = true;
                openedDoors++;
            }
            catch (Exception exception)
            {
                Logger.Warn($"[ScriptedWarhead/EPLab] Could not force open door {door.DoorName}: {exception.GetBaseException().Message}");
            }
        }

        Warhead.OpenBlastDoors();

        foreach (LightsController lights in LightsController.List)
        {
            previousLights[lights] = new LightState(lights.LightsEnabled, lights.OverrideLightsColor);
            lights.LightsEnabled = true;
            lights.OverrideLightsColor = OmegaLightColor;
        }

        changedLights = previousLights.Count;
    }

    public int DetonateAndKill(out bool nativeDetonationTriggered)
    {
        nativeDetonationTriggered = Warhead.Exists && !Warhead.IsDetonated;
        if (nativeDetonationTriggered)
            Warhead.Detonate();

        int affected = 0;
        foreach (Player player in Player.List.ToArray())
        {
            if (player == null || player.IsDestroyed || !player.IsAlive)
                continue;

            affected++;
            try
            {
                if (player.ReferenceHub.characterClassManager != null)
                    player.IsGodModeEnabled = false;
            }
            catch (Exception exception)
            {
                Logger.Warn($"[ScriptedWarhead/EPLab] Could not clear god mode for player {player.PlayerId}: {exception.GetType().Name}.");
            }

            player.Kill(KillReason);
            if (player.IsAlive)
                player.SetRole(RoleTypeId.Spectator, RoleChangeReason.Died, RoleSpawnFlags.All);
        }

        return affected;
    }

    public void Restore()
    {
        foreach (KeyValuePair<Door, bool> entry in previousDoorStates)
        {
            if (entry.Key?.Base != null)
                entry.Key.IsOpened = entry.Value;
        }
        previousDoorStates.Clear();

        foreach (KeyValuePair<LightsController, LightState> entry in previousLights)
        {
            if (entry.Key?.Base == null)
                continue;
            entry.Key.LightsEnabled = entry.Value.Enabled;
            entry.Key.OverrideLightsColor = entry.Value.Color;
        }
        previousLights.Clear();
    }

    private readonly struct LightState
    {
        public LightState(bool enabled, Color color)
        {
            Enabled = enabled;
            Color = color;
        }

        public bool Enabled { get; }

        public Color Color { get; }
    }
}
