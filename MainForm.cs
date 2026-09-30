using System.Diagnostics;

namespace VFL.EditorAutomatico;

internal sealed class MainForm : Form
{
    private readonly TextBox _input = new();
    private readonly TextBox _output = new();
    private readonly ComboBox _preset = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _silenceDb = Number(-80, -5, -35, 1, 0);
    private readonly NumericUpDown _minimumSilence = Number(0.10m, 10, 0.70m, 0.05m, 2);
    private readonly NumericUpDown _remainingPause = Number(0, 3, 0.25m, 0.05m, 2);
    private readonly NumericUpDown _speechMargin = Number(0, 1, 0.08m, 0.01m, 2);
    private readonly ComboBox _quality = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _analyze = new() { Text = "ANALISAR SILÊNCIOS" };
    private readonly Button _render = new() { Text = "GERAR VÍDEO", Tag = "primary", Enabled = false };
    private readonly Button _clear = new() { Text = "LIMPAR / PRÓXIMO" };
    private readonly Button _cancel = new() { Text = "CANCELAR", Enabled = false };
    private readonly Label _status = new() { Text = "Selecione um vídeo para começar.", ForeColor = Theme.Muted, AutoSize = true };
    private readonly Label _headline = new()
    {
        Text = "AGUARDANDO VÍDEO", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 16)
    };
    private readonly Label _originalValue = SummaryValue("00:00.000");
    private readonly Label _removedValue = SummaryValue("00:00.000");
    private readonly Label _finalValue = SummaryValue("00:00.000");
    private readonly Label _cutsValue = SummaryValue("0");
    private readonly CutTimeline _timeline = new() { Dock = DockStyle.Fill };
    private readonly ModernProgressBar _progress = new() { Dock = DockStyle.Fill };
    private SilenceAnalysis? _analysis;
    private CancellationTokenSource? _operationCts;
    private bool _loadingSettings;
    private bool _applyingPreset;

    public MainForm()
    {
        Text = "VFL Editor Automático";
        Size = new Size(1120, 820);
        MinimumSize = new Size(1060, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Background;
        Theme.ApplyIcon(this);
        BuildUi();
        Theme.Apply(this);
        LoadSettings();
        WireEvents();
        Shown += (_, _) => FitToScreen();
        FormClosed += (_, _) => { SaveSettings(); _operationCts?.Cancel(); };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Tag = "background" };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(28, 14, 28, 12), Tag = "header" };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        header.Controls.Add(new Label
        {
            Text = "VFL", Dock = DockStyle.Fill, BackColor = Theme.Primary, Tag = "accent",
            TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Black", 16)
        }, 0, 0);
        var title = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(18, 0, 0, 0), Tag = "header" };
        title.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        title.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        title.Controls.Add(new Label
        {
            Text = "VFL Editor Automático", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 19),
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
        }, 0, 0);
        title.Controls.Add(new Label
        {
            Text = "Corte pausas automaticamente e preserve o ritmo natural da fala.", Dock = DockStyle.Fill,
            ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
        }, 0, 1);
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(new Label
        {
            Text = "CORTA SILÊNCIO  |  v1.0", Dock = DockStyle.Fill,
            ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.5f)
        }, 2, 0);
        root.Controls.Add(header, 0, 0);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(28, 20, 28, 8), Tag = "background" };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        root.Controls.Add(content, 0, 1);

        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 10, Padding = new Padding(22, 16, 22, 16),
            Tag = "rounded-surface", Margin = new Padding(0, 0, 12, 0), AutoScroll = true,
            AutoScrollMinSize = new Size(0, 540)
        };
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        for (var index = 0; index < 6; index++) settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        settings.Controls.Add(new Label { Text = "CONFIGURAÇÃO DO CORTE", ForeColor = Theme.Primary, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9) }, 0, 0);
        settings.Controls.Add(FileRow("Vídeo de entrada", _input, false), 0, 1);
        settings.Controls.Add(FileRow("Salvar resultado em", _output, true), 0, 2);
        settings.Controls.Add(ComboRow("Modo de corte", _preset, "Suave, normal ou agressivo"), 0, 3);
        settings.Controls.Add(NumberRow("Silêncio", _silenceDb, "dB  •  menor é mais conservador"), 0, 4);
        settings.Controls.Add(NumberRow("Pausa mínima", _minimumSilence, "segundos para considerar corte"), 0, 5);
        settings.Controls.Add(NumberRow("Pausa restante", _remainingPause, "segundos mantidos entre falas"), 0, 6);
        settings.Controls.Add(NumberRow("Margem da fala", _speechMargin, "proteção antes e depois"), 0, 7);
        settings.Controls.Add(ComboRow("Qualidade", _quality, "qualidade do MP4 final"), 0, 8);
        settings.Controls.Add(new Label
        {
            Text = "O programa remove somente o centro das pausas longas. Uma pequena respiração permanece para evitar cortes secos.",
            Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f), Padding = new Padding(0, 8, 0, 0)
        }, 0, 9);
        content.Controls.Add(settings, 0, 0);

        var result = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 5, Padding = new Padding(18, 16, 18, 16),
            Tag = "rounded-surface", Margin = new Padding(12, 0, 0, 0)
        };
        result.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        result.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        result.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        result.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        result.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        result.Controls.Add(new Label { Text = "RESULTADO DA ANÁLISE", Dock = DockStyle.Fill, ForeColor = Theme.Primary, Font = new Font("Segoe UI Semibold", 9) }, 0, 0);
        result.Controls.Add(_headline, 0, 1);
        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Tag = "surface", Padding = new Padding(10) };
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metrics.Controls.Add(Metric("DURAÇÃO ORIGINAL", _originalValue), 0, 0);
        metrics.Controls.Add(Metric("TEMPO REMOVIDO", _removedValue), 1, 0);
        metrics.Controls.Add(Metric("DURAÇÃO ESTIMADA", _finalValue), 0, 1);
        metrics.Controls.Add(Metric("CORTES SUGERIDOS", _cutsValue), 1, 1);
        result.Controls.Add(metrics, 0, 2);
        result.Controls.Add(_timeline, 0, 3);
        result.Controls.Add(new Label
        {
            Text = "Primeiro analise o vídeo. Depois confira a faixa vermelha na linha do tempo e gere uma cópia editada. O arquivo original nunca será modificado.",
            Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.TopCenter,
            Font = new Font("Segoe UI", 9), Padding = new Padding(30, 16, 30, 0)
        }, 0, 4);
        content.Controls.Add(result, 1, 0);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 2, Padding = new Padding(28, 6, 28, 8), Tag = "background" };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        foreach (var button in new[] { _analyze, _render, _clear, _cancel }) { button.Dock = DockStyle.Fill; button.Margin = new Padding(0, 2, 10, 2); }
        footer.Controls.Add(_analyze, 0, 0); footer.Controls.Add(_render, 1, 0); footer.Controls.Add(_clear, 2, 0); footer.Controls.Add(_cancel, 3, 0);
        _status.Dock = DockStyle.Fill; _status.TextAlign = ContentAlignment.MiddleLeft; footer.Controls.Add(_status, 4, 0);
        footer.Controls.Add(new Label { Text = "ORIGINAL PRESERVADO", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8) }, 5, 0);
        footer.Controls.Add(_progress, 0, 1); footer.SetColumnSpan(_progress, 6);
        root.Controls.Add(footer, 0, 2);
    }

    private void WireEvents()
    {
        _preset.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingSettings || _applyingPreset || _preset.SelectedItem?.ToString() == "Personalizado") return;
            ApplyPreset(_preset.SelectedItem?.ToString() ?? "Normal");
            InvalidateAnalysis();
            SaveSettings();
        };
        foreach (var number in new[] { _silenceDb, _minimumSilence, _remainingPause, _speechMargin })
            number.ValueChanged += (_, _) =>
            {
                if (_loadingSettings || _applyingPreset) return;
                _applyingPreset = true; _preset.SelectedItem = "Personalizado"; _applyingPreset = false;
                InvalidateAnalysis(); SaveSettings();
            };
        _quality.SelectedIndexChanged += (_, _) => { if (!_loadingSettings) SaveSettings(); };
        _analyze.Click += async (_, _) => await AnalyzeAsync();
        _render.Click += async (_, _) => await RenderAsync();
        _clear.Click += (_, _) => Reset();
        _cancel.Click += (_, _) => _operationCts?.Cancel();
    }

    private Control FileRow(string label, TextBox box, bool save)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Tag = "surface" };
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); row.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        row.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = Theme.Muted }, 0, 0);
        box.Dock = DockStyle.Fill; box.Margin = new Padding(0, 4, 10, 2); row.Controls.Add(box, 0, 1);
        var browse = new Button { Text = "PROCURAR", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 2) };
        browse.Click += async (_, _) =>
        {
            if (save)
            {
                using var dialog = new SaveFileDialog { Filter = "Vídeo MP4|*.mp4", DefaultExt = "mp4", AddExtension = true };
                if (dialog.ShowDialog(this) == DialogResult.OK) _output.Text = dialog.FileName;
                return;
            }
            using var open = new OpenFileDialog { Filter = "Vídeos|*.mp4;*.mov;*.mkv;*.webm;*.avi|Todos os arquivos|*.*" };
            if (open.ShowDialog(this) != DialogResult.OK) return;
            Reset(false);
            _input.Text = open.FileName;
            _output.Text = AvailableOutput(Path.Combine(Path.GetDirectoryName(open.FileName)!,
                Path.GetFileNameWithoutExtension(open.FileName) + "_SEM_SILENCIOS.mp4"));
            await AnalyzeAsync();
        };
        row.Controls.Add(browse, 1, 1);
        return row;
    }

    private static Control NumberRow(string label, NumericUpDown number, string hint)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Tag = "surface" };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = label.ToUpperInvariant(), Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        number.Dock = DockStyle.Fill; number.Margin = new Padding(0, 11, 10, 11); row.Controls.Add(number, 1, 0);
        row.Controls.Add(new Label { Text = hint, Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8) }, 2, 0);
        return row;
    }

    private static Control ComboRow(string label, ComboBox combo, string hint)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Tag = "surface" };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = label.ToUpperInvariant(), Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        combo.Dock = DockStyle.Fill; combo.Margin = new Padding(0, 9, 10, 9); row.Controls.Add(combo, 1, 0);
        row.Controls.Add(new Label { Text = hint, Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8) }, 2, 0);
        return row;
    }

    private static Control Metric(string title, Label value)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Tag = "surface", Margin = new Padding(5) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8) }, 0, 0);
        panel.Controls.Add(value, 0, 1);
        return panel;
    }

    private async Task AnalyzeAsync()
    {
        if (!File.Exists(_input.Text)) { AppDialog.ShowMessage(this, "Vídeo necessário", "Selecione um vídeo válido para analisar."); return; }
        _operationCts = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            var progress = new Progress<string>(message => _status.Text = message);
            _analysis = await new SilenceProcessor().AnalyzeAsync(_input.Text, (double)_silenceDb.Value,
                (double)_minimumSilence.Value, (double)_remainingPause.Value, (double)_speechMargin.Value,
                progress, _operationCts.Token);
            ShowAnalysis(_analysis);
        }
        catch (OperationCanceledException) { _status.Text = "Análise cancelada."; }
        catch (Exception exception) { _status.Text = "Falha na análise."; AppDialog.ShowMessage(this, "Erro na análise", exception.Message); }
        finally { SetBusy(false); _operationCts.Dispose(); _operationCts = null; }
    }

    private async Task RenderAsync()
    {
        if (_analysis is null) { AppDialog.ShowMessage(this, "Análise necessária", "Analise o vídeo antes de gerar o resultado."); return; }
        if (string.IsNullOrWhiteSpace(_output.Text)) { AppDialog.ShowMessage(this, "Destino necessário", "Escolha onde salvar o MP4."); return; }
        if (_analysis.Cuts.Count == 0) { AppDialog.ShowMessage(this, "Nenhum corte", "Não foram encontrados silêncios para remover com esta configuração."); return; }
        _output.Text = AvailableOutput(_output.Text);
        _operationCts = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            var quality = Enum.Parse<QualityPreset>(_quality.SelectedItem?.ToString() ?? QualityPreset.Alta.ToString());
            var progress = new Progress<RenderProgress>(item => { _progress.Value = (int)item.Percent; _status.Text = item.Message; });
            await new SilenceProcessor().RenderAsync(_input.Text, _output.Text, _analysis, quality, progress, _operationCts.Token);
            if (AppDialog.Confirm(this, "Vídeo concluído", $"O vídeo foi gerado com {_analysis.Cuts.Count} cortes.\n\nDeseja abrir a pasta?"))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_output.Text}\"") { UseShellExecute = true });
        }
        catch (OperationCanceledException) { _status.Text = "Processamento cancelado."; }
        catch (Exception exception) { _status.Text = "Falha ao gerar o vídeo."; AppDialog.ShowMessage(this, "Erro na conversão", exception.Message); }
        finally { SetBusy(false); _operationCts.Dispose(); _operationCts = null; }
    }

    private void ShowAnalysis(SilenceAnalysis analysis)
    {
        _headline.Text = analysis.Cuts.Count == 0 ? "NENHUM CORTE NECESSÁRIO" : $"{analysis.Cuts.Count} CORTES ENCONTRADOS";
        _headline.ForeColor = analysis.Cuts.Count == 0 ? Theme.Muted : Theme.Primary;
        _originalValue.Text = FormatTime(analysis.Video.Duration);
        _removedValue.Text = FormatTime(analysis.RemovedDuration);
        _finalValue.Text = FormatTime(analysis.FinalDuration);
        _cutsValue.Text = analysis.Cuts.Count.ToString();
        _timeline.Analysis = analysis;
        _render.Enabled = analysis.Cuts.Count > 0;
        _status.Text = analysis.Cuts.Count == 0
            ? "Nenhum silêncio longo encontrado. Tente o modo agressivo se necessário."
            : $"Pronto: serão removidos {FormatTime(analysis.RemovedDuration)}.";
    }

    private void InvalidateAnalysis()
    {
        if (_analysis is null) return;
        _analysis = null;
        _render.Enabled = false;
        _timeline.Analysis = null;
        _headline.Text = "CONFIGURAÇÃO ALTERADA";
        _headline.ForeColor = Theme.Muted;
        _status.Text = "Clique em Analisar silêncios para atualizar os cortes.";
    }

    private void Reset(bool clearPaths = true)
    {
        _operationCts?.Cancel();
        _analysis = null;
        _timeline.Analysis = null;
        _headline.Text = "AGUARDANDO VÍDEO";
        _headline.ForeColor = Theme.Muted;
        _originalValue.Text = _removedValue.Text = _finalValue.Text = "00:00.000";
        _cutsValue.Text = "0";
        _progress.Value = 0;
        _render.Enabled = false;
        if (clearPaths) { _input.Clear(); _output.Clear(); }
        _status.Text = clearPaths ? "Pronto para o próximo vídeo. Seus ajustes foram mantidos." : "Preparando o novo vídeo...";
    }

    private void SetBusy(bool busy)
    {
        _analyze.Enabled = !busy;
        _render.Enabled = !busy && _analysis is not null && _analysis.Cuts.Count > 0;
        _clear.Enabled = !busy;
        _cancel.Enabled = busy;
        _preset.Enabled = !busy; _silenceDb.Enabled = !busy; _minimumSilence.Enabled = !busy;
        _remainingPause.Enabled = !busy; _speechMargin.Enabled = !busy; _quality.Enabled = !busy;
    }

    private void ApplyPreset(string preset)
    {
        _applyingPreset = true;
        try
        {
            switch (preset)
            {
                case "Suave":
                    _silenceDb.Value = -42; _minimumSilence.Value = 1.00m; _remainingPause.Value = 0.45m; _speechMargin.Value = 0.12m;
                    break;
                case "Agressivo":
                    _silenceDb.Value = -28; _minimumSilence.Value = 0.40m; _remainingPause.Value = 0.12m; _speechMargin.Value = 0.04m;
                    break;
                default:
                    _silenceDb.Value = -35; _minimumSilence.Value = 0.70m; _remainingPause.Value = 0.25m; _speechMargin.Value = 0.08m;
                    break;
            }
        }
        finally { _applyingPreset = false; }
    }

    private void LoadSettings()
    {
        _loadingSettings = true;
        _preset.Items.AddRange(["Suave", "Normal", "Agressivo", "Personalizado"]);
        _quality.Items.AddRange(Enum.GetNames<QualityPreset>());
        var settings = AppSettings.Load();
        _silenceDb.Value = Clamp(settings.SilenceDb, _silenceDb);
        _minimumSilence.Value = Clamp(settings.MinimumSilence, _minimumSilence);
        _remainingPause.Value = Clamp(settings.RemainingPause, _remainingPause);
        _speechMargin.Value = Clamp(settings.SpeechMargin, _speechMargin);
        _preset.SelectedItem = _preset.Items.Contains(settings.Preset) ? settings.Preset : "Normal";
        _quality.SelectedItem = Enum.TryParse<QualityPreset>(settings.Quality, out var quality) ? quality.ToString() : QualityPreset.Alta.ToString();
        _loadingSettings = false;
    }

    private void SaveSettings()
    {
        new AppSettings
        {
            SilenceDb = _silenceDb.Value,
            MinimumSilence = _minimumSilence.Value,
            RemainingPause = _remainingPause.Value,
            SpeechMargin = _speechMargin.Value,
            Preset = _preset.SelectedItem?.ToString() ?? "Personalizado",
            Quality = _quality.SelectedItem?.ToString() ?? QualityPreset.Alta.ToString()
        }.Save();
    }

    private static Label SummaryValue(string value) => new()
    {
        Text = value, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 13)
    };

    private static NumericUpDown Number(decimal minimum, decimal maximum, decimal value, decimal increment, int decimals) => new()
    {
        Minimum = minimum, Maximum = maximum, Value = value, Increment = increment,
        DecimalPlaces = decimals, TextAlign = HorizontalAlignment.Center
    };

    private static decimal Clamp(decimal value, NumericUpDown control) => Math.Min(control.Maximum, Math.Max(control.Minimum, value));

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss\.fff") : time.ToString(@"mm\:ss\.fff");
    }

    private static string AvailableOutput(string desiredPath)
    {
        if (!File.Exists(desiredPath)) return desiredPath;
        var directory = Path.GetDirectoryName(desiredPath) ?? "";
        var extension = Path.GetExtension(desiredPath);
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var index = 2;
        string candidate;
        do candidate = Path.Combine(directory, $"{name}_{index++}{extension}"); while (File.Exists(candidate));
        return candidate;
    }

    private void FitToScreen()
    {
        var area = Screen.FromControl(this).WorkingArea;
        Size = new Size(Math.Min(Width, area.Width - 24), Math.Min(Height, area.Height - 24));
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }
}
