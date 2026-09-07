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

        // Embedded toolbars use wider spacing between adjacent actions.
        var isEmbedded = Classes.Contains("embedded");
        var run = new List<Control>();
        var column = 0;
        void Flush()
        {
            if (run.Count == 0)
                return;
            var row = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = isEmbedded && run.Count > 1 ? 10.5 : 0,
            };
            foreach (var c in run)
                row.Children.Add(c);
            var capsule = new GlassSurface
            {
                Height = 40,
                CornerRadius = new CornerRadius(20),
                BlurRadius = 18,
                GlassThickness = 0,
                Saturation = 1,
                RefractionStrength = 0,
                ChromaticAberration = 0,
                DepthEffect = 0,
                LightIntensity = 0,
                FresnelStrength = 0,
                Magnification = 1,
                ShadowOpacity = 0.10,
                ShadowBlur = 12,
                ShadowOffset = 2,
                ShadowContactWeight = 0.2,
                MinWidth = isEmbedded ? (run.Count > 1 ? 99.3 : 48) : 0,
                RenderTransform = isEmbedded && column == 0 && run.Count > 1
                    ? new TranslateTransform(-5.35, 0)
                    : null,
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
                _groups.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
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
