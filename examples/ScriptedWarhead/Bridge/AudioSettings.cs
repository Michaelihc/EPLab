namespace ScriptedWarheadEPLab.Bridge;

internal sealed class AudioSettings
{
    public bool Enabled { get; set; }

    public string SourcePath { get; set; } = string.Empty;

    public string FfmpegPath { get; set; } = "ffmpeg";

    public float Volume { get; set; } = 1f;

    public int MaximumSeconds { get; set; } = 240;
}
