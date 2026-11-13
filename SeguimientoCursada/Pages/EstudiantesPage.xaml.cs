using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class EstudiantesPage : ContentPage
{
    private readonly EstudiantesViewModel _viewModel;

    public EstudiantesPage()
    {
        InitializeComponent();
        _viewModel = new EstudiantesViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RecargarAsync();
    }
}
