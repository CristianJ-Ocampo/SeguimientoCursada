using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public sealed class ConfiguracionViewModel : INotifyPropertyChanged
{
    private readonly ConfiguracionRiesgoService _configuracion;
    private readonly DatabaseService _databaseService;
    private string _asistenciaRegular;
    private string _asistenciaCritica;
    private string _entregasMinimas;
    private string _mensaje = string.Empty;
    private bool _mensajeEsError;

    public ConfiguracionViewModel(
        ConfiguracionRiesgoService configuracion,
        DatabaseService databaseService)
    {
        _configuracion = configuracion;
        _databaseService = databaseService;
        _asistenciaRegular = Formatear(_configuracion.AsistenciaRegular);
        _asistenciaCritica = Formatear(_configuracion.AsistenciaCritica);
        _entregasMinimas = Formatear(_configuracion.EntregasMinimas);
        GuardarCommand = new Command(Guardar);
        RestaurarCommand = new Command(Restaurar);
        BorrarEstudiantesCommand = new Command(async () => await BorrarEstudiantesAsync());
        RestablecerDatosCommand = new Command(async () => await RestablecerDatosAsync());
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

    public Color ColorMensaje => _mensajeEsError ? Colors.Red : Colors.Green;

    public Command GuardarCommand { get; }
    public Command RestaurarCommand { get; }
    public Command BorrarEstudiantesCommand { get; }
    public Command RestablecerDatosCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Guardar()
    {
        if (!TryParse(AsistenciaRegular, out var regular) ||
            !TryParse(AsistenciaCritica, out var critica) ||
            !TryParse(EntregasMinimas, out var entregas))
        {
            MostrarMensaje("Ingresá porcentajes válidos entre 0 y 100.", esError: true);
            return;
        }

        try
        {
            _configuracion.Guardar(regular, critica, entregas);
            MostrarMensaje("Configuración guardada correctamente.");
        }
        catch (Exception ex)
        {
            MostrarMensaje(ex.Message, esError: true);
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
            MostrarMensaje("Valores predeterminados restaurados.");
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudieron restaurar los valores: {ex.Message}", esError: true);
        }
    }

    private async Task BorrarEstudiantesAsync()
    {
        if (!await ConfirmarAsync(
                "Borrar todos los estudiantes",
                "Se eliminarán todos los estudiantes junto con sus asistencias, entregas y contactos. " +
                "El calendario de clases y los trabajos prácticos se conservan.",
                "Borrar",
                "Se perderán todos los datos de los estudiantes. Esta acción no se puede deshacer. ¿Continuar?"))
        {
            return;
        }

        try
        {
            var eliminados = await _databaseService.EliminarTodosLosEstudiantesAsync();
            LimpiarColeccionesEnMemoria();
            MostrarMensaje(eliminados == 1
                ? "Se eliminó 1 estudiante y sus datos asociados."
                : $"Se eliminaron {eliminados} estudiantes y sus datos asociados.");
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudieron borrar los estudiantes: {ex.Message}", esError: true);
        }
    }

    private async Task RestablecerDatosAsync()
    {
        if (!await ConfirmarAsync(
                "Restablecer todos los datos",
                "Se eliminarán estudiantes, asistencias, fechas de clase, trabajos prácticos, " +
                "entregas y contactos. Las preferencias de la aplicación se conservan.",
                "Restablecer",
                "Se perderán todos los datos, incluido el calendario y los trabajos prácticos. " +
                "Esta acción no se puede deshacer. ¿Confirmás el restablecimiento completo?"))
        {
            return;
        }

        try
        {
            var eliminados = await _databaseService.ResetearTodosLosDatosAsync();
            LimpiarColeccionesEnMemoria();
            MostrarMensaje(eliminados == 1
                ? "Se restablecieron todos los datos. Se eliminó 1 estudiante."
                : $"Se restablecieron todos los datos. Se eliminaron {eliminados} estudiantes.");
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudieron restablecer los datos: {ex.Message}", esError: true);
        }
    }

    private static async Task<bool> ConfirmarAsync(
        string titulo,
        string primeraAdvertencia,
        string textoAceptar,
        string segundaAdvertencia)
    {
        var pagina = Application.Current?.Windows.FirstOrDefault()?.Page;

        if (pagina is null)
        {
            return false;
        }

        var primerPaso = await pagina.DisplayAlertAsync(
            titulo,
            primeraAdvertencia,
            textoAceptar,
            "Cancelar");

        if (!primerPaso)
        {
            return false;
        }

        return await pagina.DisplayAlertAsync(
            $"Confirmar {titulo.ToLowerInvariant()}",
            segundaAdvertencia,
            textoAceptar,
            "Cancelar");
    }

    private static void LimpiarColeccionesEnMemoria()
    {
        EstudiantesViewModel.TodosLosEstudiantes.Clear();
        AsistenciaViewModel.TodasLasAsistencias.Clear();
    }

    private void MostrarMensaje(string mensaje, bool esError = false)
    {
        _mensajeEsError = esError;
        Mensaje = mensaje;
        OnPropertyChanged(nameof(ColorMensaje));
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
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
