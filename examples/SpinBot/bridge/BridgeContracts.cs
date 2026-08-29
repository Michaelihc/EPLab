using System;
using System.Collections.Generic;
using System.Reflection;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles.FirstPersonControl;
using SpinBot.Config;
using SpinBot.Models;
using SpinBot.Patches;
using SpinBot.Services;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace SpinBot.Models
{
    public enum SpinMode
    {
        Smooth,
        Reverse,
        Jitter,
        Random,
    }

    public enum FireControlMode
    {
        ForceFire,
        CorrectOnFire,
    }

    public readonly struct SpinPose
    {
        public SpinPose(float pitch, float yaw)
        {
            Pitch = pitch;
            Yaw = yaw;
        }

        public float Pitch { get; }
        public float Yaw { get; }
    }

    public readonly struct SpinProfile
    {
        public SpinProfile(SpinMode mode, float speed, float pitch)
        {
            Mode = mode;
            Speed = speed;
            Pitch = pitch;
        }

        public SpinMode Mode { get; }
        public float Speed { get; }
        public float Pitch { get; }
    }
}

namespace SpinBot.Config
{
    /// <summary>Only values consumed by the native firearm/dummy bridge belong here.</summary>
    public sealed class SpinBotConfig
    {
        public bool EnableAutoAimAndFire { get; set; } = true;
        public float AutoAimMaximumDistance { get; set; } = 150f;
        public float AutoFireInterval { get; set; } = 0.12f;
        public float AutoAimScanInterval { get; set; } = 0.12f;
        public bool AutoAimTargetsDummies { get; set; } = true;
        public SpinMode DefaultMode { get; set; } = SpinMode.Jitter;
        public float DefaultRotationSpeed { get; set; } = 720f;
        public float DefaultObserverPitch { get; set; } = -88f;
        public float MaximumRotationSpeed { get; set; } = 2160f;
        public bool DummyAutoJump { get; set; } = true;
        public float DummyJumpInterval { get; set; } = 0.58f;
        public int MaximumDummiesPerCommand { get; set; } = 16;
        public float SpawnDistanceMeters { get; set; } = 4.5f;
        public float DummySpacingMeters { get; set; } = 2.2f;
        public float DemoNtfSpinHz { get; set; } = 5f;
        public float DemoChaosSpinHz { get; set; } = 0.8f;
        public float DemoChaosRevealDelay { get; set; } = 3f;
        public float DemoNtfMovementRadius { get; set; } = 0.7f;
        public float DemoNtfMovementHz { get; set; } = 0.45f;
        public float DemoChaosMovementRadius { get; set; } = 0.65f;
        public float DemoChaosMovementHz { get; set; } = 0.4f;
    }
}

namespace SpinBot.Services
{
    /// <summary>
    /// Contract implemented by the generated Easy-language assembly. Native code never owns game policy.
    /// </summary>
    public abstract class EasySpinHost
    {
        public abstract bool TryGetPose(FpcMouseLook mouseLook, float now, out SpinPose pose);
        public abstract bool EnableFor(Player player, int mode, float speed, float pitch, bool isDummy);
        public abstract bool DisableFor(Player player);
        public abstract bool IsActive(Player player);
        public abstract void UpdateDummyProfiles(int mode, float speed, float pitch);
        public abstract List<Player> GetActiveRealPlayers();
        public abstract bool HasAccess(Player player);
        public abstract float GetMinimumDamage(Player player);
        public abstract int GetFireControlMode(Player player);
        public abstract void RebindPlayer(Player player);

        public SpinBotNativeRuntime StartNative(SpinBotNativeOptions options) => SpinBotNativeRuntime.Start(this, options);

        public void ScheduleRebind(Player player, float delay) => SpinBotNativeTools.ScheduleRebind(this, player, delay);

        public void SaveGeneratedConfig()
        {
            try
            {
                Type? pluginType = GetType().Assembly.GetType(GetType().Namespace + ".__EplabPlugin", throwOnError: false);
                object? plugin = pluginType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
                plugin?.GetType().GetMethod("SaveConfig", BindingFlags.Instance | BindingFlags.Public)?.Invoke(plugin, null);
            }
            catch (Exception exception)
            {
                Logger.Warn($"[SpinBot/EPLab] Could not save generated config: {exception.GetBaseException().Message}");
            }
        }
    }

    /// <summary>Adapter with the exact surface expected by the reused native services.</summary>
    public sealed class SpinStateService
    {
        private readonly EasySpinHost _host;

        public SpinStateService(EasySpinHost host) => _host = host;

