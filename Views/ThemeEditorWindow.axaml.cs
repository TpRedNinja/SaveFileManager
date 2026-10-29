using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SavefileManager.Helpers;
using SavefileManager.Services;
using System.Diagnostics.CodeAnalysis;
using SavefileManager.Themes;

namespace SavefileManager.Views;

/// <summary>Lets the user pick a colour for every themed part of the UI, with live preview.</summary>
public partial class ThemeEditorWindow : Window
{
    private const string V = "*.json";
    private readonly ThemeDefinition _working = new();
    private readonly ThemeDefinition _previouslyActive = new();
    private readonly Dictionary<string, (Border Swatch, TextBox Hex)> _rows = [];
    private bool _updatingHex;
    private bool _saved;
    private bool _rowsReady, _closed;

    public bool Accepted { get; private set; }

    /// <summary>The edited theme; only valid when <see cref="Accepted"/> is true.</summary>
    public ThemeDefinition Result => _working;

    public ThemeEditorWindow()
    {
        InitializeComponent();
    }

    public ThemeEditorWindow(ThemeDefinition customTheme, ThemeDefinition currentlyActive) : this()
    {
        ThemeManager.Attach(this);
        _working = customTheme.Clone();
        _working.Name = ThemeManager.CustomName;
        _working.FillMissing(BuiltInThemes.Create("Dark"));
        _previouslyActive = currentlyActive;

        foreach (var n in BuiltInThemes.Names) BaseCombo.Items.Add(n);
        BaseCombo.SelectedIndex = 0;

        Closing += Window_Closing;
        Opened += async (_, _) => await FillWhenShown();
    }

    // The window is shown first, then the (many) rows are added in small batches so it never freezes
    // or shows an empty frame while it builds.
    private async Task FillWhenShown()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        int n = 0;
        string? lastGroup = null;
        foreach (var key in ThemeKeys.All)
        {
            if (_closed) return;
            AddRow(key, ref lastGroup);
            if (++n % 8 == 0)
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        if (_closed) return;
        _rowsReady = true;
        ThemeManager.Apply(_working);   // live preview on the real window behind this one
    }

    private void AddRow(ThemeKey key, ref string? lastGroup)
    {
        {
            if (key.Group != lastGroup)
            {
                lastGroup = key.Group;
                RowsPanel.Children.Add(new TextBlock
                {
                    Text = key.Group,
                    FontWeight = FontWeight.Bold,
                    FontSize = 14,
                    Margin = new Thickness(0, RowsPanel.Children.Count == 0 ? 0 : 16, 0, 6)
                });
            }

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(44, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel));

            var label = new TextBlock { Text = key.Label, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

            var swatch = new Border
            {
                Height = 28,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Cursor = new Cursor(StandardCursorType.Hand),
                Background = new SolidColorBrush(_working.Get(key.Key))
            };
            ToolTip.SetTip(swatch, "Click to choose a color");
            swatch.Bind(Border.BorderBrushProperty, this.GetResourceObservable("InputBorderBrush"));

            var hex = new TextBox
            {
                Text = _working.Colors[key.Key].ToUpperInvariant(),
                MaxLength = 7,
                Width = 92,
                Height = 30,
                MinHeight = 0,
                Padding = new Thickness(6, 0),
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };

            var k = key;
            swatch.PointerPressed += (_, _) => PickColor(k);
            hex.TextChanged += (_, _) =>
            {
                if (_updatingHex) return;
                var t = (hex.Text ?? "").Trim().TrimStart('#');
                if (t.Length == 6 && ColorUtil.TryParseHex(hex.Text, out var c))
                    SetColor(k.Key, c, updateHexBox: false);
            };

            Grid.SetColumn(swatch, 1);
            Grid.SetColumn(hex, 2);
            grid.Children.Add(label);
            grid.Children.Add(swatch);
            grid.Children.Add(hex);
            RowsPanel.Children.Add(grid);
            _rows[key.Key] = (swatch, hex);
        }
    }

    private void SetColor(string key, Color color, bool updateHexBox)
    {
        _working.Set(key, color);
        if (_rows.TryGetValue(key, out var row))
        {
            row.Swatch.Background = new SolidColorBrush(color);
            if (updateHexBox)
            {
                _updatingHex = true;
                row.Hex.Text = ColorUtil.ToHex(color);
                _updatingHex = false;
            }
        }
        if (_rowsReady) ThemeManager.Apply(_working);
    }

    private async void PickColor(ThemeKey key)
    {
        var original = _working.Get(key.Key);
        var dlg = new ColorPickerDialog(original, key.Label);
        dlg.ColorChanged += c => SetColor(key.Key, c, updateHexBox: true);
        await Dialogs.ShowModal(dlg, this);
        SetColor(key.Key, dlg.Accepted ? dlg.SelectedColor : original, true);
    }

    private void RefreshAllRows()
    {
        _updatingHex = true;
        foreach (var key in ThemeKeys.All)
        {
            if (!_rows.TryGetValue(key.Key, out var row)) continue;
            var c = _working.Get(key.Key);
            row.Swatch.Background = new SolidColorBrush(c);
            row.Hex.Text = ColorUtil.ToHex(c);
        }
        _updatingHex = false;
        ThemeManager.Apply(_working);
    }

    private async void LoadBase_Click(object? sender, RoutedEventArgs e)
    {
        if (BaseCombo.SelectedItem is not string name) return;
        if (!await Dialogs.Confirm(this, "Load Colors",
                $"Replace all colors below with the \"{name}\" theme colors?"))
            return;
        _working.CopyColorsFrom(BuiltInThemes.Create(name));
        RefreshAllRows();
    }

    // ---------------------------------------------------------- import / export
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static readonly string[] JsonPattern = [V];

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        var path = await Pickers.SaveFile(this, "Export custom theme", "savefile-manager-theme.json",
            "Theme file", JsonPattern);
        if (path == null) return;
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(_working, Json));
        }
        catch (Exception ex)
        {
            await Dialogs.Info(this, "Export Failed", ex.Message);
        }
    }

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        var path = await Pickers.OpenFile(this, "Import theme", "Theme file", JsonPattern);
        if (path == null) return;
        try
        {
            var imported = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("The file is empty.");
            imported.FillMissing(_working);
            _working.CopyColorsFrom(imported);
            RefreshAllRows();
        }
        catch (Exception ex)
        {
            await Dialogs.Info(this, "Import Failed", "That file isn't a valid theme file.\n\n" + ex.Message);
        }
    }

    // ---------------------------------------------------------- closing
    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        _saved = true;
        Accepted = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Accepted = false;
        Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        _closed = true;
        // Closed without saving: put back whatever theme was active before.
        if (!_saved) ThemeManager.Apply(_previouslyActive);
    }
}
