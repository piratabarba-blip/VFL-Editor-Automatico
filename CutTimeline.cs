namespace VFL.EditorAutomatico;

internal sealed class CutTimeline : Control
{
    private SilenceAnalysis? _analysis;

    public SilenceAnalysis? Analysis
    {
        get => _analysis;
        set { _analysis = value; Invalidate(); }
    }

    public CutTimeline()
    {
        DoubleBuffered = true;
        Height = 70;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Background);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bar = new Rectangle(28, Math.Max(54, Height / 2 - 34), Math.Max(1, Width - 56), 68);
        using var keep = new SolidBrush(Color.FromArgb(44, Theme.Success));
        using var cut = new SolidBrush(Theme.Danger);
        using var border = new Pen(Theme.Border);
        using var grid = new Pen(Color.FromArgb(35, Theme.Muted));
        using var font = new Font("Segoe UI", 8f);
        using var titleFont = new Font("Segoe UI Semibold", 10f);
        using var text = new SolidBrush(Theme.Muted);
        using var bright = new SolidBrush(Theme.Text);

        if (_analysis is null)
        {
            using var iconFont = new Font("Segoe UI Symbol", 34f);
            using var iconBrush = new SolidBrush(Theme.Dim);
            var icon = "✂";
            var iconSize = e.Graphics.MeasureString(icon, iconFont);
            e.Graphics.DrawString(icon, iconFont, iconBrush, (Width - iconSize.Width) / 2, Height / 2 - 70);
            var message = "Selecione um vídeo para visualizar o mapa de cortes";
            var messageSize = e.Graphics.MeasureString(message, titleFont);
            e.Graphics.DrawString(message, titleFont, text, (Width - messageSize.Width) / 2, Height / 2 + 2);
            return;
        }

        const int divisions = 10;
        for (var index = 0; index <= divisions; index++)
        {
            var x = bar.Left + index * bar.Width / divisions;
            e.Graphics.DrawLine(grid, x, bar.Top - 24, x, bar.Bottom + 24);
            var seconds = _analysis.Video.Duration * index / divisions;
            var label = FormatRuler(seconds);
            var labelSize = e.Graphics.MeasureString(label, font);
            e.Graphics.DrawString(label, font, text, x - labelSize.Width / 2, bar.Top - 23);
        }
        e.Graphics.FillRectangle(keep, bar);
        if (_analysis.Video.Duration > 0)
        {
            foreach (var range in _analysis.Cuts)
            {
                var left = bar.Left + (int)Math.Round(range.Start / _analysis.Video.Duration * bar.Width);
                var right = bar.Left + (int)Math.Round(range.End / _analysis.Video.Duration * bar.Width);
                e.Graphics.FillRectangle(cut, left, bar.Top, Math.Max(1, right - left), bar.Height);
            }
        }
        e.Graphics.DrawRectangle(border, bar);
    }

    private static string FormatRuler(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }
}
