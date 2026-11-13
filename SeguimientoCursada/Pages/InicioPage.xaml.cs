using Microsoft.Extensions.DependencyInjection;
using SeguimientoCursada.Services;
using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class InicioPage : ContentPage
{
    private readonly InicioViewModel _viewModel;

    public InicioPage()
    {
        InitializeComponent();
        var services = Application.Current?.Handler?.MauiContext?.Services
            ?? throw new InvalidOperationException("Los servicios de la aplicación no están disponibles.");
        _viewModel = new InicioViewModel(
            services.GetRequiredService<DatabaseService>(),
            services.GetRequiredService<ConfiguracionRiesgoService>());
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CargarAsync();
    }
}
