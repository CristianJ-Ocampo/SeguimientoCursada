using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using SeguimientoCursada.Models;
using SeguimientoCursada.Pages;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public class EstudiantesViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private string _comisionSeleccionada = "1° A";
    private string _mensajeError = string.Empty;
    private readonly ConfiguracionRiesgoService _configuracionRiesgoService;
    private IReadOnlyList<FechaClase> _fechasClase = Array.Empty<FechaClase>();
    private IReadOnlyList<TrabajoPractico> _trabajos = Array.Empty<TrabajoPractico>();
    private IReadOnlyList<Asistencia> _asistencias = Array.Empty<Asistencia>();
    private IReadOnlyList<EntregaTrabajo> _entregas = Array.Empty<EntregaTrabajo>();

    public EstudiantesViewModel()
        : this(ObtenerDatabaseService())
    {
    }

    public EstudiantesViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _configuracionRiesgoService = Application.Current?.Handler?.MauiContext?.Services
            .GetRequiredService<ConfiguracionRiesgoService>()
            ?? new ConfiguracionRiesgoService();
        AbrirDetalleCommand = new Command(async parameter =>
        {
            if (TryObtenerLegajo(parameter, out var legajo))
            {
                await AbrirDetalleAsync(legajo);
            }
        });
        IrAlAltaCommand = new Command(async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//AltaEstudiantePage");
            }
        });
        ActualizarNominaFiltrada();
        _ = CargarEstudiantesAsync();
    }

    public static ObservableCollection<Estudiante> TodosLosEstudiantes { get; } = new();

    public ObservableCollection<Estudiante> Estudiantes => TodosLosEstudiantes;

    public ObservableCollection<EstudianteListadoViewModel> EstudiantesFiltrados { get; } = new();

    private readonly Dictionary<int, ContactoEstudiante> _ultimosContactos = new();

    public IReadOnlyList<string> Comisiones { get; } = new[] { "1° A", "1° B" };

    public string ComisionSeleccionada
    {
        get => _comisionSeleccionada;
        set
        {
            if (SetProperty(ref _comisionSeleccionada, value))
            {
                ActualizarNominaFiltrada();
            }
        }
    }

    public int CantidadEstudiantes => EstudiantesFiltrados.Count;

    public string MensajeNominaVacia =>
        $"Todavía no hay estudiantes en la comisión {ComisionSeleccionada}. Usá \"Alta de estudiantes\" para registrar el primero.";

    public string MensajeError
    {
        get => _mensajeError;
        private set
        {
            if (SetProperty(ref _mensajeError, value))
            {
                OnPropertyChanged(nameof(TieneMensajeError));
            }
        }
    }

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    public Command AbrirDetalleCommand { get; }

    public Command IrAlAltaCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RecargarAsync()
    {
        await CargarEstudiantesAsync();
    }

    private async Task CargarEstudiantesAsync()
    {
        try
        {
            var estudiantes = await _databaseService.GetEstudiantesAsync();
            var contactos = await _databaseService.GetUltimosContactosAsync();
            var fechasClase = await _databaseService.GetFechasClaseAsync();
            var trabajos = await _databaseService.GetTrabajosPracticosAsync();
            var asistencias = await _databaseService.GetAsistenciasAsync();
            var entregas = await _databaseService.GetEntregasAsync();

            _ultimosContactos.Clear();
            foreach (var contacto in contactos)
            {
                _ultimosContactos[contacto.LegajoEstudiante] = contacto;
            }

            Estudiantes.Clear();
            foreach (var estudiante in estudiantes)
            {
                Estudiantes.Add(estudiante);
            }

            _fechasClase = fechasClase;
            _trabajos = trabajos;
            _asistencias = asistencias;
            _entregas = entregas;

            MensajeError = string.Empty;
            ActualizarNominaFiltrada();
        }
        catch (Exception ex)
        {
            MensajeError = $"No se pudieron cargar los estudiantes: {ex.Message}";
        }
    }

    private static DatabaseService ObtenerDatabaseService()
    {
        return Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<DatabaseService>()
            ?? throw new InvalidOperationException("DatabaseService no está disponible.");
    }

    private static async Task AbrirDetalleAsync(int legajo)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(EstudianteDetallePage)}?Legajo={legajo.ToString(CultureInfo.InvariantCulture)}");
    }

    private static bool TryObtenerLegajo(object? parameter, out int legajo)
    {
        return int.TryParse(
            Convert.ToString(parameter, CultureInfo.InvariantCulture),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out legajo) && legajo > 0;
    }

    private void ActualizarNominaFiltrada()
    {
        EstudiantesFiltrados.Clear();

        foreach (var estudiante in Estudiantes
                     .Where(estudiante => estudiante.Comision == ComisionSeleccionada)
                     .OrderBy(estudiante => estudiante.Apellido, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(estudiante => estudiante.Nombre, StringComparer.CurrentCultureIgnoreCase))
        {
            _ultimosContactos.TryGetValue(estudiante.Legajo, out var contacto);
            var resultado = RiesgoEstudianteService.Calcular(
                estudiante,
                _asistencias,
                _fechasClase,
                _trabajos,
                _entregas,
                _configuracionRiesgoService);
            EstudiantesFiltrados.Add(new EstudianteListadoViewModel(estudiante, contacto, resultado));
        }

        OnPropertyChanged(nameof(CantidadEstudiantes));
        OnPropertyChanged(nameof(MensajeNominaVacia));
    }

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
