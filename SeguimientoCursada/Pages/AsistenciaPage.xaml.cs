using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class AsistenciaPage : ContentPage
{
    public AsistenciaPage()
    {
        InitializeComponent();
        BindingContext = new AsistenciaViewModel();
    }
}
