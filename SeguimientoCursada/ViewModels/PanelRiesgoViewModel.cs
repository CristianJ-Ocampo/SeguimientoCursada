using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SeguimientoCursada.Models;
using SeguimientoCursada.Pages;
using SeguimientoCursada.Services;
using Microsoft.Extensions.DependencyInjection;

namespace SeguimientoCursada.ViewModels;

public class PanelRiesgoViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<string> ComisionesDisponibles = new[] { "1° A", "1° B" };
    private string _comisionSeleccionada = "1° A";
    private readonly DatabaseService _databaseService;
    private readonly ConfiguracionRiesgoService _configuracionRiesgoService;

    public PanelRiesgoViewModel()
    {
        _databaseService = Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<DatabaseService>()
            ?? throw new InvalidOperationException("DatabaseService no está disponible.");
        _configuracionRiesgoService = Application.Current?.Handler?.MauiContext?.Services
            .GetRequiredService<ConfiguracionRiesgoService>()
            ?? throw new InvalidOperationException("ConfiguracionRiesgoService no está disponible.");
        _configuracionRiesgoService.ConfiguracionCambió += ConfiguracionCambió;
        AbrirDetalleCommand = new Command(async parameter =>
        {
            if (int.TryParse(
                    Convert.ToString(parameter, CultureInfo.InvariantCulture),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var legajo) && legajo > 0)
            {
                await AbrirDetalleAsync(legajo);
            }
        });
        EstudiantesViewModel.TodosLosEstudiantes.CollectionChanged += FuenteCambiada;
        AsistenciaViewModel.TodasLasAsistencias.CollectionChanged += FuenteCambiada;
        _ = ActualizarPanelAsync();
    }

    private void ConfiguracionCambió(object? sender, EventArgs e)
    {
        _ = ActualizarPanelAsync();
    }

    public IReadOnlyList<string> Comisiones => ComisionesDisponibles;

    public string ComisionSeleccionada
    {
        get => _comisionSeleccionada;
        set
        {
            if (SetProperty(ref _comisionSeleccionada, value))
            {
                _ = ActualizarPanelAsync();
            }
        }
    }

    public ObservableCollection<EstudianteRiesgoViewModel> Estudiantes { get; } = new();

    public Command AbrirDetalleCommand { get; }

    public string MensajeNominaVacia =>
        $"Todavía no hay estudiantes en la comisión {ComisionSeleccionada}.";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void FuenteCambiada(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = ActualizarPanelAsync();
    }

    private static Task AbrirDetalleAsync(int legajo)
    {
        return Shell.Current.GoToAsync(
            $"{nameof(EstudianteDetallePage)}?Legajo={legajo.ToString(CultureInfo.InvariantCulture)}");
    }

    private async Task ActualizarPanelAsync()
    {
        var totalClases = await _databaseService.GetTotalClasesAsync();
        var trabajos = await _databaseService.GetTrabajosPracticosAsync(soloObligatorios: true);
        var asistencias = await _databaseService.GetAsistenciasAsync();
        var entregas = await _databaseService.GetEntregasAsync();
        Estudiantes.Clear();

        foreach (var estudiante in EstudiantesViewModel.TodosLosEstudiantes
                     .Where(estudiante => estudiante.Comision == ComisionSeleccionada)
                      .Select(estudiante => new EstudianteRiesgoViewModel(
                          estudiante,
                          asistencias,
                          totalClases,
                          trabajos,
                          entregas,
                          _configuracionRiesgoService))
                     .OrderByDescending(estudiante => estudiante.PrioridadRiesgo)
                     .ThenByDescending(estudiante => estudiante.Inasistencias)
                     .ThenBy(estudiante => estudiante.Apellido, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(estudiante => estudiante.Nombre, StringComparer.CurrentCultureIgnoreCase))
        {
            Estudiantes.Add(estudiante);
        }

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

public class EstudianteRiesgoViewModel
{
    public EstudianteRiesgoViewModel(
        Estudiante estudiante,
        IEnumerable<Asistencia> asistencias,
        int totalClases,
        IEnumerable<TrabajoPractico> trabajos,
        IEnumerable<EntregaTrabajo> entregas,
        ConfiguracionRiesgoService configuracion)
    {
        var resultado = RiesgoEstudianteService.Calcular(
            estudiante,
            asistencias,
            totalClases,
            trabajos,
            entregas,
            configuracion);

        Legajo = estudiante.Legajo;
        Apellido = estudiante.Apellido;
        Nombre = estudiante.Nombre;
        Inasistencias = resultado.Inasistencias;
        PorcentajeAsistencia = resultado.PorcentajeAsistencia;
        PorcentajeEntregas = resultado.PorcentajeEntregas;
        NivelRiesgo = resultado.Nivel.ToString();
        MotivoRiesgo = resultado.Motivo;
        PrioridadRiesgo = resultado.Nivel switch
        {
            SeguimientoCursada.Services.NivelRiesgo.Alto => 3,
            SeguimientoCursada.Services.NivelRiesgo.Medio => 2,
            _ => 1
        };
    }

    public int Legajo { get; }
    public string Apellido { get; }
    public string Nombre { get; }
    public int Inasistencias { get; }
    public decimal PorcentajeAsistencia { get; }
    public decimal PorcentajeEntregas { get; }
    public string NivelRiesgo { get; }
    public string MotivoRiesgo { get; }
    public int PrioridadRiesgo { get; }
}
