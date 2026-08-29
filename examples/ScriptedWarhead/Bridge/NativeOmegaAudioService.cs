using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LabApi.Features.Audio;
using LabApi.Features.Wrappers;
using MEC;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace ScriptedWarheadEPLab.Bridge;

/// <summary>
/// The intentionally interpolated part: ffmpeg decoding, thread handoff, and SpeakerToy packets.
/// </summary>
internal sealed class NativeOmegaAudioService
{
    private readonly AudioSettings settings;
    private readonly ConcurrentQueue<Action> mainThreadActions = new();
    private readonly object preparationLock = new();

    private CancellationTokenSource? preparationCancellation;
    private float[]? samples;
    private string preparationStatus = "Audio has not been prepared.";
    private bool preparing;
    private bool enabled;
    private SpeakerToy? speaker;
    private CoroutineHandle startDelay;
    private CoroutineHandle cleanupDelay;

    public NativeOmegaAudioService(AudioSettings settings) => this.settings = settings;

    public void Enable()
    {
        if (enabled)
            return;
        enabled = true;
        StaticUnityMethods.OnUpdate += OnUpdate;
        Prepare();
    }

    public void Disable()
    {
        if (!enabled)
            return;
        enabled = false;
        StaticUnityMethods.OnUpdate -= OnUpdate;
        CancelPreparation();
        Stop();
        lock (preparationLock)
        {
            samples = null;
            preparationStatus = "Audio service is disabled.";
            preparing = false;
        }
        while (mainThreadActions.TryDequeue(out _))
        {
        }
    }

    public void Prepare()
    {
        if (!enabled || !settings.Enabled)
            return;
        lock (preparationLock)
        {
            if (samples != null || preparing)
                return;
            preparing = true;
            preparationStatus = "Audio is being decoded.";
            preparationCancellation = new CancellationTokenSource();
            CancellationTokenSource preparation = preparationCancellation;
            _ = Task.Run(() => PrepareInBackground(preparation));
        }
    }

    public bool TryPlay(Action onStarted, Action<string> onFailed, out string response)
    {
        if (!settings.Enabled)
        {
            response = "Custom Omega audio is disabled.";
            return false;
        }

        float[]? preparedSamples;
        lock (preparationLock)
        {
            preparedSamples = samples;
            response = preparationStatus;
        }
        if (preparedSamples == null || preparedSamples.Length == 0)
            return false;

        Stop();
        try
        {
            SpeakerToy createdSpeaker = CreateGlobalSpeaker();
            speaker = createdSpeaker;
            float duration = preparedSamples.Length / (float)AudioTransmitter.SampleRate;
            startDelay = Timing.CallDelayed(0.5f, () =>
            {
                if (speaker != createdSpeaker || createdSpeaker.IsDestroyed)
                {
                    onFailed("The native Omega SpeakerToy disappeared before playback began.");
                    return;
                }

                try
                {
                    createdSpeaker.Play(preparedSamples, queue: false, loop: false);
                    onStarted();
                    cleanupDelay = Timing.CallDelayed(duration + 1f, () => DestroySpeaker(createdSpeaker));
                    Logger.Info($"[ScriptedWarhead/EPLab] Native Omega SpeakerToy started {duration:0.###} seconds of audio on controller {createdSpeaker.ControllerId}.");
                }
                catch (Exception exception)
                {
                    string reason = exception.GetBaseException().Message;
                    DestroySpeaker(createdSpeaker);
                    onFailed(reason);
                }
            });

            response = $"Native SpeakerToy controller {createdSpeaker.ControllerId} spawned; playback waits 0.5 seconds for client propagation.";
            return true;
        }
        catch (Exception exception)
        {
            Stop();
            response = exception.GetBaseException().Message;
            return false;
        }
    }

    public void Stop()
    {
        if (startDelay.IsRunning)
            Timing.KillCoroutines(startDelay);
        if (cleanupDelay.IsRunning)
            Timing.KillCoroutines(cleanupDelay);
        startDelay = default;
        cleanupDelay = default;

        SpeakerToy? current = speaker;
        speaker = null;
        if (current == null || current.IsDestroyed)
            return;
        try
        {
            current.Stop();
        }
        catch (Exception exception)
        {
            Logger.Warn($"[ScriptedWarhead/EPLab] Could not stop the native Omega speaker: {exception.GetBaseException().Message}");
        }
        try
        {
            current.Destroy();
        }
        catch (Exception exception)
        {
            Logger.Warn($"[ScriptedWarhead/EPLab] Could not destroy the native Omega speaker: {exception.GetBaseException().Message}");
        }
    }

