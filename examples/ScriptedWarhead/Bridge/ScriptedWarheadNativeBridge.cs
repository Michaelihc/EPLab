using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using CommandSystem;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
using UnityEngine;

namespace ScriptedWarheadEPLab.Bridge;

/// <summary>
/// A small typed doorway from generated Easy-style code to SCP:SL's native systems.
/// It contains mechanics, never the ScriptedWarhead timeline policy.
/// </summary>
public sealed class ScriptedWarheadNativeBridge
{
    private readonly List<CoroutineHandle> scheduled = new();
    private readonly FacilityOmegaService facility = new();
    private RuntimeSettings settings = new();
    private NativeOmegaAudioService? audio;
    private CoroutineHandle? detonationHandle;
    private float detonationRealtime = float.NaN;

    public bool IsRoundInProgress() => Round.IsRoundInProgress;

    public bool HasWarheadPermission(ICommandSender sender)
    {
        Player? player = Player.Get(sender);
        return player == null || sender.CheckPermission(PlayerPermissions.WarheadEvents, out _);
    }

    public void LoadConfiguration(object? source)
    {
        settings = new RuntimeSettings
        {
            IsEnabled = Read(source, "is_enabled", true),
            Language = Read(source, "language", string.Empty),
            AudioEnabled = Read(source, "omega_audio_enabled", true),
            AudioPath = Read(source, "omega_audio_path", string.Empty),
            FfmpegPath = Read(source, "omega_audio_ffmpeg_path", "ffmpeg"),
            AudioVolume = Read(source, "omega_audio_volume", 1f),
            AudioMaximumSeconds = Read(source, "omega_audio_max_seconds", 240),
            SubtitleDelaySeconds = Read(source, "omega_subtitle_delay_seconds", 3.19f),
            SubtitleDurationSeconds = Read(source, "omega_subtitle_duration_seconds", 6f),
            DetonationDelaySeconds = Read(source, "omega_detonation_delay_seconds", 182f),
        };
    }

    public bool IsEnabled() => settings.IsEnabled;

    public bool AudioEnabled() => settings.AudioEnabled;

    public string Language() => settings.Language;

    public float SubtitleDelaySeconds() => SanitizeNonNegative(settings.SubtitleDelaySeconds);

    public float SubtitleDurationSeconds() => Math.Max(1f, settings.SubtitleDurationSeconds);

    public float DetonationDelaySeconds() => SanitizeDetonationDelay(settings.DetonationDelaySeconds, 182f);

    public void EnableConfiguredAudio()
        => EnableAudio(settings.AudioEnabled, settings.AudioPath, settings.FfmpegPath, settings.AudioVolume, settings.AudioMaximumSeconds);

    public void EnableAudio(bool enabled, string sourcePath, string ffmpegPath, float volume, int maximumSeconds)
    {
        audio?.Disable();
        audio = new NativeOmegaAudioService(new AudioSettings
        {
            Enabled = enabled,
            SourcePath = sourcePath ?? string.Empty,
            FfmpegPath = ffmpegPath ?? "ffmpeg",
            Volume = volume,
            MaximumSeconds = maximumSeconds,
        });
        audio.Enable();
    }

    public void PrepareAudio() => audio?.Prepare();

    public void StopAudio() => audio?.Stop();

    public bool TryPlayAudio(Action onStarted, Action<string> onFailed, out string response)
    {
        if (audio == null)
        {
            response = "Audio bridge is not enabled.";
            return false;
        }
        return audio.TryPlay(onStarted, onFailed, out response);
    }

    public void Schedule(float delaySeconds, Action callback)
        => scheduled.Add(Timing.CallDelayed(Math.Max(0f, delaySeconds), callback));

    public void ScheduleDetonation(float delaySeconds, Action callback)
    {
        CancelDetonation();
        float delay = Math.Max(1f, delaySeconds);
        detonationRealtime = Time.realtimeSinceStartup + delay;
        CoroutineHandle handle = Timing.CallDelayed(delay, callback);
        detonationHandle = handle;
        scheduled.Add(handle);
    }

    public int RemainingDetonationSeconds()
    {
        if (float.IsNaN(detonationRealtime))
            return 0;
        return Mathf.Max(0, Mathf.CeilToInt(detonationRealtime - Time.realtimeSinceStartup));
    }

    public void CancelDetonation()
    {
        if (detonationHandle.HasValue)
            Timing.KillCoroutines(detonationHandle.Value);
        detonationHandle = null;
        detonationRealtime = float.NaN;
    }

    public int TryInitiateDeadmanSwitch()
    {
        if (DeadmanSwitch.IsSequenceActive)
            return 0;
        if (!Warhead.Exists || Warhead.IsDetonated)
            return 2;
        DeadmanSwitch.InitiateProtocol();
        return 1;
    }

    public bool IsDeadmanSwitchDetonation()
        => Warhead.ScenarioType == WarheadScenarioType.DeadmanSwitch;

    public void ApplyOmegaFacility(out int openedDoors, out int untouchedElevatorDoors, out int changedLights)
        => facility.Apply(out openedDoors, out untouchedElevatorDoors, out changedLights);

    public void PlayCustomSubtitle(string language, float durationSeconds)
        => OmegaPresentation.PlaySubtitle(IsChinese(language), durationSeconds);

    public void PlayFallbackCassie(string language)
        => OmegaPresentation.PlayFallback(IsChinese(language));

    public int DetonateAndKill(out bool nativeDetonationTriggered)
        => facility.DetonateAndKill(out nativeDetonationTriggered);

    public bool IsChinese(string language)
        => !string.Equals((language ?? string.Empty).Trim(), "en", StringComparison.OrdinalIgnoreCase);

    public bool TextEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public float SanitizeDetonationDelay(float configured, float fallback)
        => float.IsNaN(configured) || float.IsInfinity(configured) ? fallback : Math.Max(1f, configured);

    public float SanitizeNonNegative(float configured)
        => float.IsNaN(configured) || float.IsInfinity(configured) ? 0f : Math.Max(0f, configured);

    public void ResetRound()
    {
        StopAudio();
        foreach (CoroutineHandle handle in scheduled)
            Timing.KillCoroutines(handle);
        scheduled.Clear();
        detonationHandle = null;
        detonationRealtime = float.NaN;
        facility.Restore();
    }

    public void Disable()
    {
        ResetRound();
        audio?.Disable();
        audio = null;
    }

    private static T Read<T>(object? source, string name, T fallback)
    {
        if (source == null)
            return fallback;
        PropertyInfo? property = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        object? value = property?.GetValue(source);
        if (value is T typed)
            return typed;
        try
        {
            return value == null ? fallback : (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    private sealed class RuntimeSettings
    {
        public bool IsEnabled { get; set; } = true;
        public string Language { get; set; } = string.Empty;
        public bool AudioEnabled { get; set; } = true;
        public string AudioPath { get; set; } = string.Empty;
        public string FfmpegPath { get; set; } = "ffmpeg";
        public float AudioVolume { get; set; } = 1f;
        public int AudioMaximumSeconds { get; set; } = 240;
        public float SubtitleDelaySeconds { get; set; } = 3.19f;
        public float SubtitleDurationSeconds { get; set; } = 6f;
        public float DetonationDelaySeconds { get; set; } = 182f;
    }
}
