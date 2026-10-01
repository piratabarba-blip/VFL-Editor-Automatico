namespace VFL.EditorAutomatico;

internal sealed class ModernProgressBar : Control
{
    private int _value;
    public int Value { get => _value; set { _value = Math.Clamp(value, 0, 100); Invalidate(); } }
    public ModernProgressBar() { DoubleBuffered = true; Height = 8; }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Field);
        var fillWidth = (int)(Width * _value / 100d);
        if (fillWidth <= 0) return;
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Rectangle(0, 0, Math.Max(1, fillWidth), Height), Theme.AccentCyan, Theme.Primary, 0f);
        e.Graphics.FillRectangle(brush, 0, 0, fillWidth, Height);
    }
}
