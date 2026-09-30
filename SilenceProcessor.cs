using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VFL.EditorAutomatico;

internal sealed partial class SilenceProcessor
{
    private readonly string _ffmpeg;
    private readonly string _ffprobe;

    public SilenceProcessor()
    {
        _ffmpeg = FindTool("ffmpeg") ?? throw new InvalidOperationException(
            "FFmpeg portátil não encontrado. Extraia novamente a pasta completa do programa.");
        _ffprobe = FindTool("ffprobe") ?? throw new InvalidOperationException(
            "FFprobe portátil não encontrado. Extraia novamente a pasta completa do programa.");
    }

    public async Task<SilenceAnalysis> AnalyzeAsync(string inputPath, double silenceDb, double minimumSilence,
        double remainingPause, double speechMargin, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Lendo as informações do vídeo...");
        var video = await ProbeAsync(inputPath, cancellationToken);
        if (!video.HasAudio) throw new InvalidOperationException("Este vídeo não possui uma faixa de áudio para analisar.");

        progress.Report("Escutando o áudio e procurando silêncios...");
        var ci = CultureInfo.InvariantCulture;
        var log = await RunCaptureErrorAsync(_ffmpeg,
            ["-hide_banner", "-nostats", "-i", inputPath, "-vn", "-af",
             $"silencedetect=noise={silenceDb.ToString("0.###", ci)}dB:d={minimumSilence.ToString("0.###", ci)}",
             "-f", "null", "-"], cancellationToken);

        var silences = ParseSilences(log, video.Duration);
        var cuts = BuildCuts(silences, video.Duration, remainingPause, speechMargin);
        var removed = cuts.Sum(range => range.Duration);
        progress.Report(cuts.Count == 0 ? "Nenhum silêncio longo encontrado." : $"Análise pronta: {cuts.Count} cortes sugeridos.");
        return new SilenceAnalysis(video, silences, cuts, removed, Math.Max(0, video.Duration - removed));
    }

