using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using SeguimientoCursada.Models;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public class AsistenciaViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private static readonly IReadOnlyList<string> ComisionesDisponibles = new[] { "1° A", "1° B" };
    public static ObservableCollection<Asistencia> TodasLasAsistencias { get; } = new();
    private string _comisionSeleccionada = "1° A";
    private DateTime _fechaSeleccionada = DateTime.Today;
    private string _mensajeValidacion = string.Empty;
    private int _versionActualizacion;

    public AsistenciaViewModel()
        : this(ObtenerDatabaseService())
    {
    }

    public AsistenciaViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        GuardarAsistenciaCommand = new Command(async () => await GuardarAsistenciaAsync());
        EstudiantesViewModel.TodosLosEstudiantes.CollectionChanged += (_, _) =>
            _ = ActualizarEstudiantesAsync();
        _ = RecargarAsync();
    }

    public IReadOnlyList<string> Comisiones => ComisionesDisponibles;

    public string ComisionSeleccionada
    {
        get => _comisionSeleccionada;
        set
        {
            if (SetProperty(ref _comisionSeleccionada, value))
            {
                _ = ActualizarEstudiantesAsync();
            }
        }
    }

    public DateTime FechaSeleccionada
    {
        get => _fechaSeleccionada;
        set
        {
            if (SetProperty(ref _fechaSeleccionada, value))
            {
                _ = ActualizarEstudiantesAsync();
            }
        }
    }

    public ObservableCollection<EstudianteAsistenciaViewModel> Estudiantes { get; } = new();

    public ObservableCollection<Asistencia> Asistencias => TodasLasAsistencias;

    public string MensajeValidacion
    {
        get => _mensajeValidacion;
        private set => SetProperty(ref _mensajeValidacion, value);
    }

    public Command GuardarAsistenciaCommand { get; }

    public async Task RecargarAsync()
    {
        await CargarAsistenciasAsync();
        await ActualizarEstudiantesAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task GuardarAsistenciaAsync()
    {
        if (!ComisionesDisponibles.Contains(ComisionSeleccionada))
        {
            await MostrarAlertaAsync("Seleccioná una comisión válida.");
            return;
        }

        if (FechaSeleccionada == default)
        {
            await MostrarAlertaAsync("Seleccioná la fecha de la clase.");
            return;
        }

        if (Estudiantes.Count == 0)
        {
            await MostrarAlertaAsync($"No hay estudiantes registrados en la comisión {ComisionSeleccionada}.");
            return;
        }

        var fecha = FechaSeleccionada.Date;
        var comision = ComisionSeleccionada;
        var legajosVisibles = Estudiantes
            .Select(estudiante => estudiante.Legajo)
            .ToHashSet();

        var registrosAnteriores = (await _databaseService.GetAsistenciasAsync())
            .Where(asistencia =>
                asistencia.Fecha.Date == fecha &&
                string.Equals(
                    (asistencia.Comision ?? string.Empty).Trim(),
                    comision,
                    StringComparison.Ordinal) &&
                legajosVisibles.Contains(asistencia.LegajoEstudiante))
            .ToList();

        var nuevosRegistros = Estudiantes
            .Select(estudiante => new Asistencia
            {
                LegajoEstudiante = estudiante.Legajo,
                Fecha = fecha,
                Presente = estudiante.Presente,
                Estado = estudiante.Estado,
                Comision = comision
            })
            .ToList();

        try
        {
            foreach (var registro in registrosAnteriores)
            {
                await _databaseService.DeleteAsistenciaAsync(registro);
            }

            foreach (var registro in nuevosRegistros)
            {
                await _databaseService.SaveAsistenciaAsync(registro);
            }

            var fechaAgregada = await _databaseService.EnsureFechaClaseAsync(fecha, comision);

            await RecargarAsync();
            MensajeValidacion = fechaAgregada
                ? "Asistencia guardada correctamente. La fecha se agregó al calendario como día de cursada."
                : "Asistencia guardada correctamente.";
        }
        catch (Exception ex)
        {
            await RecargarAsync();
            MensajeValidacion = $"No se pudo guardar la asistencia: {ex.Message}";
            return;
        }

        await MostrarAlertaAsync(MensajeValidacion);
    }

    private async Task CargarAsistenciasAsync()
    {
        try
        {
            var asistencias = await _databaseService.GetAsistenciasAsync();

            Asistencias.Clear();
            foreach (var asistencia in asistencias)
            {
                Asistencias.Add(asistencia);
            }
        }
        catch (Exception ex)
        {
            MensajeValidacion = $"No se pudieron cargar las asistencias: {ex.Message}";
        }
    }

    private async Task ActualizarEstudiantesAsync()
    {
        var version = ++_versionActualizacion;
        var comision = ComisionSeleccionada;
        var fecha = FechaSeleccionada.Date;

        try
        {
            var asistencias = await _databaseService.GetAsistenciasAsync();

            if (version != _versionActualizacion)
            {
                return;
            }

            var registros = asistencias
                .Where(asistencia =>
                    asistencia.Fecha.Date == fecha &&
                    string.Equals(
                        (asistencia.Comision ?? string.Empty).Trim(),
                        comision,
                        StringComparison.Ordinal))
                .GroupBy(asistencia => asistencia.LegajoEstudiante)
                .ToDictionary(grupo => grupo.Key, grupo => grupo.First());

            var estudiantes = (await _databaseService.GetEstudiantesAsync())
                .Where(estudiante => string.Equals(
                    estudiante.Comision,
                    comision,
                    StringComparison.Ordinal))
                .Where(estudiante =>
                    estudiante.FechaIngreso is null ||
                    estudiante.FechaIngreso.Value.Date <= fecha)
                .OrderBy(estudiante => estudiante.Apellido, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(estudiante => estudiante.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var resultado = new List<EstudianteAsistenciaViewModel>();

            foreach (var estudiante in estudiantes)
            {
                registros.TryGetValue(estudiante.Legajo, out var existente);
                resultado.Add(new EstudianteAsistenciaViewModel(estudiante, existente));
            }

            if (version != _versionActualizacion)
            {
                return;
            }

            Estudiantes.Clear();
            foreach (var estudiante in resultado)
            {
                Estudiantes.Add(estudiante);
            }
        }
        catch (Exception ex)
        {
            MensajeValidacion = $"No se pudieron cargar los estudiantes: {ex.Message}";
        }
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

public class EstudianteAsistenciaViewModel : INotifyPropertyChanged
{
    private bool _presente;
    private string _estado;

    public EstudianteAsistenciaViewModel(Estudiante estudiante, Asistencia? existente = null)
    {
        Legajo = estudiante.Legajo;
        Apellido = estudiante.Apellido;
        Nombre = estudiante.Nombre;
        _estado = existente is null
            ? EstadoAsistencia.Ausente
            : EstadoAsistencia.Normalizar(existente);
        _presente = _estado is EstadoAsistencia.Presente or EstadoAsistencia.Justificado;
    }

    public int Legajo { get; }

    public string Apellido { get; }

    public string Nombre { get; }

    public string Estado => _estado;

    public bool Presente
    {
        get => _presente;
        set
        {
            if (_presente == value)
            {
                return;
            }

            _presente = value;
            _estado = value ? EstadoAsistencia.Presente : EstadoAsistencia.Ausente;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Presente)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
