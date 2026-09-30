namespace VFL.EditorAutomatico;

internal sealed record VideoInfo(double Duration, bool HasAudio);

internal sealed record TimeRange(double Start, double End)
{
    public double Duration => Math.Max(0, End - Start);
}

internal sealed record SilenceAnalysis(
    VideoInfo Video,
    IReadOnlyList<TimeRange> Silences,
    IReadOnlyList<TimeRange> Cuts,
    double RemovedDuration,
    double FinalDuration);

internal sealed record RenderProgress(double Percent, string Message);

internal enum QualityPreset
{
    Rapida,
    Equilibrada,
    Alta
}
