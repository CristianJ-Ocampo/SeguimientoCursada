using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class PanelRiesgoPage : ContentPage
{
    private readonly PanelRiesgoViewModel _viewModel;

    public PanelRiesgoPage()
    {
        InitializeComponent();
        _viewModel = new PanelRiesgoViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.ActualizarAsync();
    }
}
