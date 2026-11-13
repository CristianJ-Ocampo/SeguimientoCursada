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
    private string _inputLegajo = string.Empty;
    private string _apellido = string.Empty;
    private string _nombre = string.Empty;
    private string _comisionSeleccionada = "1° A";
    private string _mensajeValidacion = string.Empty;
    private readonly ConfiguracionRiesgoService _configuracionRiesgoService;
    private int _totalClases;
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
        AgregarEstudianteCommand = new Command(async () => await AgregarEstudianteAsync());
        AbrirDetalleCommand = new Command(async parameter =>
        {
            if (TryObtenerLegajo(parameter, out var legajo))
            {
                await AbrirDetalleAsync(legajo);
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

    public string InputLegajo
    {
        get => _inputLegajo;
        set => SetProperty(ref _inputLegajo, value ?? string.Empty);
    }

    public string Apellido
    {
        get => _apellido;
        set => SetProperty(ref _apellido, value);
    }

    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

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
        $"Todavía no hay estudiantes en la comisión {ComisionSeleccionada}. ¡Registrá el primero!";

    public string MensajeValidacion
    {
        get => _mensajeValidacion;
        private set => SetProperty(ref _mensajeValidacion, value);
    }

    public Command AgregarEstudianteCommand { get; }

    public Command AbrirDetalleCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task AgregarEstudianteAsync()
    {
        var inputLegajo = InputLegajo;

        if (string.IsNullOrWhiteSpace(inputLegajo))
        {
            await MostrarAlertaAsync("El legajo es un campo obligatorio.");
            return;
        }

        if (!inputLegajo.All(char.IsDigit) || !int.TryParse(inputLegajo, out var legajo))
        {
            await MostrarAlertaAsync("El legajo solo debe contener números.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Apellido))
        {
            await MostrarAlertaAsync("El Apellido es obligatorio.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ComisionSeleccionada))
        {
            await MostrarAlertaAsync("La Comisión es obligatoria.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Nombre))
        {
            await MostrarAlertaAsync("El Nombre es obligatorio.");
            return;
        }

        if (Estudiantes.Any(estudiante => estudiante.Legajo == legajo))
        {
            await MostrarAlertaAsync($"El legajo {legajo} ya está registrado.");
            return;
        }

        var estudiante = new Estudiante
        {
            Legajo = legajo,
            Apellido = Apellido.Trim(),
            Nombre = Nombre.Trim(),
            Comision = ComisionSeleccionada
        };

        try
        {
            await _databaseService.SaveEstudianteAsync(estudiante);
        }
        catch (Exception ex)
        {
            MensajeValidacion = $"No se pudo guardar el estudiante: {ex.Message}";
            return;
        }

        Estudiantes.Add(estudiante);

        InputLegajo = string.Empty;
        Apellido = string.Empty;
        Nombre = string.Empty;
        MensajeValidacion = string.Empty;
        ActualizarNominaFiltrada();
    }

    private async Task CargarEstudiantesAsync()
    {
        try
        {
            var estudiantes = await _databaseService.GetEstudiantesAsync();
            var contactos = await _databaseService.GetUltimosContactosAsync();
            var totalClases = await _databaseService.GetTotalClasesAsync();
            var trabajos = await _databaseService.GetTrabajosPracticosAsync(soloObligatorios: true);
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

            _totalClases = totalClases;
            _trabajos = trabajos;
            _asistencias = asistencias;
            _entregas = entregas;

            ActualizarNominaFiltrada();
        }
        catch (Exception ex)
        {
            MensajeValidacion = $"No se pudieron cargar los estudiantes: {ex.Message}";
        }
    }

    public async Task RecargarAsync()
    {
        await CargarEstudiantesAsync();
    }

    private static DatabaseService ObtenerDatabaseService()
    {
        return Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<DatabaseService>()
            ?? throw new InvalidOperationException("DatabaseService no está disponible.");
    }

    private static Task MostrarAlertaAsync(string mensaje)
    {
        return Application.Current?.MainPage?.DisplayAlert("Atención", mensaje, "OK")
            ?? Task.CompletedTask;
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
                _totalClases,
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
