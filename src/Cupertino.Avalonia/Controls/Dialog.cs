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
    /// <summary>
    /// A regular action.
    /// </summary>
    Default,
    /// <summary>
    /// The action returned by Escape dismissal.
    /// </summary>
    Cancel,
    /// <summary>
    /// An action with destructive styling.
    /// </summary>
    Destructive,
    /// <summary>
    /// An emphasized action with initial keyboard focus priority.
    /// </summary>
    Preferred,
}

/// <summary>
/// One button in a <see cref="Dialog"/>.
/// </summary>
public class DialogAction
{
    /// <summary>
    /// Creates an action with its label and semantic role.
    /// </summary>
    public DialogAction(string title, DialogActionRole role = DialogActionRole.Default)
    {
        Title = title;
        Role = role;
    }

    /// <summary>
    /// The primary text displayed by this control.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// The action role used for appearance, initial focus preference, and Escape cancellation.
    /// </summary>
    public DialogActionRole Role { get; }

    /// <summary>
    /// Whether Role is Destructive.
    /// </summary>
    public bool IsDestructive => Role == DialogActionRole.Destructive;

    /// <summary>
    /// Whether Role is Cancel.
    /// </summary>
    public bool IsCancel => Role == DialogActionRole.Cancel;

    /// <summary>
    /// Whether Role is Preferred.
    /// </summary>
    public bool IsPreferred => Role == DialogActionRole.Preferred;
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

    /// <summary>
    /// Identifies the <see cref="Title"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Dialog, string?>(nameof(Title));

    /// <summary>
    /// Identifies the <see cref="Message"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<Dialog, string?>(nameof(Message));

    /// <summary>
    /// Identifies the <see cref="ActionsLayout"/> property.
    /// </summary>
    public static readonly StyledProperty<DialogActionsLayout> ActionsLayoutProperty =
        AvaloniaProperty.Register<Dialog, DialogActionsLayout>(nameof(ActionsLayout));

    /// <summary>
    /// Identifies the <see cref="Actions"/> property.
    /// </summary>
    public static readonly StyledProperty<IList<DialogAction>> ActionsProperty =
        AvaloniaProperty.Register<Dialog, IList<DialogAction>>(
            nameof(Actions), defaultValue: Array.Empty<DialogAction>());

    /// <summary>
    /// The primary text displayed by this control.
    /// </summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Optional message below the dialog title.
    /// </summary>
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

    /// <summary>
    /// The displayed actions. Replacing the list updates the buttons; in-place updates require an observable collection.
    /// </summary>
    public IList<DialogAction> Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>
    /// Gets the action command.
    /// </summary>
    public System.Windows.Input.ICommand InvokeActionCommand { get; }

    /// <summary>
    /// Creates a Dialog with its default settings.
    /// </summary>
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

    /// <summary>
    /// Shows this dialog in the owner’s overlay and returns the selected action, or null when dismissed. Throws if this instance is already showing.
    /// </summary>
    public Task<DialogAction?> ShowAsync(Visual owner)
    {
        if (_completion is not null)
            throw new InvalidOperationException("This dialog is already being shown.");

        RemovePendingHost();
        _previousFocus = TopLevel.GetTopLevel(owner)?.FocusManager?.GetFocusedElement();
        _layer = OverlayLayer.GetOverlayLayer(owner)
            ?? throw new InvalidOperationException("The visual is not in a window that has an overlay layer.");

        CupertinoAccessibility.Changed += OnAccessibilityChanged;
        _completion = new TaskCompletionSource<DialogAction?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _scrim = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
            Opacity = CupertinoAccessibility.ReduceMotion ? 1 : 0,
            Transitions = [new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = CupertinoAccessibility.ReduceMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(200),
            }],
        };
        _scrim.PointerPressed += (_, _) => Close(null);

        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Opacity = CupertinoAccessibility.ReduceMotion ? 1 : 0;
        RenderTransform = TransformOperations.Parse(CupertinoAccessibility.ReduceMotion ? "scale(1)" : "scale(1.133)");
        Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = CupertinoAccessibility.ReduceMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(150) },
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = CupertinoAccessibility.ReduceMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(280),
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
                                button.DataContext is DialogAction { Role: DialogActionRole.Preferred })
                            ?? buttons.FirstOrDefault(button =>
                                button.DataContext is DialogAction { Role: DialogActionRole.Default })
                            ?? buttons.FirstOrDefault(button =>
                                button.DataContext is DialogAction { Role: DialogActionRole.Cancel })
                            ?? buttons.FirstOrDefault();
            if (preferred?.Focus() != true)
                Focus();
        }, TimeSpan.FromMilliseconds(16));

        return _completion.Task;
    }

    /// <summary>
    /// Completes the dialog result and dismisses its host. Passing null represents dismissal; calling on a closed dialog has no effect.
    /// </summary>
    public void Close(DialogAction? result)
    {
        if (_completion is null || _layer is null)
            return;

        var layer = _layer;
        var host = _host;
        var scrim = _scrim;
        var completion = _completion;
        var previousFocus = _previousFocus;
        CupertinoAccessibility.Changed -= OnAccessibilityChanged;
        _completion = null;
        _layer = null;
        _scrim = null;
        _previousFocus = null;
        _sizeSubscription?.Dispose();
        _sizeSubscription = null;

        Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = CupertinoAccessibility.ReduceMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(120) },
        ];
        Opacity = 0;
        if (scrim is not null)
            scrim.Opacity = 0;

        void FinishClose()
        {
            var restoreFocus = IsFocusWithin(host, previousFocus);
            RemoveHost(host, layer);
            if (restoreFocus)
                RestoreFocus(previousFocus);
        }
        if (CupertinoAccessibility.ReduceMotion)
            FinishClose();
        else
            DispatcherTimer.RunOnce(FinishClose, TimeSpan.FromMilliseconds(150));

        completion.TrySetResult(result);
    }

    private void OnAccessibilityChanged(object? sender, EventArgs e)
    {
        if (!CupertinoAccessibility.ReduceMotion || _completion is null)
            return;
        Transitions = null;
        Opacity = 1;
        RenderTransform = TransformOperations.Parse("scale(1)");
        if (_scrim is not null)
        {
            _scrim.Transitions = null;
            _scrim.Opacity = 1;
        }
    }

    private void OnHostDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Panel host || !ReferenceEquals(_host, host) || _completion is null)
            return;

        CupertinoAccessibility.Changed -= OnAccessibilityChanged;
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

    private ItemsControl? _actionsHost;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActionsLayoutProperty)
            PseudoClasses.Set(":stacked", ActionsLayout == DialogActionsLayout.Stack);
        else if (change.Property == ActionsProperty && _actionsHost is not null)
            _actionsHost.ItemsSource = Actions;
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        PseudoClasses.Set(":stacked", ActionsLayout == DialogActionsLayout.Stack);

        _actionsHost = e.NameScope.Find<ItemsControl>("PART_Actions");
        if (_actionsHost is not null)
            _actionsHost.ItemsSource = Actions;
    }

    /// <inheritdoc/>
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
