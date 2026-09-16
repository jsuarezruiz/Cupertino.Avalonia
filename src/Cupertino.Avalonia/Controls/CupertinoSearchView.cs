using System.Collections;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;

namespace Cupertino.Controls;

/// <summary>
/// Chooses whether the search field is always visible or can collapse.
/// </summary>
public enum CupertinoSearchDisplayMode
{
    /// <summary>
    /// The search field stays in the normal layout.
    /// </summary>
    Inline,
    /// <summary>
    /// A compact button can expand into the search field.
    /// </summary>
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
    /// <summary>
    /// Identifies the <see cref="ItemsSource"/> property.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<CupertinoSearchView, IEnumerable?>(nameof(ItemsSource));

    /// <summary>
    /// Identifies the <see cref="Text"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CupertinoSearchView, string?>(
            nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="Scopes"/> property.
    /// </summary>
    public static readonly StyledProperty<IList<string>?> ScopesProperty =
        AvaloniaProperty.Register<CupertinoSearchView, IList<string>?>(nameof(Scopes));

    /// <summary>
    /// Identifies the <see cref="SelectedScopeIndex"/> property.
    /// </summary>
    public static readonly StyledProperty<int> SelectedScopeIndexProperty =
        AvaloniaProperty.Register<CupertinoSearchView, int>(
            nameof(SelectedScopeIndex), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="SelectedItem"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<CupertinoSearchView, object?>(
            nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="IsExpanded"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CupertinoSearchView, bool>(
            nameof(IsExpanded), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="DisplayMode"/> property.
    /// </summary>
    public static readonly StyledProperty<CupertinoSearchDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<CupertinoSearchView, CupertinoSearchDisplayMode>(nameof(DisplayMode));

    /// <summary>
    /// Identifies the <see cref="ItemTemplate"/> property.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<CupertinoSearchView, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>
    /// Identifies the <see cref="EmptyContent"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<CupertinoSearchView, object?>(nameof(EmptyContent));

    private readonly ObservableCollection<object> _filteredItems = new();
    private IEnumerable? _filteredSource;
    private readonly List<(object? Value, bool Matches)> _sourceEntries = new();
    /// <summary>
    /// Identifies the <see cref="FilteredItems"/> property.
    /// </summary>
    public static readonly DirectProperty<CupertinoSearchView, IReadOnlyList<object>> FilteredItemsProperty =
        AvaloniaProperty.RegisterDirect<CupertinoSearchView, IReadOnlyList<object>>(
            nameof(FilteredItems), o => o.FilteredItems);

    /// <summary>
    /// The searchable source. Observable add, remove, replace, and move notifications update results incrementally while attached.
    /// </summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    /// <summary>
    /// The search query. Changes synchronously filter the source on the UI thread using a trimmed query.
    /// </summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>
    /// Optional scope labels. Observable changes update the scope selector while attached.
    /// </summary>
    public IList<string>? Scopes { get => GetValue(ScopesProperty); set => SetValue(ScopesProperty, value); }
    /// <summary>
    /// The selected scope index, clamped to the available labels; zero when no scopes exist.
    /// </summary>
    public int SelectedScopeIndex { get => GetValue(SelectedScopeIndexProperty); set => SetValue(SelectedScopeIndexProperty, value); }
    /// <summary>
    /// The selected result item, synchronized with the results list.
    /// </summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    /// <summary>
    /// Whether the search field is expanded in collapsible mode.
    /// </summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    /// <summary>
    /// Chooses an always-visible search field or a field that can collapse to a button.
    /// </summary>
    public CupertinoSearchDisplayMode DisplayMode { get => GetValue(DisplayModeProperty); set => SetValue(DisplayModeProperty, value); }
    /// <summary>
    /// The template used for individual filtered results.
    /// </summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
    /// <summary>
    /// Content displayed when no results match the current query and scope; null shows the CupertinoSearchEmptyText resource.
    /// </summary>
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    /// <summary>
    /// The current results in source order, including duplicates. The collection instance is stable; query, scope and source changes update it in place.
    /// </summary>
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

    /// <summary>
    /// Raised when Enter is pressed in the search field.
    /// </summary>
    public event EventHandler? SearchSubmitted;

    private TextBox? _field;
    private TabStrip? _scopeStrip;
    private ListBox? _results;
    private ContentPresenter? _empty;
    private IDisposable? _emptyBinding;
    private INotifyCollectionChanged? _observableSource;
    private INotifyCollectionChanged? _observableScopes;
    private bool _isAttached;
    private bool _syncing;
    private int _scopeSynchronizationGeneration;

    /// <summary>
    /// Creates a CupertinoSearchView with its default settings.
    /// </summary>
    public CupertinoSearchView()
    {
        UpdatePseudoClasses();
    }

    /// <inheritdoc/>
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
        _empty = e.NameScope.Find<ContentPresenter>("PART_Empty");
        UpdateEmptyContent();

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

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        ConnectSource();
        ConnectScopes();
        SynchronizeScopes();
        ApplyFilter();
    }

    /// <inheritdoc/>
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

    /// <summary>
    /// Clears the query and collapses the field when DisplayMode is Collapsible.
    /// </summary>
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

    /// <inheritdoc/>
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
            if (change.Property == IsExpandedProperty && IsExpanded)
                FocusField();
        }
        else if (change.Property == ItemTemplateProperty && _results is not null)
            _results.ItemTemplate = ItemTemplate;
        else if (change.Property == EmptyContentProperty)
            UpdateEmptyContent();
        else if (change.Property == SelectedItemProperty && _results is not null && !_syncing)
            _results.SelectedItem = SelectedItem;
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset ||
            (e.OldItems is not null && e.OldStartingIndex < 0) ||
            (e.NewItems is not null && e.NewStartingIndex < 0))
        {
            ApplyFilter();
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Move && e.OldItems is { } movedItems)
        {
            MoveSourceEntries(e.OldStartingIndex, e.NewStartingIndex, movedItems.Count);
            return;
        }

