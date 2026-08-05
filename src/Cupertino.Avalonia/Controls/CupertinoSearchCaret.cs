using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// Paints a custom search-field caret.
/// </summary>
public sealed class CupertinoSearchCaret : Control
{
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<CupertinoSearchCaret, IBrush?>(nameof(Brush));

    public static readonly StyledProperty<double> CaretWidthProperty =
        AvaloniaProperty.Register<CupertinoSearchCaret, double>(nameof(CaretWidth), 2);

    public static readonly StyledProperty<double> CaretHeightProperty =
        AvaloniaProperty.Register<CupertinoSearchCaret, double>(nameof(CaretHeight), 23);

    private readonly DispatcherTimer _blinkTimer;
    private TextPresenter? _presenter;
    private TextBox? _textBox;
    private bool _blinkOn;
    private bool _invalidateQueued;

    public CupertinoSearchCaret()
    {
        _blinkTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(500),
            DispatcherPriority.Render,
            (_, _) =>
            {
                _blinkOn = !_blinkOn;
                Opacity = _blinkOn ? 1 : 0;
            });

        Transitions =
        [
            new Avalonia.Animation.DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(150),
            },
        ];
    }

    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    public double CaretWidth
    {
        get => GetValue(CaretWidthProperty);
        set => SetValue(CaretWidthProperty, value);
    }

    public double CaretHeight
    {
        get => GetValue(CaretHeightProperty);
        set => SetValue(CaretHeightProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _presenter = (this.GetVisualParent() as Panel)?.Children.OfType<TextPresenter>().FirstOrDefault();
        _textBox = this.GetVisualAncestors().OfType<TextBox>().FirstOrDefault();
        if (_presenter is not null)
            _presenter.CaretBoundsChanged += OnCaretChanged;
        if (_textBox is not null)
        {
            _textBox.GotFocus += OnFocusChanged;
            _textBox.LostFocus += OnFocusChanged;
            _textBox.PropertyChanged += OnTextBoxPropertyChanged;
        }

        UpdateBlinking();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _blinkTimer.Stop();
        if (_presenter is not null)
            _presenter.CaretBoundsChanged -= OnCaretChanged;
        if (_textBox is not null)
        {
            _textBox.GotFocus -= OnFocusChanged;
            _textBox.LostFocus -= OnFocusChanged;
            _textBox.PropertyChanged -= OnTextBoxPropertyChanged;
        }
        _presenter = null;
        _textBox = null;

        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!_blinkOn || Brush is null || _presenter is null || _textBox is null ||
            !_textBox.IsFocused || _textBox.SelectionStart != _textBox.SelectionEnd)
            return;

        var caret = _presenter.TextLayout.HitTestTextPosition(_textBox.CaretIndex);
        var width = Math.Max(0, CaretWidth);
        var height = Math.Min(Bounds.Height, Math.Max(0, CaretHeight));
        var x = Math.Floor(caret.X);
        if (caret.X > 0 && _textBox.CaretIndex == (_textBox.Text?.Length ?? 0))
            x -= 1;
        var y = (Bounds.Height - height) / 2;
        var rect = new Rect(x, y, width, height);
        context.DrawRectangle(Brush, null, new RoundedRect(rect, width / 2));
    }

    private void OnCaretChanged(object? sender, EventArgs e) => ShowNow();

    private void OnFocusChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        UpdateBlinking();

    private void OnTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty || e.Property == TextBox.CaretIndexProperty ||
            e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty)
            ShowNow();
    }

    private void UpdateBlinking()
    {
        if (_textBox?.IsFocused == true && _textBox.SelectionStart == _textBox.SelectionEnd)
        {
            ShowNow();
            return;
        }

        _blinkTimer.Stop();
        _blinkOn = false;
        Opacity = 0;
        QueueInvalidate();
    }

    private void ShowNow()
    {
        if (_textBox?.IsFocused != true || _textBox.SelectionStart != _textBox.SelectionEnd)
        {
            UpdateBlinking();
            return;
        }

        _blinkTimer.Stop();
        _blinkOn = true;
        Opacity = 1;
        QueueInvalidate();
        _blinkTimer.Start();
    }

    private void QueueInvalidate()
    {
        // Defer invalidation outside TextPresenter rendering.
        if (_invalidateQueued)
            return;
        _invalidateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _invalidateQueued = false;
            InvalidateVisual();
        }, DispatcherPriority.Render);
    }
}