        public IReadOnlyList<Player> ActiveRealPlayers => _host.GetActiveRealPlayers();
        public bool EnableFor(Player player, SpinProfile profile, bool isDummy) =>
            _host.EnableFor(player, (int)profile.Mode, profile.Speed, profile.Pitch, isDummy);
        public bool DisableFor(Player player) => _host.DisableFor(player);
        public bool IsActive(Player player) => _host.IsActive(player);
        public bool TryGetPose(FpcMouseLook mouseLook, float now, out SpinPose pose) => _host.TryGetPose(mouseLook, now, out pose);

        public void UpdateDummyProfiles(Func<SpinProfile, SpinProfile> update)
        {
            SpinProfile next = update(new SpinProfile(SpinMode.Smooth, 0f, 0f));
            _host.UpdateDummyProfiles((int)next.Mode, next.Speed, next.Pitch);
        }
    }

    /// <summary>Auto-aim asks Easy code for all access and per-player policy decisions.</summary>
    public sealed class ServerSettingsService
    {
        private readonly EasySpinHost _host;

        public ServerSettingsService(EasySpinHost host) => _host = host;
        public bool HasAccess(Player player) => _host.HasAccess(player);
        public float GetMinimumDamage(Player player) => _host.GetMinimumDamage(player);
        public FireControlMode GetFireControlMode(Player player) => (FireControlMode)_host.GetFireControlMode(player);
    }

    /// <summary>Options passed from readable .易 configuration into the native edge.</summary>
    public sealed class SpinBotNativeOptions
    {
        public bool EnableAutoAimAndFire = true;
        public float AutoAimMaximumDistance = 150f;
        public float AutoFireInterval = 0.12f;
        public float AutoAimScanInterval = 0.12f;
        public bool AutoAimTargetsDummies = true;
        public int DefaultMode = (int)SpinMode.Jitter;
        public float DefaultRotationSpeed = 720f;
        public float DefaultObserverPitch = -88f;
        public float MaximumRotationSpeed = 2160f;
        public bool DummyAutoJump = true;
        public float DummyJumpInterval = 0.58f;
        public int MaximumDummiesPerCommand = 16;
        public float SpawnDistanceMeters = 4.5f;
        public float DummySpacingMeters = 2.2f;
        public float DemoNtfSpinHz = 5f;
        public float DemoChaosSpinHz = 0.8f;
        public float DemoChaosRevealDelay = 3f;
        public float DemoNtfMovementRadius = 0.7f;
        public float DemoNtfMovementHz = 0.45f;
        public float DemoChaosMovementRadius = 0.65f;
        public float DemoChaosMovementHz = 0.4f;
    }

    /// <summary>Owns only Harmony, native firearm requests, and native dummy mechanics.</summary>
    public sealed class SpinBotNativeRuntime : IDisposable
    {
        private readonly SpinStateService _states;
        private readonly DummySpinService _dummies;
        private readonly AutoAimFireService _autoAim;
        private bool _running;

        private SpinBotNativeRuntime(EasySpinHost host, SpinBotNativeOptions options)
        {
            SpinBotConfig config = ToConfig(options);
            _states = new SpinStateService(host);
            ServerSettingsService settings = new ServerSettingsService(host);
            _dummies = new DummySpinService(config, _states);
            _autoAim = new AutoAimFireService(config, _states, settings);
        }

        public static SpinBotNativeRuntime Start(EasySpinHost host, SpinBotNativeOptions options)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (options == null) throw new ArgumentNullException(nameof(options));
            SpinBotNativeRuntime runtime = new SpinBotNativeRuntime(host, options);
            runtime.StartCore();
            return runtime;
        }

        public int DummyCount => _dummies.Count;
        public int Mode => (int)_dummies.Mode;
        public float Speed => _dummies.Speed;
        public float Pitch => _dummies.Pitch;
        public bool JumpEnabled => _dummies.JumpEnabled;
        public float DemoNtfSpinHz => _dummies.DemoNtfSpinHz;
        public float DemoChaosSpinHz => _dummies.DemoChaosSpinHz;
        public long QueuedShots => _autoAim.QueuedShots;
        public long CorrectedRays => _autoAim.CorrectedRays;
        public long NoTargetScans => _autoAim.NoTargetScans;
        public long NoFirearmScans => _autoAim.NoFirearmScans;
        public string LastState => _autoAim.LastState;

