using System.Text.Json;

namespace VFL.EditorAutomatico;

internal sealed class AppSettings
{
    public decimal SilenceDb { get; set; } = -35;
    public decimal MinimumSilence { get; set; } = 0.70m;
    public decimal RemainingPause { get; set; } = 0.25m;
    public decimal SpeechMargin { get; set; } = 0.08m;
    public string Preset { get; set; } = "Normal";
    public string Quality { get; set; } = QualityPreset.Alta.ToString();

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VFL", "EditorAutomatico", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temporary = SettingsPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, SettingsPath, true);
        }
        catch { }
    }
}
