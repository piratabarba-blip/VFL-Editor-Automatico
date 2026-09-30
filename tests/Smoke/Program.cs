using System.Diagnostics;
using VFL.EditorAutomatico;

var ffmpeg = args[0];
var testRoot = Path.Combine(Path.GetTempPath(), "VFL-EditorAutomatico-Smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
var input = Path.Combine(testRoot, "entrada.mp4");
var output = Path.Combine(testRoot, "saida.mp4");
var stressOutput = Path.Combine(testRoot, "saida-150-cortes.mp4");
try
{
    await RunAsync(ffmpeg,
    [
        "-y", "-hide_banner", "-loglevel", "error",
        "-f", "lavfi", "-i", "testsrc2=duration=6:size=640x360:rate=24",
        "-f", "lavfi", "-i", "sine=frequency=440:duration=6:sample_rate=48000",
        "-filter_complex", "[1:a]volume=0:enable='between(t,1,2)+between(t,3,4)+between(t,5,6)'[a]",
        "-map", "0:v", "-map", "[a]", "-c:v", "libx264", "-preset", "ultrafast",
        "-c:a", "aac", "-shortest", input
    ]);

    var processor = new SilenceProcessor();
    var analysis = await processor.AnalyzeAsync(input, -35, 0.70, 0.25, 0.08,
        new Progress<string>(_ => { }), CancellationToken.None);
    Console.WriteLine($"detected={analysis.Silences.Count}; cuts={analysis.Cuts.Count}; removed={analysis.RemovedDuration:F3}; final={analysis.FinalDuration:F3}");
    if (analysis.Cuts.Count != 3) throw new Exception($"Esperados 3 cortes, encontrados {analysis.Cuts.Count}.");

    await processor.RenderAsync(input, output, analysis, QualityPreset.Rapida,
        new Progress<RenderProgress>(_ => { }), CancellationToken.None);
    if (!File.Exists(output) || new FileInfo(output).Length == 0) throw new Exception("O MP4 final não foi criado.");

    var outputAnalysis = await processor.AnalyzeAsync(output, -35, 0.70, 0.25, 0.08,
        new Progress<string>(_ => { }), CancellationToken.None);
    Console.WriteLine($"output_duration={outputAnalysis.Video.Duration:F3}; output_bytes={new FileInfo(output).Length}");
    if (outputAnalysis.Video.Duration >= 5.5 || outputAnalysis.Video.Duration <= 3.5)
        throw new Exception("A duração do MP4 final ficou fora do intervalo esperado.");

    var stressCuts = Enumerable.Range(0, 150)
        .Select(index => new TimeRange(0.10 + index * 0.035, 0.11 + index * 0.035))
        .ToArray();
    var stressRemoved = stressCuts.Sum(range => range.Duration);
    var stressAnalysis = new SilenceAnalysis(analysis.Video, stressCuts, stressCuts,
        stressRemoved, analysis.Video.Duration - stressRemoved);
    await processor.RenderAsync(input, stressOutput, stressAnalysis, QualityPreset.Rapida,
        new Progress<RenderProgress>(_ => { }), CancellationToken.None);
    if (!File.Exists(stressOutput) || new FileInfo(stressOutput).Length == 0)
        throw new Exception("O teste com 150 cortes não criou o MP4.");
    Console.WriteLine($"stress_cuts={stressCuts.Length}; stress_bytes={new FileInfo(stressOutput).Length}");

    if (args.Length > 1 && File.Exists(args[1]))
    {
        var realInput = args[1];
        var realOutput = Path.Combine(testRoot, "saida-real-cancelada.mp4");
        var realAnalysis = await processor.AnalyzeAsync(realInput, -35, 0.70, 0.25, 0.08,
            new Progress<string>(_ => { }), CancellationToken.None);
        Console.WriteLine($"real_duration={realAnalysis.Video.Duration:F3}; real_cuts={realAnalysis.Cuts.Count}");
        using var realCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            await processor.RenderAsync(realInput, realOutput, realAnalysis, QualityPreset.Rapida,
                new Progress<RenderProgress>(_ => { }), realCancellation.Token);
        }
        catch (OperationCanceledException) { }
        if (File.Exists(realOutput)) throw new Exception("O arquivo parcial do teste real não foi removido.");
        Console.WriteLine("real_startup=PASS");
    }
    Console.WriteLine("PASS");
}
finally
{
    try { Directory.Delete(testRoot, true); } catch { }
}

static async Task RunAsync(string executable, IEnumerable<string> arguments)
{
    var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    using var process = Process.Start(info) ?? throw new Exception("Falha ao iniciar FFmpeg de teste.");
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new Exception("FFmpeg de teste retornou erro.");
}
