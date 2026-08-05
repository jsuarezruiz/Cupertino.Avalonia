using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cupertino.Gallery.Pages;

public partial class CarouselPage : UserControl
{
    public CarouselPage()
    {
        InitializeComponent();
    }

    private void OnPrevious(object? sender, RoutedEventArgs e) =>
        this.FindControl<Carousel>("Pager")!.Previous();

    private void OnNext(object? sender, RoutedEventArgs e) =>
        this.FindControl<Carousel>("Pager")!.Next();

    private void OnPreviousCards(object? sender, RoutedEventArgs e) =>
        this.FindControl<Carousel>("Cards")!.Previous();

    private void OnNextCards(object? sender, RoutedEventArgs e) =>
        this.FindControl<Carousel>("Cards")!.Next();
}
