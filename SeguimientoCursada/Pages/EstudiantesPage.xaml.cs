using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class EstudiantesPage : ContentPage
{
    public EstudiantesPage()
    {
        InitializeComponent();
        BindingContext = new EstudiantesViewModel();
    }
}
