using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SavefileManager.Models;
using SavefileManager.Services;

namespace SavefileManager.Views;

/// <summary>Fonts (one for everything, or one per part) and optional pictures behind the two file lists.</summary>
public partial class AppearanceWindow : Window
{
    private readonly AppearanceSettings _original;
    private readonly AppearanceSettings _work;
    private readonly Dictionary<string, ComboBox> _elementCombos = new();
    private readonly List<(ImageSlot Slot, TextBlock Label, Button Remove)> _slotRows = new();
    private bool _loading = true;
    private bool _closingHandled;

    public AppearanceSettings Result => _work;

    public AppearanceWindow(AppearanceSettings current)
    {
        InitializeComponent();
        _original = current.Clone();
        _work = current.Clone();

        BuildFontControls();
        BuildBackgroundControls();
        RefreshFontLists();

        AllRadio.IsChecked = !_work.Fonts.PerElement;
        EachRadio.IsChecked = _work.Fonts.PerElement;
        UpdateModeUi();
        _loading = false;
    }

    // ------------------------------------------------------------------ fonts

    private void BuildFontControls()
    {
        foreach (var (key, label) in FontService.Elements)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new TextBlock { Text = label, Width = 150, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            var combo = new ComboBox { Tag = key };
            combo.SelectionChanged += ElementCombo_Changed;
            row.Children.Add(combo);
            EachPanel.Children.Add(row);
            _elementCombos[key] = combo;
        }
    }

    private void RefreshFontLists()
    {
        bool was = _loading;
        _loading = true;
        var names = FontService.AllFontNames();

        void Fill(ComboBox box, string? wanted)
        {
            box.ItemsSource = null;
            box.ItemsSource = names;
            box.SelectedItem = names.FirstOrDefault(n => n.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                               ?? names.FirstOrDefault(n => n == "Segoe UI")
                               ?? names.FirstOrDefault();
        }

        Fill(GlobalCombo, _work.Fonts.GlobalFont);
        foreach (var (key, combo) in _elementCombos)
        {
            _work.Fonts.Elements.TryGetValue(key, out var f);
            Fill(combo, string.IsNullOrWhiteSpace(f) ? _work.Fonts.GlobalFont : f);
        }
        _loading = was;
    }

    private void UpdateModeUi()
    {
        GlobalCombo.IsEnabled = AllRadio.IsChecked == true;
        EachPanel.IsEnabled = EachRadio.IsChecked == true;
        EachPanel.Opacity = EachPanel.IsEnabled ? 1.0 : 0.5;
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _work.Fonts.PerElement = EachRadio.IsChecked == true;
        UpdateModeUi();
        Preview();
    }

    private void GlobalCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || GlobalCombo.SelectedItem is not string name) return;
        _work.Fonts.GlobalFont = name;
        // parts that were never given their own font follow the global one
        Preview();
    }

    private void ElementCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || sender is not ComboBox box || box.Tag is not string key || box.SelectedItem is not string name) return;
        _work.Fonts.Elements[key] = name;
        Preview();
    }

    private void AddFont_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose font file(s)",
            Filter = "Font files (*.ttf;*.otf;*.ttc)|*.ttf;*.otf;*.ttc|All files|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != true) return;

        string? last = null;
        var failed = new List<string>();
        foreach (var file in dlg.FileNames)
        {
            var name = FontService.AddFontFile(file);
            if (name == null) failed.Add(System.IO.Path.GetFileName(file)); else last = name;
        }

        if (last != null)
        {
            // keep current choices, then select the newly added font where it's relevant
            RefreshFontLists();
            if (AllRadio.IsChecked == true)
            {
                _work.Fonts.GlobalFont = last;
                GlobalCombo.SelectedItem = last;
            }
            Preview();
        }
        if (failed.Count > 0)
            Dialogs.Info(this, "Couldn't load font", "These files aren't usable fonts:\n\n" + string.Join("\n", failed));
    }

    // ------------------------------------------------------------------ backgrounds

    private void BuildBackgroundControls()
    {
        AddBackgroundSection("Your list (left)", _work.PersonalBackground);
        AddBackgroundSection("Game list (right)", _work.GameBackground);
    }

    private void AddBackgroundSection(string title, ImageSlot slot)
    {
        var box = new StackPanel { Margin = new Thickness(0, 6, 0, 10) };
        box.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var choose = new Button { Content = "Choose picture..." };
        var remove = new Button { Content = "Remove", Margin = new Thickness(8, 0, 0, 0) };
        var label = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
        buttons.Children.Add(choose);
        buttons.Children.Add(remove);
        buttons.Children.Add(label);
        box.Children.Add(buttons);

        var opacityRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        opacityRow.Children.Add(new TextBlock { Text = "Opacity:", Width = 70, VerticalAlignment = VerticalAlignment.Center });
        var pct = new TextBlock { Width = 44, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(pct, Dock.Right);
        var slider = new Slider { Minimum = 0.05, Maximum = 1, Value = slot.Opacity, VerticalAlignment = VerticalAlignment.Center };
        opacityRow.Children.Add(pct);
        opacityRow.Children.Add(slider);
        box.Children.Add(opacityRow);

        var fitRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        fitRow.Children.Add(new TextBlock { Text = "Fit:", Width = 70, VerticalAlignment = VerticalAlignment.Center });
        var fit = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        var fitOptions = new[]
        {
            ("UniformToFill", "Fill (crop edges)"),
            ("Fill", "Stretch"),
            ("Uniform", "Fit inside (keep shape)"),
        };
        foreach (var (_, text) in fitOptions) fit.Items.Add(text);
        fit.SelectedIndex = Math.Max(0, Array.FindIndex(fitOptions, o => o.Item1 == slot.Stretch));
        fitRow.Children.Add(fit);
        box.Children.Add(fitRow);

        void Update()
        {
            bool has = !string.IsNullOrEmpty(slot.ImageFile);
            label.Text = has ? "Picture set" : "No picture";
            remove.IsEnabled = has;
            slider.IsEnabled = has;
            fit.IsEnabled = has;
            pct.Text = $"{(int)Math.Round(slot.Opacity * 100)}%";
        }

        choose.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Choose a background picture", Filter = ImageService.FileFilter };
            if (dlg.ShowDialog(this) != true) return;
            try { slot.ImageFile = ImageService.ImportImage(dlg.FileName, "backgrounds"); }
            catch (Exception ex) { Dialogs.Info(this, "Couldn't use that picture", ex.Message); return; }
            Update();
            Preview();
        };
        remove.Click += (_, _) => { slot.ImageFile = null; Update(); Preview(); };
        slider.ValueChanged += (_, _) => { slot.Opacity = slider.Value; Update(); Preview(); };
        fit.SelectionChanged += (_, _) =>
        {
            if (fit.SelectedIndex >= 0) slot.Stretch = fitOptions[fit.SelectedIndex].Item1;
            Preview();
        };

        Update();
        BackgroundsPanel.Children.Add(box);
    }

    // ------------------------------------------------------------------ preview / buttons

    // Everything previews live on the real window; Cancel puts the original back.
    private void Preview()
    {
        if (_loading) return;
        AppearanceManager.Apply(_work);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _closingHandled = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closingHandled) return;
        AppearanceManager.Apply(_original);
    }
}
