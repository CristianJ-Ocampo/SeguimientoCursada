using Microsoft.Extensions.DependencyInjection;
using SeguimientoCursada.Services;
using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class ConfiguracionPage : ContentPage
{
    public ConfiguracionPage()
    {
        InitializeComponent();
        var configuracion = Application.Current?.Handler?.MauiContext?.Services
            .GetRequiredService<ConfiguracionRiesgoService>()
            ?? throw new InvalidOperationException("ConfiguracionRiesgoService no está disponible.");
        BindingContext = new ConfiguracionViewModel(configuracion);
    }
}