    private void PrepareInBackground(CancellationTokenSource preparation)
    {
        CancellationToken token = preparation.Token;
        try
        {
            float[] decoded = DecodeMp3(token);
            token.ThrowIfCancellationRequested();
            lock (preparationLock)
            {
                samples = decoded;
                preparationStatus = $"Prepared {decoded.Length / (float)AudioTransmitter.SampleRate:0.###} seconds of native PCM audio.";
                preparing = false;
            }
            mainThreadActions.Enqueue(() => Logger.Info($"[ScriptedWarhead/EPLab] {preparationStatus}"));
        }
        catch (OperationCanceledException)
        {
            lock (preparationLock)
            {
                preparing = false;
                preparationStatus = enabled ? "Audio preparation was cancelled." : "Audio service is disabled.";
            }
        }
        catch (Exception exception)
        {
            string reason = exception.GetBaseException().Message;
            lock (preparationLock)
            {
                samples = null;
                preparing = false;
                preparationStatus = reason;
            }
            mainThreadActions.Enqueue(() => Logger.Error($"[ScriptedWarhead/EPLab] Failed to prepare native Omega audio: {reason}"));
        }
        finally
        {
            lock (preparationLock)
            {
                if (ReferenceEquals(preparationCancellation, preparation))
                    preparationCancellation = null;
            }
            preparation.Dispose();
        }
    }

    private float[] DecodeMp3(CancellationToken token)
    {
        string sourcePath = Environment.ExpandEnvironmentVariables((settings.SourcePath ?? string.Empty).Trim());
        if (!Path.IsPathRooted(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Omega audio file was not found at the configured absolute path.", sourcePath);

        int maxSeconds = Math.Max(1, settings.MaximumSeconds);
        string tempPath = Path.Combine(Path.GetTempPath(), "scripted-warhead-eplab-" + Guid.NewGuid().ToString("N") + ".f32le");
        try
        {
            string arguments = string.Join(
                " ",
                "-hide_banner",
                "-loglevel error",
                "-nostdin",
                "-y",
                "-t " + maxSeconds.ToString(CultureInfo.InvariantCulture),
                "-i " + Quote(sourcePath),
                "-vn",
                "-ac 1",
                "-ar " + AudioTransmitter.SampleRate.ToString(CultureInfo.InvariantCulture),
                "-f f32le",
                Quote(tempPath));

            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(settings.FfmpegPath) ? "ffmpeg" : settings.FfmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };

            try
            {
                process.Start();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Could not start ffmpeg at '{process.StartInfo.FileName}': {exception.GetBaseException().Message}", exception);
            }

            using CancellationTokenRegistration registration = token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch
                {
                }
            });

            string stderr = process.StandardError.ReadToEnd();
            int timeoutMilliseconds = checked((maxSeconds + 60) * 1000);
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }
                throw new InvalidOperationException("ffmpeg timed out while decoding the Omega audio file.");
            }

            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("ffmpeg failed: " + Trim(stderr));

            byte[] bytes = File.ReadAllBytes(tempPath);
            if (bytes.Length == 0 || bytes.Length % sizeof(float) != 0)
                throw new InvalidDataException("ffmpeg produced invalid or empty float32 PCM audio.");
            int maximumBytes = checked(AudioTransmitter.SampleRate * maxSeconds * sizeof(float));
            if (bytes.Length > maximumBytes)
                throw new InvalidDataException($"Decoded audio exceeds the configured {maxSeconds}-second limit.");

            float[] result = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            for (int index = 0; index < result.Length; index++)
            {
                float sample = result[index];
                result[index] = float.IsNaN(sample) || float.IsInfinity(sample)
                    ? 0f
                    : Math.Max(-1f, Math.Min(1f, sample));
            }
            return result;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private SpeakerToy CreateGlobalSpeaker()
    {
        SpeakerToy? created = null;
        try
        {
            created = SpeakerToy.Create(Vector3.zero, parent: null, networkSpawn: false);
            created.ControllerId = FindAvailableControllerId();
            created.IsSpatial = false;
            created.Volume = SanitizeVolume(settings.Volume);
            created.MinDistance = 0f;
            created.MaxDistance = float.MaxValue;
            created.Spawn();
            return created;
        }
        catch
        {
            if (created != null && !created.IsDestroyed)
                created.Destroy();
            throw;
        }
    }

    private static byte FindAvailableControllerId()
    {
        HashSet<byte> used = new();
        foreach (SpeakerToy item in SpeakerToy.List)
        {
            if (!item.IsDestroyed)
                used.Add(item.ControllerId);
        }
        for (int candidate = byte.MaxValue; candidate >= byte.MinValue; candidate--)
        {
            if (!used.Contains((byte)candidate))
                return (byte)candidate;
        }
        throw new InvalidOperationException("All native SpeakerToy controller IDs are already in use.");
    }

    private void DestroySpeaker(SpeakerToy expected)
    {
        if (speaker == expected)
            Stop();
    }

    private void CancelPreparation()
    {
        lock (preparationLock)
        {
            try
            {
                preparationCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private void OnUpdate()
    {
        while (mainThreadActions.TryDequeue(out Action action))
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Logger.Error($"[ScriptedWarhead/EPLab] Native audio main-thread action failed: {exception.GetBaseException().Message}");
            }
        }
    }

    private static float SanitizeVolume(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(0f, Math.Min(2f, value));

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string Trim(string output)
    {
        string value = (output ?? string.Empty).Trim();
        if (value.Length > 700)
            value = value.Substring(0, 700) + "...";
        return string.IsNullOrWhiteSpace(value) ? "unknown ffmpeg error" : value;
    }
}
