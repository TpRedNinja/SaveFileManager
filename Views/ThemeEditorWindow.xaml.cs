using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SavefileManager.Helpers;
using SavefileManager.Models;
using SavefileManager.Services;

namespace SavefileManager.Views;

/// <summary>Lets the user pick a colour for every themed part of the UI, with live preview.</summary>
public partial class ThemeEditorWindow : Window
{
    private readonly ThemeDefinition _working;
    private readonly ThemeDefinition _previouslyActive;
    private readonly Dictionary<string, (Border Swatch, TextBox Hex)> _rows = new();
    private bool _updatingHex;
    private bool _saved;

    /// <summary>The edited theme; only valid when ShowDialog() returned true.</summary>
    public ThemeDefinition Result => _working;

    public ThemeEditorWindow(ThemeDefinition customTheme, ThemeDefinition currentlyActive)
    {
        InitializeComponent();
        ThemeManager.Attach(this);
        _working = customTheme.Clone();
        _working.Name = ThemeManager.CustomName;
        _working.FillMissing(BuiltInThemes.Create("Dark"));
        _previouslyActive = currentlyActive;

        foreach (var n in BuiltInThemes.Names) BaseCombo.Items.Add(n);
        BaseCombo.SelectedIndex = 0;

        BuildRows();
        ThemeManager.Apply(_working);   // preview straight away
    }

    private void BuildRows()
    {
        string? lastGroup = null;
        foreach (var key in ThemeKeys.All)
        {
            if (key.Group != lastGroup)
            {
                lastGroup = key.Group;
                RowsPanel.Children.Add(new TextBlock
                {
                    Text = key.Group,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Margin = new Thickness(0, RowsPanel.Children.Count == 0 ? 0 : 16, 0, 6)
                });
            }

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });

            var label = new TextBlock { Text = key.Label, VerticalAlignment = VerticalAlignment.Center };

            var swatch = new Border
            {
                Height = 24,
                Margin = new Thickness(8, 0, 8, 0),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand,
                ToolTip = "Click to choose a color",
                Background = new SolidColorBrush(_working.Get(key.Key))
            };
            swatch.SetResourceReference(Border.BorderBrushProperty, "InputBorderBrush");

            var hex = new TextBox
            {
                Text = _working.Colors[key.Key].ToUpperInvariant(),
                MaxLength = 7,
                Padding = new Thickness(4, 2, 4, 2)
            };

            var k = key;
            swatch.MouseLeftButtonDown += (_, _) => PickColor(k);
            hex.TextChanged += (_, _) =>
            {
                if (_updatingHex) return;
                var t = hex.Text.Trim().TrimStart('#');
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
        var row = _rows[key];
        row.Swatch.Background = new SolidColorBrush(color);
        if (updateHexBox)
        {
            _updatingHex = true;
            row.Hex.Text = ColorUtil.ToHex(color);
            _updatingHex = false;
        }
        ThemeManager.Apply(_working);
    }

    private void PickColor(ThemeKey key)
    {
        var original = _working.Get(key.Key);
        var dlg = new ColorPickerDialog(original, key.Label) { Owner = this };
        dlg.ColorChanged += c => SetColor(key.Key, c, updateHexBox: true);
        if (dlg.ShowDialog() == true)
            SetColor(key.Key, dlg.SelectedColor, true);
        else
            SetColor(key.Key, original, true);
    }

    private void RefreshAllRows()
    {
        _updatingHex = true;
        foreach (var key in ThemeKeys.All)
        {
            var c = _working.Get(key.Key);
            _rows[key.Key].Swatch.Background = new SolidColorBrush(c);
            _rows[key.Key].Hex.Text = ColorUtil.ToHex(c);
        }
        _updatingHex = false;
        ThemeManager.Apply(_working);
    }

    private void LoadBase_Click(object sender, RoutedEventArgs e)
    {
        if (BaseCombo.SelectedItem is not string name) return;
        if (!Dialogs.Confirm(this, "Load Colors",
                $"Replace all colors below with the \"{name}\" theme colors?"))
            return;
        _working.CopyColorsFrom(BuiltInThemes.Create(name));
        RefreshAllRows();
    }

    // ---------------------------------------------------------- import / export
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export custom theme",
            Filter = "Theme file (*.json)|*.json",
            FileName = "savefile-manager-theme.json"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(_working, Json));
        }
        catch (Exception ex)
        {
            Dialogs.Info(this, "Export Failed", ex.Message);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Import theme", Filter = "Theme file (*.json)|*.json" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var imported = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(dlg.FileName), Json);
            if (imported == null) throw new InvalidDataException("The file is empty.");
            imported.FillMissing(_working);
            _working.CopyColorsFrom(imported);
            RefreshAllRows();
        }
        catch (Exception ex)
        {
            Dialogs.Info(this, "Import Failed", "That file isn't a valid theme file.\n\n" + ex.Message);
        }
    }

    // ---------------------------------------------------------- closing
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _saved = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Closed without saving: put back whatever theme was active before.
        if (!_saved) ThemeManager.Apply(_previouslyActive);
    }
}
