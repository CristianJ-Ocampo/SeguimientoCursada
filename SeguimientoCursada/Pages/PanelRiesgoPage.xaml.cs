using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class PanelRiesgoPage : ContentPage
{
    public PanelRiesgoPage()
    {
        InitializeComponent();
        BindingContext = new PanelRiesgoViewModel();
    }
}