        public void SpawnDemo(Player origin) => _dummies.SpawnDemo(origin);
        public void Spawn(Player origin, string kind, int count) =>
            _dummies.Spawn(origin, IsChaos(kind) ? SpinDummyKind.ChaosMinimal : SpinDummyKind.NtfScout, count);
        public int Clear() => _dummies.Clear();
        public void SetSpeed(float value) => _dummies.SetSpeed(value);
        public void SetPitch(float value) => _dummies.SetPitch(value);
        public void SetMode(int value) => _dummies.SetMode((SpinMode)Mathf.Clamp(value, 0, 3));
        public void SetJumpEnabled(bool value) => _dummies.SetJumpEnabled(value);

        public void Dispose()
        {
            if (!_running) return;
            _running = false;
            try { _autoAim.Disable(); } catch (Exception exception) { Logger.Warn($"[SpinBot/EPLab] Auto-fire cleanup: {exception.GetBaseException().Message}"); }
            try { _dummies.Disable(); } catch (Exception exception) { Logger.Warn($"[SpinBot/EPLab] Dummy cleanup: {exception.GetBaseException().Message}"); }
            VisualRotationPatch.Remove();
            SpinBotPlugin.Instance = null;
        }

        private void StartCore()
        {
            _running = true;
            try
            {
                SpinBotPlugin.Instance = new SpinBotPlugin(_autoAim);
                VisualRotationPatch.Apply(_states);
                _dummies.Enable();
                _autoAim.Enable();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static bool IsChaos(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "ci" || normalized == "chaos" || normalized == "revolver";
        }

        private static SpinBotConfig ToConfig(SpinBotNativeOptions value) => new SpinBotConfig
        {
            EnableAutoAimAndFire = value.EnableAutoAimAndFire,
            AutoAimMaximumDistance = value.AutoAimMaximumDistance,
            AutoFireInterval = value.AutoFireInterval,
            AutoAimScanInterval = value.AutoAimScanInterval,
            AutoAimTargetsDummies = value.AutoAimTargetsDummies,
            DefaultMode = (SpinMode)Mathf.Clamp(value.DefaultMode, 0, 3),
            DefaultRotationSpeed = value.DefaultRotationSpeed,
            DefaultObserverPitch = value.DefaultObserverPitch,
            MaximumRotationSpeed = value.MaximumRotationSpeed,
            DummyAutoJump = value.DummyAutoJump,
            DummyJumpInterval = value.DummyJumpInterval,
            MaximumDummiesPerCommand = value.MaximumDummiesPerCommand,
            SpawnDistanceMeters = value.SpawnDistanceMeters,
            DummySpacingMeters = value.DummySpacingMeters,
            DemoNtfSpinHz = value.DemoNtfSpinHz,
            DemoChaosSpinHz = value.DemoChaosSpinHz,
            DemoChaosRevealDelay = value.DemoChaosRevealDelay,
            DemoNtfMovementRadius = value.DemoNtfMovementRadius,
            DemoNtfMovementHz = value.DemoNtfMovementHz,
            DemoChaosMovementRadius = value.DemoChaosMovementRadius,
            DemoChaosMovementHz = value.DemoChaosMovementHz,
        };
    }

    public static class SpinBotNativeTools
    {
        public static string[] Strings(params string[] values) => values ?? Array.Empty<string>();

        public static bool TryGetMouseLook(Player player, out FpcMouseLook mouseLook, out float horizontal)
        {
            mouseLook = null!;
            horizontal = 0f;
            if (player == null || player.IsDestroyed || player.ReferenceHub.roleManager.CurrentRole is not IFpcRole fpc || !fpc.FpcModule.ModuleReady)
                return false;
            mouseLook = fpc.FpcModule.MouseLook;
            horizontal = mouseLook.CurrentHorizontal;
            return true;
        }

        public static void ScheduleRebind(EasySpinHost host, Player player, float delay) =>
            Timing.CallDelayed(Mathf.Max(0f, delay), () => host.RebindPlayer(player));

        public static Player? ResolvePlayer(string value)
        {
            if (int.TryParse(value, out int playerId))
            {
                Player? byId = Player.Get(playerId);
                if (byId != null) return byId;
            }

            return Player.Get(value) ?? Player.GetByNickname(value, requireFullMatch: false);
        }
    }
}

namespace SpinBot
{
    /// <summary>Minimal compatibility anchor used only by the linked correct-on-fire patch.</summary>
    internal sealed class SpinBotPlugin
    {
        internal SpinBotPlugin(AutoAimFireService autoAim) => AutoAim = autoAim;
        internal static SpinBotPlugin? Instance { get; set; }
        internal AutoAimFireService AutoAim { get; }
    }
}
