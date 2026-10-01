using System.Diagnostics;

namespace VFL.EditorAutomatico;

internal sealed class MainForm : Form
{
    private readonly TextBox _input = new();
    private readonly TextBox _output = new();
    private readonly ComboBox _preset = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ValueSlider _silenceDb = Slider(-80, -5, -35, 1, 0);
    private readonly ValueSlider _minimumSilence = Slider(0.10m, 10, 0.70m, 0.05m, 2);
    private readonly ValueSlider _remainingPause = Slider(0, 3, 0.25m, 0.05m, 2);
    private readonly ValueSlider _speechMargin = Slider(0, 1, 0.08m, 0.01m, 2);
    private readonly ComboBox _quality = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _selectVideo = new() { Text = "SELECIONAR VÍDEO", Tag = "primary" };
    private readonly Button _selectOutput = new() { Text = "...", Width = 42 };
    private readonly Button _analyze = new() { Text = "ANALISAR SILÊNCIOS" };
    private readonly Button _render = new() { Text = "GERAR VÍDEO EDITADO", Tag = "success", Enabled = false };
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
    private readonly Label _videoTitle = new() { Text = "Nenhum vídeo carregado", AutoEllipsis = true, ForeColor = Theme.AccentCyan };
    private readonly Label _videoMeta = new() { Text = "Selecione um arquivo para começar", AutoEllipsis = true, ForeColor = Theme.Muted };
    private readonly Label _outputFolder = new() { Text = "Destino automático", AutoEllipsis = true, ForeColor = Theme.Muted };
    private readonly CutTimeline _timeline = new() { Dock = DockStyle.Fill };
    private readonly ModernProgressBar _progress = new() { Dock = DockStyle.Fill };
    private SilenceAnalysis? _analysis;
    private CancellationTokenSource? _operationCts;
    private bool _loadingSettings;
    private bool _applyingPreset;

