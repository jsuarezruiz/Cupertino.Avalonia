using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

public enum CupertinoSearchDisplayMode
{
    Inline,
    Collapsible,
}

/// <summary>
/// Provides searchable, optionally scoped results.
/// </summary>
[TemplatePart("PART_CompactButton", typeof(Button))]
[TemplatePart("PART_Field", typeof(TextBox))]
[TemplatePart("PART_CancelButton", typeof(Button))]
[TemplatePart("PART_Scopes", typeof(TabStrip))]
[TemplatePart("PART_Results", typeof(ListBox))]
[PseudoClasses(":expanded", ":collapsible", ":scopes", ":empty")]
public class CupertinoSearchView : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<CupertinoSearchView, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CupertinoSearchView, string?>(
            nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IList<string>?> ScopesProperty =
        AvaloniaProperty.Register<CupertinoSearchView, IList<string>?>(nameof(Scopes));

    public static readonly StyledProperty<int> SelectedScopeIndexProperty =
        AvaloniaProperty.Register<CupertinoSearchView, int>(
            nameof(SelectedScopeIndex), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<CupertinoSearchView, object?>(
            nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CupertinoSearchView, bool>(
            nameof(IsExpanded), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<CupertinoSearchDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<CupertinoSearchView, CupertinoSearchDisplayMode>(nameof(DisplayMode));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<CupertinoSearchView, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<CupertinoSearchView, object?>(nameof(EmptyContent), "No Results");

    private IReadOnlyList<object> _filteredItems = Array.Empty<object>();
    public static readonly DirectProperty<CupertinoSearchView, IReadOnlyList<object>> FilteredItemsProperty =
        AvaloniaProperty.RegisterDirect<CupertinoSearchView, IReadOnlyList<object>>(
            nameof(FilteredItems), o => o.FilteredItems);

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public IList<string>? Scopes { get => GetValue(ScopesProperty); set => SetValue(ScopesProperty, value); }
    public int SelectedScopeIndex { get => GetValue(SelectedScopeIndexProperty); set => SetValue(SelectedScopeIndexProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public CupertinoSearchDisplayMode DisplayMode { get => GetValue(DisplayModeProperty); set => SetValue(DisplayModeProperty, value); }
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    public IReadOnlyList<object> FilteredItems => _filteredItems;

    private Func<object, string, int, bool>? _filter;
    private Func<object, string?>? _searchTextSelector;

    /// <summary>
    /// Gets or sets the custom item filter.
    /// </summary>
    public Func<object, string, int, bool>? Filter
    {
        get => _filter;
        set
        {
            if (ReferenceEquals(_filter, value))
                return;
            _filter = value;
            ApplyFilter();
        }
    }

    /// <summary>
    /// Gets or sets the text selector used by the default filter.
    /// </summary>
    public Func<object, string?>? SearchTextSelector
    {
        get => _searchTextSelector;
        set
        {
            if (ReferenceEquals(_searchTextSelector, value))
                return;
            _searchTextSelector = value;
            ApplyFilter();
        }
    }

    public event EventHandler? SearchSubmitted;

    private TextBox? _field;
    private TabStrip? _scopeStrip;
    private ListBox? _results;
    private INotifyCollectionChanged? _observableSource;
    private INotifyCollectionChanged? _observableScopes;
    private bool _isAttached;
    private bool _syncing;
    private int _scopeSynchronizationGeneration;

    public CupertinoSearchView()
    {
        UpdatePseudoClasses();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_field is not null)
        {
            _field.TextChanged -= OnFieldTextChanged;
            _field.KeyDown -= OnFieldKeyDown;
        }
        if (_scopeStrip is not null)
            _scopeStrip.SelectionChanged -= OnScopeSelectionChanged;
        if (_results is not null)
            _results.SelectionChanged -= OnResultSelectionChanged;

        base.OnApplyTemplate(e);
        _field = e.NameScope.Find<TextBox>("PART_Field");
        _scopeStrip = e.NameScope.Find<TabStrip>("PART_Scopes");
        _results = e.NameScope.Find<ListBox>("PART_Results");

        if (e.NameScope.Find<Button>("PART_CompactButton") is { } compact)
            compact.Click += (_, _) => SetExpanded(true);
        if (e.NameScope.Find<Button>("PART_CancelButton") is { } cancel)
            cancel.Click += (_, _) => Cancel();

        if (_field is not null)
        {
            _field.Text = Text;
            _field.TextChanged += OnFieldTextChanged;
            _field.KeyDown += OnFieldKeyDown;
        }
        if (_scopeStrip is not null)
        {
            _scopeStrip.ItemsSource = Scopes;
            _scopeStrip.SelectedIndex = SelectedScopeIndex;
            _scopeStrip.SelectionChanged += OnScopeSelectionChanged;
        }
        if (_results is not null)
        {
            _results.ItemTemplate = ItemTemplate;
            _results.ItemsSource = _filteredItems;
            _results.SelectedItem = SelectedItem;
            _results.SelectionChanged += OnResultSelectionChanged;
        }

        ApplyFilter();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        ConnectSource();
        ConnectScopes();
        SynchronizeScopes();
        ApplyFilter();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        DisconnectSource();
        DisconnectScopes();
        base.OnDetachedFromVisualTree(e);
    }

    private void SetExpanded(bool expanded)
    {
        SetCurrentValue(IsExpandedProperty, expanded);
        if (expanded)
            FocusField();
    }

    private void FocusField() => Avalonia.Threading.Dispatcher.UIThread.Post(() => _field?.Focus());

    public void Cancel()
    {
        SetCurrentValue(TextProperty, string.Empty);
        if (DisplayMode == CupertinoSearchDisplayMode.Collapsible)
            SetCurrentValue(IsExpandedProperty, false);
    }

    private void OnFieldTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing)
            return;
        SetCurrentValue(TextProperty, _field?.Text);
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        SearchSubmitted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnScopeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _scopeStrip is not null)
            SetCurrentValue(SelectedScopeIndexProperty, Math.Max(0, _scopeStrip.SelectedIndex));
    }

    private void OnResultSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _results is not null)
            SetCurrentValue(SelectedItemProperty, _results.SelectedItem);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            DisconnectSource();
            ConnectSource();
            ApplyFilter();
        }
        else if (change.Property == TextProperty || change.Property == SelectedScopeIndexProperty)
        {
            if (change.Property == SelectedScopeIndexProperty)
            {
                var normalized = NormalizeScopeIndex(SelectedScopeIndex);
                if (normalized != SelectedScopeIndex)
                {
                    SetCurrentValue(SelectedScopeIndexProperty, normalized);
                    return;
                }
            }
            _syncing = true;
            try
            {
                if (_field is not null && _field.Text != Text)
                    _field.Text = Text;
                var scopeIndex = Scopes is { Count: > 0 } ? SelectedScopeIndex : -1;
                if (_scopeStrip is not null && _scopeStrip.SelectedIndex != scopeIndex)
                    _scopeStrip.SelectedIndex = scopeIndex;
            }
            finally { _syncing = false; }
            ApplyFilter();
        }
        else if (change.Property == ScopesProperty)
        {
            DisconnectScopes();
            ConnectScopes();
            if (_scopeStrip is not null)
                _scopeStrip.ItemsSource = Scopes;
            SynchronizeScopes();
        }
        else if (change.Property == IsExpandedProperty || change.Property == DisplayModeProperty)
        {
            UpdatePseudoClasses();
            if (IsExpanded)
                FocusField();
        }
        else if (change.Property == ItemTemplateProperty && _results is not null)
            _results.ItemTemplate = ItemTemplate;
        else if (change.Property == SelectedItemProperty && _results is not null && !_syncing)
            _results.SelectedItem = SelectedItem;
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ApplyFilter();

    private void OnScopesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var generation = ++_scopeSynchronizationGeneration;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (generation == _scopeSynchronizationGeneration)
                SynchronizeScopes();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void ConnectSource()
    {
        if (!_isAttached || _observableSource is not null)
            return;
        _observableSource = ItemsSource as INotifyCollectionChanged;
        if (_observableSource is not null)
            _observableSource.CollectionChanged += OnSourceCollectionChanged;
    }

    private void DisconnectSource()
    {
        if (_observableSource is not null)
            _observableSource.CollectionChanged -= OnSourceCollectionChanged;
        _observableSource = null;
    }

    private void ConnectScopes()
    {
        if (!_isAttached || _observableScopes is not null)
            return;
        _observableScopes = Scopes as INotifyCollectionChanged;
        if (_observableScopes is not null)
            _observableScopes.CollectionChanged += OnScopesCollectionChanged;
    }

    private void DisconnectScopes()
    {
        if (_observableScopes is not null)
            _observableScopes.CollectionChanged -= OnScopesCollectionChanged;
        _observableScopes = null;
    }

    private void SynchronizeScopes()
    {
        var normalized = NormalizeScopeIndex(SelectedScopeIndex);
        if (normalized != SelectedScopeIndex)
        {
            SetCurrentValue(SelectedScopeIndexProperty, normalized);
            return;
        }

        _syncing = true;
        try
        {
            var scopeIndex = Scopes is { Count: > 0 } ? normalized : -1;
            if (_scopeStrip is not null && _scopeStrip.SelectedIndex != scopeIndex)
                _scopeStrip.SelectedIndex = scopeIndex;
        }
        finally { _syncing = false; }

        UpdatePseudoClasses();
        ApplyFilter();
    }

    private int NormalizeScopeIndex(int index) =>
        Scopes is { Count: > 0 } scopes ? Math.Clamp(index, 0, scopes.Count - 1) : 0;

    public void Refresh() => ApplyFilter();

    private void ApplyFilter()
    {
        var oldItems = _filteredItems;
        var filteredItems = new List<object>();
        var query = (Text ?? string.Empty).Trim();
        if (ItemsSource is not null)
        {
            foreach (var value in ItemsSource)
            {
                if (value is null)
                    continue;
                if (Filter?.Invoke(value, query, SelectedScopeIndex) ?? DefaultMatch(value, query))
                    filteredItems.Add(value);
            }
        }

        _filteredItems = filteredItems;
        RaisePropertyChanged(FilteredItemsProperty, oldItems, _filteredItems);
        if (_results is not null)
            _results.ItemsSource = _filteredItems;
        PseudoClasses.Set(":empty", _filteredItems.Count == 0);
    }

    private bool DefaultMatch(object item, string query)
    {
        if (query.Length == 0)
            return true;
        var text = SearchTextSelector is { } selector ? selector(item) : item.ToString();
        return text?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":expanded", IsExpanded);
        PseudoClasses.Set(":collapsible", DisplayMode == CupertinoSearchDisplayMode.Collapsible);
        PseudoClasses.Set(":scopes", Scopes is { Count: > 0 });
    }
}
