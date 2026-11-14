using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class AsistenciaPage : ContentPage
{
    private readonly AsistenciaViewModel _viewModel;

    public AsistenciaPage()
    {
        InitializeComponent();
        _viewModel = new AsistenciaViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RecargarAsync();
    }
}