    public MainForm()
    {
        Text = "VFL Editor Automático";
        Size = new Size(1440, 860);
        MinimumSize = new Size(1180, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Background;
        Theme.ApplyIcon(this);
        BuildProfessionalUi();
        Theme.Apply(this);
        LoadSettings();
        WireEvents();
        Shown += (_, _) => FitToScreen();
        FormClosed += (_, _) => { SaveSettings(); _operationCts?.Cancel(); };
    }

    private void BuildProfessionalUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Tag = "background", Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        var topbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(16, 10, 16, 9), Tag = "header", Margin = Padding.Empty };
        topbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        topbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        topbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));

        topbar.Controls.Add(CreateBrand("VFL Editor Automático", "CORTE DE SILÊNCIOS  •  v1.1"), 0, 0);

        _selectVideo.Dock = DockStyle.Fill; _selectVideo.Margin = new Padding(12, 5, 12, 5);
        topbar.Controls.Add(_selectVideo, 1, 0);

        var videoTag = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(12, 4, 12, 4), Tag = "card", Margin = new Padding(0, 2, 14, 2) };
        videoTag.RowStyles.Add(new RowStyle(SizeType.Percent, 54)); videoTag.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        _videoTitle.Dock = DockStyle.Fill; _videoTitle.TextAlign = ContentAlignment.BottomLeft; _videoTitle.Font = new Font("Segoe UI Semibold", 9); _videoTitle.Margin = Padding.Empty;
        _videoMeta.Dock = DockStyle.Fill; _videoMeta.TextAlign = ContentAlignment.TopLeft; _videoMeta.Font = new Font("Segoe UI", 7.8f); _videoMeta.Margin = Padding.Empty;
        videoTag.Controls.Add(_videoTitle, 0, 0); videoTag.Controls.Add(_videoMeta, 0, 1);
        topbar.Controls.Add(videoTag, 2, 0);

        var destination = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Tag = "header", Margin = Padding.Empty };
        destination.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); destination.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        var destinationInfo = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Tag = "card", Padding = new Padding(10, 4, 8, 4), Margin = new Padding(0, 2, 6, 2) };
        destinationInfo.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); destinationInfo.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        destinationInfo.Controls.Add(new Label { Text = "SALVAR RESULTADO EM", Dock = DockStyle.Fill, ForeColor = Theme.Dim, Font = new Font("Segoe UI Semibold", 7), TextAlign = ContentAlignment.BottomLeft, Margin = Padding.Empty }, 0, 0);
        _outputFolder.Dock = DockStyle.Fill; _outputFolder.TextAlign = ContentAlignment.TopLeft; _outputFolder.Font = new Font("Consolas", 8); _outputFolder.Margin = Padding.Empty;
        destinationInfo.Controls.Add(_outputFolder, 0, 1);
        destination.Controls.Add(destinationInfo, 0, 0);
        _selectOutput.Dock = DockStyle.Fill; _selectOutput.Margin = new Padding(0, 5, 0, 5); destination.Controls.Add(_selectOutput, 1, 0);
        topbar.Controls.Add(destination, 3, 0);
        root.Controls.Add(topbar, 0, 0);

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Tag = "background", Margin = Padding.Empty };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        root.Controls.Add(main, 0, 1);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 10, Padding = new Padding(16, 14, 16, 14), Tag = "sidebar",
            Margin = Padding.Empty, AutoScroll = true, AutoScrollMinSize = new Size(0, 600)
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        for (var index = 0; index < 4; index++) left.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        left.Controls.Add(SectionTitle("AJUSTES DO CORTE", "⚙"), 0, 0);
        left.Controls.Add(new Label { Text = "Defina quanto silêncio será detectado e quanto da pausa deve permanecer.", Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.3f) }, 0, 1);
        left.Controls.Add(StackedCombo("MODO DE CORTE", _preset), 0, 2);
        left.Controls.Add(new Label { Text = "DETECÇÃO DE SILÊNCIO", Dock = DockStyle.Fill, ForeColor = Theme.AccentCyan, Font = new Font("Segoe UI Semibold", 8), TextAlign = ContentAlignment.BottomLeft }, 0, 3);
        left.Controls.Add(StackedNumber("NÍVEL DE SILÊNCIO", _silenceDb, "dB  •  menor é mais conservador"), 0, 4);
        left.Controls.Add(StackedNumber("PAUSA MÍNIMA", _minimumSilence, "segundos para considerar corte"), 0, 5);
        left.Controls.Add(StackedNumber("PAUSA RESTANTE", _remainingPause, "respiração mantida entre falas"), 0, 6);
        left.Controls.Add(StackedNumber("MARGEM DA FALA", _speechMargin, "proteção antes e depois"), 0, 7);
        left.Controls.Add(new Label { Text = "DICA\nComece pelo modo Normal. Use Agressivo apenas quando houver muitas pausas curtas.", Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.3f), Padding = new Padding(10), Tag = "card" }, 0, 8);
        _analyze.Dock = DockStyle.Fill; _analyze.Tag = "primary"; _analyze.Margin = new Padding(0, 6, 0, 0); left.Controls.Add(_analyze, 0, 9);
        main.Controls.Add(left, 0, 0);

        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Tag = "background", Margin = Padding.Empty };
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        var workspaceHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20, 10, 20, 6), Tag = "background" };
        workspaceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); workspaceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        workspaceHeader.Controls.Add(new Label { Text = "MAPA DE CORTES", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 10), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _headline.TextAlign = ContentAlignment.MiddleRight; _headline.Font = new Font("Segoe UI Semibold", 9); workspaceHeader.Controls.Add(_headline, 1, 0);
        workspace.Controls.Add(workspaceHeader, 0, 0);
        _timeline.Margin = new Padding(18, 0, 18, 10); workspace.Controls.Add(_timeline, 0, 1);
        var workspaceFooter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(20, 12, 20, 12), Tag = "header", Margin = Padding.Empty };
        workspaceFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); workspaceFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); workspaceFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        workspaceFooter.Controls.Add(new Label { Text = "●  VERDE  Trecho mantido", Dock = DockStyle.Fill, ForeColor = Theme.Success, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 8) }, 0, 0);
        workspaceFooter.Controls.Add(new Label { Text = "Cada marca vermelha representa um silêncio que será encurtado.", Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.3f) }, 1, 0);
        workspaceFooter.Controls.Add(new Label { Text = "●  VERMELHO  Removido", Dock = DockStyle.Fill, ForeColor = Theme.Danger, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", 8) }, 2, 0);
        workspace.Controls.Add(workspaceFooter, 0, 2);
        main.Controls.Add(workspace, 1, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, Padding = new Padding(16, 14, 16, 14), Tag = "sidebar", Margin = Padding.Empty };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        right.Controls.Add(SectionTitle("RESUMO DA EDIÇÃO", "▦"), 0, 0);
        var summary = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Tag = "card", Padding = new Padding(12), Margin = new Padding(0, 0, 0, 10) };
        for (var index = 0; index < 4; index++) summary.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        summary.Controls.Add(SummaryRow("Duração original", _originalValue), 0, 0);
        summary.Controls.Add(SummaryRow("Tempo removido", _removedValue), 0, 1);
        summary.Controls.Add(SummaryRow("Duração estimada", _finalValue), 0, 2);
        summary.Controls.Add(SummaryRow("Cortes sugeridos", _cutsValue), 0, 3);
        right.Controls.Add(summary, 0, 1);
        right.Controls.Add(SectionTitle("EXPORTAÇÃO", "↗"), 0, 2);
        var export = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Tag = "sidebar", Margin = Padding.Empty };
        export.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); export.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); export.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); export.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        export.Controls.Add(StackedCombo("QUALIDADE DO MP4", _quality), 0, 0);
        var outputCard = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Tag = "card", Padding = new Padding(10), Margin = new Padding(0, 6, 0, 6) };
        outputCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); outputCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outputCard.Controls.Add(new Label { Text = "ARQUIVO DE SAÍDA", Dock = DockStyle.Fill, ForeColor = Theme.Dim, Font = new Font("Segoe UI Semibold", 7.5f) }, 0, 0);
        var rightOutput = new Label { Dock = DockStyle.Fill, Text = "Definido automaticamente ao selecionar o vídeo", ForeColor = Theme.Muted, AutoEllipsis = true, Font = new Font("Consolas", 8), Name = "RightOutputLabel" };
        outputCard.Controls.Add(rightOutput, 0, 1); export.Controls.Add(outputCard, 0, 1);
        export.Controls.Add(new Label { Text = "Formato: MP4 H.264 + AAC\nOriginal preservado\nFFmpeg local e independente", Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f), Padding = new Padding(4, 10, 0, 0) }, 0, 2);
        _render.Dock = DockStyle.Fill; _render.Margin = new Padding(0, 4, 0, 0); export.Controls.Add(_render, 0, 3);
        right.Controls.Add(export, 0, 3);
        _clear.Dock = DockStyle.Fill; _clear.Margin = new Padding(0, 8, 0, 0); right.Controls.Add(_clear, 0, 4);
        main.Controls.Add(right, 2, 0);

        var statusbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Padding = new Padding(16, 4, 16, 5), Tag = "header", Margin = Padding.Empty };
        statusbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); statusbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); statusbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); statusbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        statusbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); statusbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 5));
        statusbar.Controls.Add(new Label { Text = "●  PROCESSAMENTO LOCAL", Dock = DockStyle.Fill, ForeColor = Theme.AccentCyan, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 7.5f) }, 0, 0);
        _status.Dock = DockStyle.Fill; _status.TextAlign = ContentAlignment.MiddleLeft; statusbar.Controls.Add(_status, 1, 0);
        _cancel.Dock = DockStyle.Fill; _cancel.Margin = new Padding(0, 2, 10, 2); statusbar.Controls.Add(_cancel, 2, 0);
        statusbar.Controls.Add(new Label { Text = "ORIGINAL PRESERVADO", Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", 8) }, 3, 0);
        statusbar.Controls.Add(_progress, 0, 1); statusbar.SetColumnSpan(_progress, 4);
        root.Controls.Add(statusbar, 0, 2);

        void SyncOutputLabels()
        {
            var value = string.IsNullOrWhiteSpace(_output.Text) ? "Destino automático" : _output.Text;
            _outputFolder.Text = value;
            rightOutput.Text = value;
        }
        _output.TextChanged += (_, _) => SyncOutputLabels();
        SyncOutputLabels();
    }

    private static Label SectionTitle(string text, string icon) => new()
    {
        Text = $"{icon}  {text}", Dock = DockStyle.Fill, ForeColor = Theme.Text,
        Font = new Font("Segoe UI Semibold", 9), TextAlign = ContentAlignment.MiddleLeft
    };

    private Control CreateBrand(string title, string badge)
    {
        var brand = new Panel { Dock = DockStyle.Fill, Tag = "header", Margin = Padding.Empty };
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "vfl-suite-logo.png");
        Control mark = File.Exists(logoPath)
            ? new PictureBox { Image = Image.FromFile(logoPath), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(0, 5), Size = new Size(44, 44) }
            : new Label { Text = "V", Location = new Point(0, 5), Size = new Size(44, 44), BackColor = Theme.Primary, Tag = "accent", TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Black", 15) };
        brand.Controls.Add(mark);
        brand.Controls.Add(new Label { Text = title, Location = new Point(50, 3), Size = new Size(235, 25), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, TextAlign = ContentAlignment.BottomLeft, Font = new Font("Segoe UI Semibold", 11.5f), Margin = Padding.Empty });
        brand.Controls.Add(new Label { Text = badge, Location = new Point(50, 29), Size = new Size(235, 16), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, Tag = "brand-badge", TextAlign = ContentAlignment.TopLeft, Font = new Font("Segoe UI Semibold", 6.8f), Margin = Padding.Empty });
        return brand;
    }

    private static Control StackedNumber(string title, ValueSlider slider, string hint)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Tag = "sidebar", Margin = Padding.Empty };
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 15));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.3f), TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        row.Controls.Add(new Label { Text = hint, Dock = DockStyle.Fill, ForeColor = Theme.Dim, Font = new Font("Segoe UI", 7.3f), TextAlign = ContentAlignment.TopLeft }, 0, 1);
        slider.Dock = DockStyle.Fill; slider.Margin = new Padding(0, 1, 0, 1); row.Controls.Add(slider, 0, 2);
        return row;
    }

    private static Control StackedCombo(string title, ComboBox combo)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Tag = "sidebar", Margin = Padding.Empty };
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.3f), TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        combo.Dock = DockStyle.Fill; combo.Margin = new Padding(0, 6, 0, 4); row.Controls.Add(combo, 0, 1);
        return row;
    }

    private static Control SummaryRow(string title, Label value)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Tag = "card", Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        row.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        value.Font = new Font("Segoe UI Semibold", 10); value.TextAlign = ContentAlignment.MiddleRight; row.Controls.Add(value, 1, 0);
        return row;
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
        _selectVideo.Click += async (_, _) => await SelectVideoAsync();
        _selectOutput.Click += (_, _) => SelectOutput();
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

    private async Task SelectVideoAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Vídeos|*.mp4;*.mov;*.mkv;*.webm;*.avi|Todos os arquivos|*.*",
            Title = "Selecione o vídeo que será editado"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        Reset(false);
        _input.Text = dialog.FileName;
        _output.Text = AvailableOutput(Path.Combine(
            Path.GetDirectoryName(dialog.FileName)!,
            Path.GetFileNameWithoutExtension(dialog.FileName) + "_SEM_SILENCIOS.mp4"));
        _videoTitle.Text = Path.GetFileName(dialog.FileName);
        _videoMeta.Text = "Lendo áudio e preparando o mapa de cortes...";
        await AnalyzeAsync();
    }

    private void SelectOutput()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Vídeo MP4|*.mp4",
            DefaultExt = "mp4",
            AddExtension = true,
            Title = "Salvar vídeo editado em",
            FileName = string.IsNullOrWhiteSpace(_output.Text) ? "video_SEM_SILENCIOS.mp4" : Path.GetFileName(_output.Text),
            InitialDirectory = string.IsNullOrWhiteSpace(_output.Text) ? null : Path.GetDirectoryName(_output.Text)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _output.Text = dialog.FileName;
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

    private static Control NumberRow(string label, ValueSlider slider, string hint)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Tag = "surface" };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        row.Controls.Add(new Label { Text = label.ToUpperInvariant(), Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        slider.Dock = DockStyle.Fill; slider.Margin = new Padding(0, 8, 10, 8); row.Controls.Add(slider, 1, 0);
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
        catch (OperationCanceledException) { _status.Text = "Análise cancelada."; _videoMeta.Text = "Análise cancelada"; }
        catch (Exception exception) { _status.Text = "Falha na análise."; _videoMeta.Text = "Não foi possível analisar este arquivo"; AppDialog.ShowMessage(this, "Erro na análise", exception.Message); }
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
        _videoTitle.Text = Path.GetFileName(_input.Text);
        _videoMeta.Text = $"{FormatTime(analysis.Video.Duration)}  •  {analysis.Cuts.Count} cortes sugeridos";
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
        _videoMeta.Text = "Configuração alterada • analise novamente";
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
        if (clearPaths)
        {
            _input.Clear(); _output.Clear();
            _videoTitle.Text = "Nenhum vídeo carregado";
            _videoMeta.Text = "Selecione um arquivo para começar";
        }
        _status.Text = clearPaths ? "Pronto para o próximo vídeo. Seus ajustes foram mantidos." : "Preparando o novo vídeo...";
    }

    private void SetBusy(bool busy)
    {
        _analyze.Enabled = !busy;
        _render.Enabled = !busy && _analysis is not null && _analysis.Cuts.Count > 0;
        _clear.Enabled = !busy;
        _cancel.Enabled = busy;
        _selectVideo.Enabled = !busy; _selectOutput.Enabled = !busy;
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

    private static ValueSlider Slider(decimal minimum, decimal maximum, decimal value, decimal increment, int decimals) =>
        new() { Minimum = minimum, Maximum = maximum, Value = value, Increment = increment, DecimalPlaces = decimals, Tag = "surface" };

    private static decimal Clamp(decimal value, ValueSlider control) => Math.Min(control.Maximum, Math.Max(control.Minimum, value));

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
