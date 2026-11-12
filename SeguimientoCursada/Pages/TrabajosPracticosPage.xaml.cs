using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class TrabajosPracticosPage : ContentPage
{
    public TrabajosPracticosPage()
    {
        InitializeComponent();
        BindingContext = new TrabajosPracticosViewModel();
    }
}
