using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class CalendarioClasesPage : ContentPage
{
    public CalendarioClasesPage()
    {
        InitializeComponent();
        BindingContext = new CalendarioClasesViewModel();
    }
}
