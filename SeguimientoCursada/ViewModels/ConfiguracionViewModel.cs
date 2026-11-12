using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public sealed class ConfiguracionViewModel : INotifyPropertyChanged
{
    private readonly ConfiguracionRiesgoService _configuracion;
    private string _asistenciaRegular;
    private string _asistenciaCritica;
    private string _entregasMinimas;
    private string _mensaje = string.Empty;

    public ConfiguracionViewModel(ConfiguracionRiesgoService configuracion)
    {
        _configuracion = configuracion;
        _asistenciaRegular = Formatear(_configuracion.AsistenciaRegular);
        _asistenciaCritica = Formatear(_configuracion.AsistenciaCritica);
        _entregasMinimas = Formatear(_configuracion.EntregasMinimas);
        GuardarCommand = new Command(Guardar);
        RestaurarCommand = new Command(Restaurar);
    }

    public string AsistenciaRegular
    {
        get => _asistenciaRegular;
        set => SetProperty(ref _asistenciaRegular, value);
    }

    public string AsistenciaCritica
    {
        get => _asistenciaCritica;
        set => SetProperty(ref _asistenciaCritica, value);
    }

    public string EntregasMinimas
    {
        get => _entregasMinimas;
        set => SetProperty(ref _entregasMinimas, value);
    }

    public string Mensaje
    {
        get => _mensaje;
        private set => SetProperty(ref _mensaje, value);
    }

    public Command GuardarCommand { get; }
    public Command RestaurarCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Guardar()
    {
        if (!TryParse(AsistenciaRegular, out var regular) ||
            !TryParse(AsistenciaCritica, out var critica) ||
            !TryParse(EntregasMinimas, out var entregas))
        {
            Mensaje = "Ingresá porcentajes válidos entre 0 y 100.";
            return;
        }

        try
        {
            _configuracion.Guardar(regular, critica, entregas);
            Mensaje = "Configuración guardada correctamente.";
        }
        catch (Exception ex)
        {
            Mensaje = ex.Message;
        }
    }

    private void Restaurar()
    {
        try
        {
            _configuracion.RestaurarPredeterminados();
            AsistenciaRegular = Formatear(_configuracion.AsistenciaRegular);
            AsistenciaCritica = Formatear(_configuracion.AsistenciaCritica);
            EntregasMinimas = Formatear(_configuracion.EntregasMinimas);
            Mensaje = "Valores predeterminados restaurados.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudieron restaurar los valores: {ex.Message}";
        }
    }

    private static bool TryParse(string value, out decimal result)
    {
        return decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.CurrentCulture,
            out result) && result is >= 0 and <= 100;
    }

    private static string Formatear(decimal value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);

    private bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
        {
            return false;
        }

        backingField = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