    public async Task RenderAsync(string inputPath, string outputPath, SilenceAnalysis analysis,
        QualityPreset quality, IProgress<RenderProgress> progress, CancellationToken cancellationToken)
    {
        if (analysis.Cuts.Count == 0) throw new InvalidOperationException("Não existem cortes para aplicar com esta configuração.");
        if (analysis.FinalDuration < 0.5) throw new InvalidOperationException("Os ajustes removeriam praticamente todo o vídeo. Use um preset mais suave.");

        var ci = CultureInfo.InvariantCulture;
        var filter = BuildCutFilter(analysis.Cuts);
        var filterPath = Path.Combine(Path.GetTempPath(), "VFL-Editor-filter-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(filterPath, filter, new UTF8Encoding(false));
        var (crf, preset) = quality switch
        {
            QualityPreset.Rapida => (24, "veryfast"),
            QualityPreset.Equilibrada => (21, "medium"),
            _ => (18, "slow")
        };

        try
        {
            var psi = new ProcessStartInfo(_ffmpeg)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in new[]
            {
                "-y", "-hide_banner", "-i", inputPath,
                "-/filter_complex", filterPath,
                "-map", "[v]", "-map", "[a]", "-map_metadata", "0", "-map_chapters", "0",
                "-c:v", "libx264", "-preset", preset, "-crf", crf.ToString(ci), "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart",
                "-progress", "pipe:1", "-nostats", outputPath
            }) psi.ArgumentList.Add(argument);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o FFmpeg.");
            using var registration = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            });
            var errorTask = process.StandardError.ReadToEndAsync();
            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!line.StartsWith("out_time_us=", StringComparison.Ordinal) &&
                    !line.StartsWith("out_time_ms=", StringComparison.Ordinal)) continue;
                if (!long.TryParse(line[(line.IndexOf('=') + 1)..], out var microseconds)) continue;
                var seconds = microseconds / 1_000_000d;
                var percent = Math.Clamp(seconds / Math.Max(0.001, analysis.FinalDuration) * 100, 0, 99);
                progress.Report(new RenderProgress(percent, $"Gerando vídeo... {percent:0}%"));
            }
            await process.WaitForExitAsync(cancellationToken);
            var error = await errorTask;
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Falha ao gerar o vídeo.\n" + error[^Math.Min(2500, error.Length)..]);
            progress.Report(new RenderProgress(100, "Vídeo concluído."));
        }
        catch
        {
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            throw;
        }
        finally
        {
            try { File.Delete(filterPath); } catch { }
        }
    }

    private static string BuildCutFilter(IReadOnlyList<TimeRange> cuts)
    {
        const int rangesPerFilter = 20;
        var ci = CultureInfo.InvariantCulture;
        var video = new StringBuilder();
        var audio = new StringBuilder();
        var videoInput = "[0:v]";
        var audioInput = "[0:a]";
        var chunkIndex = 0;
        foreach (var chunk in cuts.Chunk(rangesPerFilter))
        {
            var removal = string.Join("+", chunk.Select(range =>
                $"between(t,{range.Start.ToString("0.######", ci)},{range.End.ToString("0.######", ci)})"));
            var videoOutput = $"[vcut{chunkIndex}]";
            var audioOutput = $"[acut{chunkIndex}]";
            video.Append(videoInput).Append("select='not(").Append(removal).Append(")'").Append(videoOutput).Append(';');
            audio.Append(audioInput).Append("aselect='not(").Append(removal).Append(")'").Append(audioOutput).Append(';');
            videoInput = videoOutput;
            audioInput = audioOutput;
            chunkIndex++;
        }
        video.Append(videoInput).Append("setpts=N/FRAME_RATE/TB[v];");
        audio.Append(audioInput).Append("asetpts=N/SR/TB[a]");
        return video.Append(audio).ToString();
    }

    private async Task<VideoInfo> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        var output = await RunCaptureOutputAsync(_ffprobe,
            ["-v", "error", "-show_entries", "format=duration:stream=codec_type", "-of", "json", path], cancellationToken);
        using var document = JsonDocument.Parse(output);
        var durationText = document.RootElement.GetProperty("format").GetProperty("duration").GetString() ?? "0";
        var duration = double.Parse(durationText, CultureInfo.InvariantCulture);
        var hasAudio = document.RootElement.GetProperty("streams").EnumerateArray()
            .Any(stream => stream.GetProperty("codec_type").GetString() == "audio");
        return new VideoInfo(duration, hasAudio);
    }

    private static IReadOnlyList<TimeRange> ParseSilences(string log, double duration)
    {
        var result = new List<TimeRange>();
        double? start = null;
        foreach (var line in log.Split('\n'))
        {
            var startMatch = SilenceStartRegex().Match(line);
            if (startMatch.Success && double.TryParse(startMatch.Groups[1].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var startValue))
            {
                start = Math.Clamp(startValue, 0, duration);
                continue;
            }
            var endMatch = SilenceEndRegex().Match(line);
            if (!endMatch.Success || !double.TryParse(endMatch.Groups[1].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var endValue)) continue;
            var rangeStart = start ?? 0;
            var rangeEnd = Math.Clamp(endValue, rangeStart, duration);
            if (rangeEnd > rangeStart) result.Add(new TimeRange(rangeStart, rangeEnd));
            start = null;
        }
        if (start is not null && duration > start.Value) result.Add(new TimeRange(start.Value, duration));
        return result;
    }

    private static IReadOnlyList<TimeRange> BuildCuts(IEnumerable<TimeRange> silences, double duration,
        double remainingPause, double speechMargin)
    {
        var preservedAtEachSide = Math.Max(0, remainingPause) / 2 + Math.Max(0, speechMargin);
        var raw = silences
            .Select(range => new TimeRange(
                Math.Clamp(range.Start + preservedAtEachSide, 0, duration),
                Math.Clamp(range.End - preservedAtEachSide, 0, duration)))
            .Where(range => range.Duration >= 0.04)
            .OrderBy(range => range.Start)
            .ToArray();
        var merged = new List<TimeRange>();
        foreach (var range in raw)
        {
            if (merged.Count == 0 || range.Start > merged[^1].End + 0.001) merged.Add(range);
            else merged[^1] = new TimeRange(merged[^1].Start, Math.Max(merged[^1].End, range.End));
        }
        return merged;
    }

    private static async Task<string> RunCaptureErrorAsync(string executable, IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var psi = CreateCapture(executable, arguments);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o FFmpeg.");
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException(error[^Math.Min(2500, error.Length)..]);
        return error;
    }

    private static async Task<string> RunCaptureOutputAsync(string executable, IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var psi = CreateCapture(executable, arguments);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o FFprobe.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return output;
    }

    private static ProcessStartInfo CreateCapture(string executable, IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        return psi;
    }

    private static string? FindTool(string name)
    {
        foreach (var path in new[] { Path.Combine(AppContext.BaseDirectory, "Tools", name + ".exe"), name + ".exe" })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(path, "-version") { UseShellExecute = false, CreateNoWindow = true });
                if (process is null) continue;
                process.WaitForExit(2000);
                if (process.ExitCode == 0) return path;
            }
            catch { }
        }
        return null;
    }

    [GeneratedRegex(@"silence_start:\s*([-+0-9.eE]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SilenceStartRegex();

    [GeneratedRegex(@"silence_end:\s*([-+0-9.eE]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SilenceEndRegex();
}
