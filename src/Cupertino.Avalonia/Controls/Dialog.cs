using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Reactive;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Animation;

namespace Cupertino.Controls;

/// <summary>
/// How a dialog arranges its actions.
/// </summary>
public enum DialogActionsLayout
{
    /// <summary>
    /// Side by side, as an alert does with two actions.
    /// </summary>
    Row,

    /// <summary>
    /// Stacked, as an action sheet does.
    /// </summary>
    Stack,
}

/// <summary>
/// Dialog action roles.
/// </summary>
public enum DialogActionRole
{
    Default,
    Cancel,
    Destructive,
}

/// <summary>
/// One button in a <see cref="Dialog"/>.
/// </summary>
public class DialogAction
{
    public DialogAction(string title, DialogActionRole role = DialogActionRole.Default)
    {
        Title = title;
        Role = role;
    }

    public string Title { get; }

    public DialogActionRole Role { get; }

    public bool IsDestructive => Role == DialogActionRole.Destructive;

    public bool IsCancel => Role == DialogActionRole.Cancel;
}

internal sealed class ActionCommand : System.Windows.Input.ICommand
{
    private readonly Action<object?> _execute;

    public ActionCommand(Action<object?> execute) => _execute = execute;

    // Never raised: the command is always executable.
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute(parameter);
}

/// <summary>
/// Presents alerts and action sheets in the window overlay layer.
/// </summary>
public class Dialog : ContentControl
{
    private TaskCompletionSource<DialogAction?>? _completion;
    private Panel? _host;
    private Control? _scrim;
    private OverlayLayer? _layer;
    private IDisposable? _sizeSubscription;
    private IInputElement? _previousFocus;

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Dialog, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<Dialog, string?>(nameof(Message));

    public static readonly StyledProperty<DialogActionsLayout> ActionsLayoutProperty =
        AvaloniaProperty.Register<Dialog, DialogActionsLayout>(nameof(ActionsLayout));

    public static readonly StyledProperty<IList<DialogAction>> ActionsProperty =
        AvaloniaProperty.Register<Dialog, IList<DialogAction>>(
            nameof(Actions), defaultValue: Array.Empty<DialogAction>());

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// Gets or sets the action layout.
    /// </summary>
    public DialogActionsLayout ActionsLayout
    {
        get => GetValue(ActionsLayoutProperty);
        set => SetValue(ActionsLayoutProperty, value);
    }

    public IList<DialogAction> Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>
    /// Gets the action command.
    /// </summary>
    public System.Windows.Input.ICommand InvokeActionCommand { get; }

    public Dialog()
    {
        Focusable = true;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        SetCurrentValue(ActionsProperty, new List<DialogAction>());
        InvokeActionCommand = new ActionCommand(p =>
        {
            if (p is DialogAction action)
                Close(action);
        });
    }

    /// <summary>
    /// Shows an alert and returns the selected action.
    /// </summary>
    public static Task<DialogAction?> ShowAsync(
        Visual owner,
        string title,
        string? message = null,
        params DialogAction[] actions)
    {
        var dialog = new Dialog
        {
            Title = title,
            Message = message,
            Actions = actions.Length > 0
                ? actions
                : new[] { new DialogAction("OK") },
            // Stack three or more actions to preserve label space.
            ActionsLayout = actions.Length > 2
                ? DialogActionsLayout.Stack
                : DialogActionsLayout.Row,
        };
        return dialog.ShowAsync(owner);
    }

    /// <summary>
    /// Shows an action sheet and returns the selected action.
    /// </summary>
    public static Task<DialogAction?> ShowSheetAsync(
        Visual owner,
        string title,
        string? message = null,
        params DialogAction[] actions)
    {
        var dialog = new Dialog
        {
            Title = title,
            Message = message,
            Actions = actions,
            ActionsLayout = DialogActionsLayout.Stack,
            Width = 240,
        };
        return dialog.ShowAsync(owner);
    }

