using Avalonia.Controls;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

public partial class ListAndSearchPage : UserControl
{
    public ListAndSearchPage()
    {
        InitializeComponent();
        var search = this.FindControl<CupertinoSearchController>("SearchDemo")!;
        search.Scopes = ["All", "People", "Files"];
        search.ItemsSource = new[]
        {
            "Ada Lovelace", "Design notes", "Grace Hopper", "Release plan",
            "Alan Turing", "Project brief", "Katherine Johnson",
        };
    }
}
