using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Cupertino.Controls;

/// <summary>
/// Separates item groups inside a <see cref="CupertinoToolbar"/>.
/// </summary>
public class ToolbarSpacer : Control
{
}

/// <summary>
/// A bottom action toolbar with glass item groups.
/// </summary>
public class CupertinoToolbar : ItemsControl
{
    private Grid? _groups;
    private ItemsSourceView? _itemsView;

    /// <summary>
    /// Creates a CupertinoToolbar with its default settings.
    /// </summary>
    public CupertinoToolbar() => ConnectItemsView();

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ClearGroups();
        base.OnApplyTemplate(e);
        _groups = e.NameScope.Find<Grid>("PART_Groups");
        BuildGroups();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ConnectItemsView();
            BuildGroups();
        }
    }

    private void ConnectItemsView()
    {
        if (_itemsView is not null)
            _itemsView.CollectionChanged -= OnItemsCollectionChanged;
        _itemsView = ItemsView;
        _itemsView.CollectionChanged += OnItemsCollectionChanged;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        BuildGroups();

    private void ClearGroups()
    {
        if (_groups is null)
            return;

        // Detach removed controls from their generated rows before reuse.
        foreach (var child in _groups.Children)
            if (child is GlassSurface { Child: Panel row })
                row.Children.Clear();
        _groups.Children.Clear();
        _groups.ColumnDefinitions.Clear();
    }

    private void BuildGroups()
    {
        if (_groups is null)
            return;
        ClearGroups();

        var hasSpacer = false;
        foreach (var probe in Items)
        {
            if (probe is ToolbarSpacer)
            {
                hasSpacer = true;
                break;
            }
        }
        _groups.HorizontalAlignment = hasSpacer
            ? Avalonia.Layout.HorizontalAlignment.Stretch
            : Avalonia.Layout.HorizontalAlignment.Center;

        var run = new List<Control>();
        var column = 0;
        void Flush()
        {
            if (run.Count == 0)
                return;
            var row = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                // UIToolbar on iOS 26 uses 48 point actions with a 4 point gap.
                Spacing = 4,
            };
            foreach (var c in run)
                row.Children.Add(c);
            var capsule = new GlassSurface
            {
                MinHeight = 48,
                CornerRadius = new CornerRadius(24),
                BlurRadius = 18,
                GlassThickness = 1,
                Saturation = 1,
                RefractionStrength = 0,
                ChromaticAberration = 0,
                DepthEffect = 0,
                LightIntensity = 0.25,
                FresnelStrength = 0,
                Magnification = 1,
                ShadowOpacity = 0.10,
                ShadowBlur = 24,
                ShadowOffset = 2,
                ShadowContactWeight = 0,
                Child = row,
            };
            capsule.Bind(GlassSurface.TintProperty,
                this.GetResourceObservable("CupertinoBarButtonTint"));
            _groups.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetColumn(capsule, column++);
            _groups.Children.Add(capsule);
            run.Clear();
        }

        foreach (var item in Items)
        {
            if (item is ToolbarSpacer)
            {
                Flush();
                _groups.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 12 });
                column++;
            }
            else if (item is Control c)
            {
                if (c is Button { Theme: null } b
                    && this.TryFindResource("CupertinoToolbarButton", out var t)
                    && t is Avalonia.Styling.ControlTheme ct)
                    b.Theme = ct;
                run.Add(c);
            }
        }
        Flush();
    }
}
