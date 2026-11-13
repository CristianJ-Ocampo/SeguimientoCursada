using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace SeguimientoCursada.ViewModels;

public sealed class AppShellViewModel : INotifyPropertyChanged
{
    private const string TemaOscuroKey = "tema.oscuro";
    private const string PreferenciaTemaDefinidaKey = "tema.preferencia_definida";
    private bool _temaOscuro;
    private bool _preferenciaTemaDefinida;
    private bool _actualizandoDesdeSistema;

    public AppShellViewModel()
    {
        _preferenciaTemaDefinida = Preferences.Default.Get(PreferenciaTemaDefinidaKey, false);
        _temaOscuro = ObtenerTemaOscuroActual();
        if (!_preferenciaTemaDefinida)
        {
            AplicarTemaDelSistema();
        }

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        }

        AlternarTemaCommand = new Command(AlternarTema);
    }

    public bool PreferenciaTemaDefinida => _preferenciaTemaDefinida;

    public bool TemaOscuro
    {
        get => _temaOscuro;
        set
        {
            if (_temaOscuro == value)
            {
                return;
            }

            _temaOscuro = value;
            if (!_actualizandoDesdeSistema)
            {
                _preferenciaTemaDefinida = true;
                Preferences.Default.Set(PreferenciaTemaDefinidaKey, true);
                Preferences.Default.Set(TemaOscuroKey, value);
                AplicarTema();
                OnPropertyChanged(nameof(PreferenciaTemaDefinida));
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextoTema));
        }
    }

    public string TextoTema => TemaOscuro ? "Activar modo claro" : "Activar modo oscuro";

    public Command AlternarTemaCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;

    private void AlternarTema()
    {
        TemaOscuro = !TemaOscuro;
    }

    private bool ObtenerTemaOscuroActual()
    {
        if (_preferenciaTemaDefinida)
        {
            return Preferences.Default.Get(TemaOscuroKey, false);
        }

        return Application.Current?.RequestedTheme == AppTheme.Dark;
    }

    private void AplicarTemaDelSistema()
    {
        if (Application.Current is not null)
        {
            Application.Current.UserAppTheme = AppTheme.Unspecified;
        }
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (_preferenciaTemaDefinida)
        {
            return;
        }

        _actualizandoDesdeSistema = true;
        try
        {
            TemaOscuro = e.RequestedTheme == AppTheme.Dark;
        }
        finally
        {
            _actualizandoDesdeSistema = false;
        }
    }

    private void AplicarTema()
    {
        if (Application.Current is not null)
        {
            Application.Current.UserAppTheme = TemaOscuro ? AppTheme.Dark : AppTheme.Light;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