    public Task<DialogAction?> ShowAsync(Visual owner)
    {
        if (_completion is not null)
            throw new InvalidOperationException("This dialog is already being shown.");

        RemovePendingHost();
        _previousFocus = TopLevel.GetTopLevel(owner)?.FocusManager?.GetFocusedElement();
        _layer = OverlayLayer.GetOverlayLayer(owner)
            ?? throw new InvalidOperationException("The visual is not in a window that has an overlay layer.");

        _completion = new TaskCompletionSource<DialogAction?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _scrim = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
            Opacity = 0,
            Transitions = [new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(200),
            }],
        };
        _scrim.PointerPressed += (_, _) => Close(null);

        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Opacity = 0;
        RenderTransform = TransformOperations.Parse("scale(1.133)");
        Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(150) },
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(280),
                Easing = new CriticallyDampedEasing { OmegaDuration = 8.4 },
            },
        ];

        // OverlayLayer does not stretch or align children.
        _host = new Panel();
        _host.DetachedFromVisualTree += OnHostDetached;
        _host.Children.Add(_scrim);
        _host.Children.Add(this);

        var layer = _layer;
        _sizeSubscription = layer.GetObservable(BoundsProperty).Subscribe(
            new AnonymousObserver<Rect>(b =>
            {
                if (_host is null)
                    return;
                _host.Width = b.Width;
                _host.Height = b.Height;
            }));

        layer.Children.Add(_host);

        // Avoid Render priority, which live glass can starve.
        var scrim = _scrim;
        DispatcherTimer.RunOnce(() =>
        {
            if (_completion is null || !ReferenceEquals(_scrim, scrim))
                return;
            scrim.Opacity = 1;
            Opacity = 1;
            RenderTransform = TransformOperations.Parse("scale(1)");

            var buttons = this.GetVisualDescendants().OfType<Button>().ToList();
            var preferred = buttons.FirstOrDefault(button =>
                                button.DataContext is DialogAction { Role: DialogActionRole.Default })
                            ?? buttons.FirstOrDefault(button =>
                                button.DataContext is DialogAction { Role: DialogActionRole.Cancel })
                            ?? buttons.FirstOrDefault();
            if (preferred?.Focus() != true)
                Focus();
        }, TimeSpan.FromMilliseconds(16));

        return _completion.Task;
    }

    public void Close(DialogAction? result)
    {
        if (_completion is null || _layer is null)
            return;

        var layer = _layer;
        var host = _host;
        var scrim = _scrim;
        var completion = _completion;
        var previousFocus = _previousFocus;
        _completion = null;
        _layer = null;
        _scrim = null;
        _previousFocus = null;
        _sizeSubscription?.Dispose();
        _sizeSubscription = null;

        Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(120) },
        ];
        Opacity = 0;
        if (scrim is not null)
            scrim.Opacity = 0;

        DispatcherTimer.RunOnce(() =>
        {
            var restoreFocus = IsFocusWithin(host, previousFocus);
            RemoveHost(host, layer);
            if (restoreFocus)
                RestoreFocus(previousFocus);
        }, TimeSpan.FromMilliseconds(150));

        completion.TrySetResult(result);
    }

    private void OnHostDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Panel host || !ReferenceEquals(_host, host) || _completion is null)
            return;

        host.DetachedFromVisualTree -= OnHostDetached;
        var previousFocus = _previousFocus;
        var restoreFocus = IsFocusWithin(host, previousFocus);
        host.Children.Clear();
        _sizeSubscription?.Dispose();
        _sizeSubscription = null;
        _layer = null;
        _host = null;
        _scrim = null;
        _previousFocus = null;
        var completion = _completion;
        _completion = null;
        completion.TrySetResult(null);
        if (restoreFocus)
            RestoreFocus(previousFocus);
    }

    private void RemovePendingHost()
    {
        if (_host is not { } host)
            return;
        RemoveHost(host, host.GetVisualParent() as OverlayLayer);
    }

    private void RemoveHost(Panel? host, OverlayLayer? layer)
    {
        if (host is null)
            return;
        host.DetachedFromVisualTree -= OnHostDetached;
        host.Children.Clear();
        layer?.Children.Remove(host);
        if (ReferenceEquals(_host, host))
            _host = null;
    }

    private static bool ContainsVisual(Visual ancestor, Visual visual)
    {
        for (Visual? current = visual; current is not null; current = current.GetVisualParent())
            if (ReferenceEquals(current, ancestor))
                return true;
        return false;
    }

    private static bool IsFocusWithin(Panel? host, IInputElement? previousFocus)
    {
        if (host is null)
            return false;
        var topLevel = TopLevel.GetTopLevel(host)
                       ?? (previousFocus as Visual is { } previousVisual
                           ? TopLevel.GetTopLevel(previousVisual)
                           : null);
        return topLevel?.FocusManager?.GetFocusedElement() is Visual focused
               && ContainsVisual(host, focused);
    }

    private static void RestoreFocus(IInputElement? previousFocus)
    {
        if (previousFocus is not Visual visual || TopLevel.GetTopLevel(visual)?.FocusManager is not { } manager)
            return;
        manager.Focus(previousFocus, NavigationMethod.Unspecified, KeyModifiers.None);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActionsLayoutProperty)
            PseudoClasses.Set(":stacked", ActionsLayout == DialogActionsLayout.Stack);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        PseudoClasses.Set(":stacked", ActionsLayout == DialogActionsLayout.Stack);

        if (e.NameScope.Find<ItemsControl>("PART_Actions") is { } items)
            items.ItemsSource = Actions;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close(Actions.FirstOrDefault(a => a.Role == DialogActionRole.Cancel));
            e.Handled = true;
        }
    }

}