        var selected = SelectedItem;
        var selectionWasRemoved = selected is not null && e.OldItems is { } removedItems
            && IndexOfSelectedInstance(removedItems, selected) >= 0;
        var wasSyncing = _syncing;
        if (selectionWasRemoved)
            _syncing = true;
        try
        {
            if (e.OldItems is { } oldItems)
            {
                var start = e.OldStartingIndex;
                var filteredIndex = FilteredIndex(start);
                for (var i = 0; i < oldItems.Count; ++i)
                    if (_sourceEntries[start + i].Matches)
                        _filteredItems.RemoveAt(filteredIndex);
                _sourceEntries.RemoveRange(start, oldItems.Count);
            }
            if (e.NewItems is { } newItems)
            {
                var start = e.NewStartingIndex;
                var filteredIndex = FilteredIndex(start);
                var query = (Text ?? string.Empty).Trim();
                for (var i = 0; i < newItems.Count; ++i)
                {
                    var entry = CreateEntry(newItems[i], query);
                    _sourceEntries.Insert(start + i, entry);
                    if (entry.Matches)
                        _filteredItems.Insert(filteredIndex++, entry.Value!);
                }
            }
            if (selectionWasRemoved)
            {
                // Replace can swap in an equal instance; select the one present.
                if (TryFindFilteredSelection(selected!, out var replacement))
                    SetCurrentValue(SelectedItemProperty, replacement);
                else
                    SetCurrentValue(SelectedItemProperty, null);
                if (_results is not null)
                    _results.SelectedItem = SelectedItem;
            }
        }
        finally
        {
            _syncing = wasSyncing;
        }
        PseudoClasses.Set(":empty", _filteredItems.Count == 0);
    }

    private void MoveSourceEntries(int oldIndex, int newIndex, int count)
    {
        var oldFilteredIndex = FilteredIndex(oldIndex);
        var moved = _sourceEntries.GetRange(oldIndex, count);
        var matchingCount = moved.Count(entry => entry.Matches);
        _sourceEntries.RemoveRange(oldIndex, count);
        // The filtered collection still contains the moved entries at this point.
        var newFilteredIndex = newIndex == _sourceEntries.Count
            ? _filteredItems.Count - matchingCount
            : FilteredIndex(newIndex);
        _sourceEntries.InsertRange(newIndex, moved);

        // Preserve collection identity and selection by reporting moves, rather
        // than temporarily removing selected items from the results list.
        var wasSyncing = _syncing;
        _syncing = true;
        try
        {
            if (oldFilteredIndex < newFilteredIndex)
            {
                for (var i = matchingCount - 1; i >= 0; --i)
                    _filteredItems.Move(oldFilteredIndex + i, newFilteredIndex + i);
            }
            else if (oldFilteredIndex > newFilteredIndex)
            {
                for (var i = 0; i < matchingCount; ++i)
                    _filteredItems.Move(oldFilteredIndex + i, newFilteredIndex + i);
            }
            // Avalonia can clear list selection while processing a move.
            if (_results is not null)
                _results.SelectedItem = SelectedItem;
        }
        finally
        {
            _syncing = wasSyncing;
        }
    }

    private int FilteredIndex(int sourceIndex)
    {
        if (sourceIndex == _sourceEntries.Count)
            return _filteredItems.Count;
        var count = 0;
        for (var i = 0; i < sourceIndex; ++i)
            if (_sourceEntries[i].Matches)
                ++count;
        return count;
    }

    private static int IndexOfSelectedInstance(System.Collections.IList items, object selected)
    {
        for (var i = 0; i < items.Count; ++i)
            if (ReferenceEquals(items[i], selected))
                return i;
        if (selected is ValueType)
            for (var i = 0; i < items.Count; ++i)
                if (Equals(items[i], selected))
                    return i;
        return -1;
    }

    private bool TryFindFilteredSelection(object selected, out object? replacement)
    {
        foreach (var item in _filteredItems)
        {
            if (ReferenceEquals(item, selected))
            {
                replacement = item;
                return true;
            }
        }
        foreach (var item in _filteredItems)
        {
            if (Equals(item, selected))
            {
                replacement = item;
                return true;
            }
        }
        replacement = null;
        return false;
    }

    private (object? Value, bool Matches) CreateEntry(object? value, string query) =>
        (value, value is not null && (Filter?.Invoke(value, query, SelectedScopeIndex) ?? DefaultMatch(value, query)));

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

    /// <summary>
    /// Synchronously re-evaluates the entire source. Use after changing item search text or a non-observable collection.
    /// </summary>
    public void Refresh() => ApplyFilter();

    private void ApplyFilter()
    {
        var query = (Text ?? string.Empty).Trim();
        var values = new List<object?>();
        if (ItemsSource is not null)
            foreach (var value in ItemsSource)
                values.Add(value);

        var selected = SelectedItem;
        var wasSyncing = _syncing;
        _syncing = true;
        try
        {
            if (ReferenceEquals(_filteredSource, ItemsSource) && SameValues(values))
                UpdateMatchesInPlace(values, query);
            else
                RebuildEntries(values, query);
            _filteredSource = ItemsSource;

            if (selected is not null)
            {
                if (TryFindFilteredSelection(selected, out var replacement))
                {
                    if (!ReferenceEquals(selected, replacement))
                        SetCurrentValue(SelectedItemProperty, replacement);
                }
                else
                    SetCurrentValue(SelectedItemProperty, null);
            }
            if (_results is not null)
            {
                if (!ReferenceEquals(_results.ItemsSource, _filteredItems))
                    _results.ItemsSource = _filteredItems;
                _results.SelectedItem = SelectedItem;
            }
        }
        finally
        {
            _syncing = wasSyncing;
        }
        PseudoClasses.Set(":empty", _filteredItems.Count == 0);
    }

    private bool SameValues(List<object?> values)
    {
        if (values.Count != _sourceEntries.Count)
            return false;
        for (var i = 0; i < values.Count; ++i)
            if (!Equals(values[i], _sourceEntries[i].Value))
                return false;
        return true;
    }

    // Re-evaluate every entry and apply only the match transitions to the results.
    private void UpdateMatchesInPlace(List<object?> values, string query)
    {
        var filteredIndex = 0;
        for (var i = 0; i < values.Count; ++i)
        {
            var previous = _sourceEntries[i];
            var wasMatch = previous.Matches;
            var entry = CreateEntry(values[i], query);
            _sourceEntries[i] = entry;
            if (wasMatch && !entry.Matches)
                _filteredItems.RemoveAt(filteredIndex);
            else if (!wasMatch && entry.Matches)
                _filteredItems.Insert(filteredIndex++, entry.Value!);
            else if (entry.Matches)
            {
                // Equal but a different reference: swap in the new instance.
                if (!ReferenceEquals(previous.Value, entry.Value)
                    && previous.Value is not ValueType && entry.Value is not ValueType)
                {
                    _filteredItems[filteredIndex] = entry.Value!;
                    if (ReferenceEquals(SelectedItem, previous.Value))
                        SetCurrentValue(SelectedItemProperty, entry.Value);
                }
                ++filteredIndex;
            }
        }
    }

    private void RebuildEntries(List<object?> values, string query)
    {
        _sourceEntries.Clear();
        _filteredItems.Clear();
        foreach (var value in values)
        {
            var entry = CreateEntry(value, query);
            _sourceEntries.Add(entry);
            if (entry.Matches)
                _filteredItems.Add(value!);
        }
    }

    private bool DefaultMatch(object item, string query)
    {
        if (query.Length == 0)
            return true;
        var text = SearchTextSelector is { } selector ? selector(item) : item.ToString();
        return text?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;
    }

    private void UpdateEmptyContent()
    {
        _emptyBinding?.Dispose();
        _emptyBinding = null;
        if (_empty is null)
            return;
        if (EmptyContent is { } content)
            _empty.Content = content;
        else
            _emptyBinding = _empty.Bind(ContentPresenter.ContentProperty,
                                        _empty.GetResourceObservable("CupertinoSearchEmptyText"));
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":expanded", IsExpanded);
        PseudoClasses.Set(":collapsible", DisplayMode == CupertinoSearchDisplayMode.Collapsible);
        PseudoClasses.Set(":scopes", Scopes is { Count: > 0 });
    }
}
