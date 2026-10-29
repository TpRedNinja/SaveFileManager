using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using SavefileManager.Helpers;
using SavefileManager.Services;

namespace SavefileManager.Views;

/// <summary>
/// Full colour picker (saturation/brightness square, hue bar, RGB, hex and presets).
/// <see cref="ColorChanged"/> fires on every change so the theme editor can preview live.
/// </summary>
public partial class ColorPickerDialog : Window
{
    private const double BoxW = 240, BoxH = 180;

    private static readonly string[] Presets =
    [
        "#000000", "#1F1F1F", "#212020", "#2B2B2B", "#3C3C3C", "#555555", "#808080", "#ADADAD",
        "#CCCCCC", "#E1E1E1", "#F0F0F0", "#FFFFFF", "#002B36", "#073642", "#586E75", "#93A1A1",
        "#4287F5", "#0078D7", "#57B6FF", "#838AEB", "#8957FF", "#B58900", "#CB4B16", "#DC322F",
        "#D33682", "#6C71C4", "#268BD2", "#2AA198", "#859900", "#2EA043", "#FF6B6B", "#FFD43B",
    ];

    private readonly Color _original;
    private double _h, _s, _v;
    private bool _updating, _draggingSv, _draggingHue;

    public bool Accepted { get; private set; }
    public Color SelectedColor { get; private set; }
    public event Action<Color>? ColorChanged;

    public ColorPickerDialog()
    {
        InitializeComponent();
    }

    public ColorPickerDialog(Color initial, string title) : this()
    {
        ThemeManager.Attach(this);
        Title = title;
        _original = initial;
        SelectedColor = initial;
        OldSwatch.Background = new SolidColorBrush(initial);

        SvBox.PointerPressed += Sv_Down;
        SvBox.PointerMoved += Sv_Move;
        SvBox.PointerReleased += Sv_Up;
        SvBox.PointerCaptureLost += (_, _) => _draggingSv = false;
        HueBar.PointerPressed += Hue_Down;
        HueBar.PointerMoved += Hue_Move;
        HueBar.PointerReleased += Hue_Up;
        HueBar.PointerCaptureLost += (_, _) => _draggingHue = false;
        OldSwatch.PointerPressed += (_, _) => { SetFromColor(_original, true); Refresh(true); };
        RBox.TextChanged += Rgb_Changed;
        GBox.TextChanged += Rgb_Changed;
        BBox.TextChanged += Rgb_Changed;
        HexBox.TextChanged += Hex_Changed;

        BuildPresets();
        SetFromColor(initial, true);
        Refresh(fireEvent: false);
    }

    private void BuildPresets()
    {
        foreach (var hex in Presets)
        {
            var color = ColorUtil.FromHex(hex, Colors.Black);
            var swatch = new Border
            {
                Width = 22, Height = 22, Margin = new Thickness(0, 0, 4, 4),
                Background = new SolidColorBrush(color),
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(swatch, hex);
            swatch.Bind(Border.BorderBrushProperty, this.GetResourceObservable("InputBorderBrush"));
            swatch.PointerPressed += (_, _) =>
            {
                SetFromColor(color, true);
                Refresh(true);
            };
            PresetPanel.Children.Add(swatch);
        }
    }

    private void SetFromColor(Color c, bool keepHueWhenGray)
    {
        var (h, s, v) = ColorUtil.ToHsv(c);
        if (!keepHueWhenGray || (s > 0 && v > 0) || (_h == 0 && _s == 0 && _v == 0)) _h = h;
        _s = s;
        _v = v;
    }

    private Color Current => ColorUtil.FromHsv(_h, _s, _v);

    /// <summary>Pushes the HSV state to every control.</summary>
    private void Refresh(bool fireEvent)
    {
        _updating = true;
        var c = Current;
        SelectedColor = c;
        SvBox.Background = new SolidColorBrush(ColorUtil.FromHsv(_h, 1, 1));
        Canvas.SetLeft(SvMarker, _s * BoxW - 7);
        Canvas.SetTop(SvMarker, (1 - _v) * BoxH - 7);
        Canvas.SetLeft(SvMarkerOuter, _s * BoxW - 8);
        Canvas.SetTop(SvMarkerOuter, (1 - _v) * BoxH - 8);
        Canvas.SetLeft(HueMarker, _h / 360.0 * BoxW - 2.5);
        NewSwatch.Background = new SolidColorBrush(c);
        RBox.Text = c.R.ToString(CultureInfo.InvariantCulture);
        GBox.Text = c.G.ToString(CultureInfo.InvariantCulture);
        BBox.Text = c.B.ToString(CultureInfo.InvariantCulture);
        HexBox.Text = ColorUtil.ToHex(c).TrimStart('#');
        _updating = false;
        if (fireEvent) ColorChanged?.Invoke(c);
    }

    // ---------------------------------------------------------- square / hue bar dragging
    private void Sv_Down(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(SvBox).Properties.IsLeftButtonPressed) return;
        _draggingSv = true;
        e.Pointer.Capture(SvBox);
        UpdateSv(e.GetPosition(SvBox));
    }
    private void Sv_Move(object? sender, PointerEventArgs e)
    {
        if (_draggingSv) UpdateSv(e.GetPosition(SvBox));
    }
    private void Sv_Up(object? sender, PointerReleasedEventArgs e)
    {
        _draggingSv = false;
        e.Pointer.Capture(null);
    }
    private void UpdateSv(Point p)
    {
        _s = Math.Clamp(p.X / BoxW, 0, 1);
        _v = 1 - Math.Clamp(p.Y / BoxH, 0, 1);
        Refresh(true);
    }

    private void Hue_Down(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(HueBar).Properties.IsLeftButtonPressed) return;
        _draggingHue = true;
        e.Pointer.Capture(HueBar);
        UpdateHue(e.GetPosition(HueBar));
    }
    private void Hue_Move(object? sender, PointerEventArgs e)
    {
        if (_draggingHue) UpdateHue(e.GetPosition(HueBar));
    }
    private void Hue_Up(object? sender, PointerReleasedEventArgs e)
    {
        _draggingHue = false;
        e.Pointer.Capture(null);
    }
    private void UpdateHue(Point p)
    {
        _h = Math.Clamp(p.X / BoxW, 0, 1) * 359.999;
        Refresh(true);
    }

    // ---------------------------------------------------------- text boxes
    private void Rgb_Changed(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        if (byte.TryParse(RBox.Text, out var r) && byte.TryParse(GBox.Text, out var g) &&
            byte.TryParse(BBox.Text, out var b))
        {
            SetFromColor(Color.FromRgb(r, g, b), true);
            Refresh(true);
        }
    }

    private void Hex_Changed(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        // Only react to a complete 6-digit value so typing isn't interrupted.
        var t = (HexBox.Text ?? "").Trim();
        if (t.TrimStart('#').Length == 6 && ColorUtil.TryParseHex(t, out var c))
        {
            SetFromColor(c, true);
            Refresh(true);
        }
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) { Accepted = true; Close(); }
    private void Cancel_Click(object? sender, RoutedEventArgs e) { Accepted = false; Close(); }
}
