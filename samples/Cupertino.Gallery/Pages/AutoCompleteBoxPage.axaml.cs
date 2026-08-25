using Avalonia.Controls;

namespace Cupertino.Gallery.Pages;

public partial class AutoCompleteBoxPage : UserControl, IGalleryCaptureState
{
    private static readonly string[] FruitNames =
    {
        "Apple", "Apricot", "Banana", "Blackberry", "Blueberry",
        "Cherry", "Fig", "Grape", "Mango", "Peach", "Pear", "Plum",
    };

    public AutoCompleteBoxPage()
    {
        InitializeComponent();
        this.FindControl<AutoCompleteBox>("Fruits")!.ItemsSource = FruitNames;
        this.FindControl<AutoCompleteBox>("FruitsContains")!.ItemsSource = FruitNames;
        this.FindControl<AutoCompleteBox>("FruitsMin")!.ItemsSource = FruitNames;
    }

    public void ApplyGalleryCaptureState()
    {
        var fruits = this.FindControl<AutoCompleteBox>("Fruits")!;
        fruits.Text = "Ap";
        fruits.IsDropDownOpen = true;
    }
}
