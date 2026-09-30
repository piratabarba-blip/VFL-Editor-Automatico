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
        e.Graphics.Clear(Theme.Surface);
        var bar = new Rectangle(4, 18, Math.Max(1, Width - 8), 24);
        using var keep = new SolidBrush(Theme.Primary);
        using var cut = new SolidBrush(Theme.Danger);
        using var border = new Pen(Theme.Border);
        e.Graphics.FillRectangle(keep, bar);
        if (_analysis is not null && _analysis.Video.Duration > 0)
        {
            foreach (var range in _analysis.Cuts)
            {
                var left = bar.Left + (int)Math.Round(range.Start / _analysis.Video.Duration * bar.Width);
                var right = bar.Left + (int)Math.Round(range.End / _analysis.Video.Duration * bar.Width);
                e.Graphics.FillRectangle(cut, left, bar.Top, Math.Max(1, right - left), bar.Height);
            }
        }
        e.Graphics.DrawRectangle(border, bar);
        using var font = new Font("Segoe UI", 8f);
        using var text = new SolidBrush(Theme.Muted);
        e.Graphics.DrawString("VERDE: mantido", font, text, 4, 49);
        e.Graphics.DrawString("VERMELHO: removido", font, text, Math.Max(120, Width - 145), 49);
    }
}
