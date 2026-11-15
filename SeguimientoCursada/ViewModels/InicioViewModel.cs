using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using SeguimientoCursada.Models;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public sealed class InicioViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private readonly ConfiguracionRiesgoService _configuracionRiesgoService;
    private bool _estaCargando;
    private bool _hayEstudiantes;
    private string _mensaje = string.Empty;

    public InicioViewModel(DatabaseService databaseService, ConfiguracionRiesgoService configuracionRiesgoService)
    {
        _databaseService = databaseService;
        _configuracionRiesgoService = configuracionRiesgoService;
        RegistrarPrimerAlumnoCommand = new Command(async () => await IrAlAltaEstudianteAsync());
    }

    public bool HayEstudiantes
    {
        get => _hayEstudiantes;
        private set
        {
            if (SetProperty(ref _hayEstudiantes, value))
            {
                OnPropertyChanged(nameof(MostrarEstadoVacio));
                OnPropertyChanged(nameof(MostrarMetricas));
                OnPropertyChanged(nameof(MostrarResumenPorComision));
            }
        }
    }

    public bool MostrarEstadoVacio => !HayEstudiantes;
    public bool MostrarMetricas => HayEstudiantes;
    public bool MostrarResumenPorComision => HayEstudiantes && ResumenesPorComision.Count > 0;
    public bool EstaCargando { get => _estaCargando; private set => SetProperty(ref _estaCargando, value); }
    public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

    public int EstudiantesOptimos { get; private set; }
    public int EstudiantesEnRiesgo { get; private set; }
    public int EstudiantesCriticos { get; private set; }
    public int EstudiantesSinDatos { get; private set; }
    public decimal PromedioAsistencia { get; private set; }
    public decimal PromedioEntregas { get; private set; }
    public int Contactados { get; private set; }
    public int PendientesContacto { get; private set; }

    public ObservableCollection<ResumenComisionViewModel> ResumenesPorComision { get; } = new();

    public Command RegistrarPrimerAlumnoCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task CargarAsync()
    {
        if (EstaCargando) return;
        EstaCargando = true;
        Mensaje = string.Empty;
        try
        {
            var estudiantes = await _databaseService.GetEstudiantesAsync();
            HayEstudiantes = estudiantes.Count > 0;
            if (!HayEstudiantes)
            {
                ReiniciarMetricas();
                return;
            }

            var fechasClase = await _databaseService.GetFechasClaseAsync();
            var trabajos = await _databaseService.GetTrabajosPracticosAsync(true);
            var asistencias = await _databaseService.GetAsistenciasAsync();
            var entregas = await _databaseService.GetEntregasAsync();
            var contactos = await _databaseService.GetUltimosContactosAsync();

            var pares = estudiantes
                .Select(estudiante => new ParEstudianteRiesgo(
                    estudiante,
                    RiesgoEstudianteService.Calcular(
                        estudiante,
                        asistencias,
                        fechasClase,
                        trabajos,
                        entregas,
                        _configuracionRiesgoService)))
                .ToList();

            var resultados = pares.Select(par => par.Resultado).ToList();
            var metricas = CalcularMetricas(resultados);

            EstudiantesOptimos = metricas.Optimos;
            EstudiantesEnRiesgo = metricas.EnRiesgo;
            EstudiantesCriticos = metricas.Criticos;
            EstudiantesSinDatos = metricas.SinDatos;
            PromedioAsistencia = metricas.PromedioAsistencia;
            PromedioEntregas = metricas.PromedioEntregas;

            var intervenibles = pares.Where(par => par.Resultado.RequiereContacto()).ToList();
            Contactados = intervenibles.Count(par =>
                contactos.Any(contacto => contacto.LegajoEstudiante == par.Estudiante.Legajo));
            PendientesContacto = intervenibles.Count - Contactados;

            ActualizarResumenPorComision(pares);
            NotificarMetricas();
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo cargar el resumen: {ex.Message}";
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private void ActualizarResumenPorComision(IReadOnlyList<ParEstudianteRiesgo> pares)
    {
        ResumenesPorComision.Clear();

        foreach (var grupo in pares
                     .GroupBy(par => string.IsNullOrWhiteSpace(par.Estudiante.Comision)
                         ? "Sin comisión"
                         : par.Estudiante.Comision.Trim())
                     .OrderBy(grupo => grupo.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var metricas = CalcularMetricas(grupo.Select(par => par.Resultado).ToList());

            ResumenesPorComision.Add(new ResumenComisionViewModel(
                grupo.Key,
                metricas.Total,
                metricas.Optimos,
                metricas.EnRiesgo,
                metricas.Criticos,
                metricas.SinDatos,
                metricas.PromedioAsistencia,
                metricas.PromedioEntregas));
        }

        OnPropertyChanged(nameof(MostrarResumenPorComision));
    }

    private static MetricasRiesgo CalcularMetricas(IReadOnlyCollection<ResultadoRiesgo> resultados)
    {
        var promediosAsistencia = resultados
            .Where(resultado => !resultado.SinRegistrosAsistencia &&
                                resultado.TotalClasesConfiguradas > 0)
            .Select(resultado => resultado.PorcentajeAsistencia)
            .ToList();
        var promediosEntregas = resultados
            .Where(resultado => resultado.TotalTrabajosObligatorios > 0)
            .Select(resultado => resultado.PorcentajeEntregas)
            .ToList();

        return new MetricasRiesgo(
            resultados.Count,
            resultados.Count(resultado => resultado.ObtenerEstadoSeguimiento() == EstadoSeguimiento.Optimo),
            resultados.Count(resultado => resultado.ObtenerEstadoSeguimiento() == EstadoSeguimiento.EnRiesgo),
            resultados.Count(resultado => resultado.ObtenerEstadoSeguimiento() == EstadoSeguimiento.Critico),
            resultados.Count(resultado => resultado.ObtenerEstadoSeguimiento() == EstadoSeguimiento.SinDatos),
            promediosAsistencia.Count > 0 ? Math.Round(promediosAsistencia.Average(), 2) : 0,
            promediosEntregas.Count > 0 ? Math.Round(promediosEntregas.Average(), 2) : 0);
    }

    private static async Task IrAlAltaEstudianteAsync()
    {
        if (Shell.Current is null)
        {
            return;
        }

        try
        {
            await Shell.Current.GoToAsync("//AltaEstudiantePage");
            return;
        }
        catch (Exception)
        {
            // Se intenta con la ruta relativa como alternativa.
        }

        try
        {
            await Shell.Current.GoToAsync("AltaEstudiantePage");
        }
        catch (Exception ex)
        {
            var pagina = Application.Current?.Windows.FirstOrDefault()?.Page;

            if (pagina is not null)
            {
                await pagina.DisplayAlertAsync(
                    "No se pudo abrir el alta",
                    $"No se pudo navegar a la pantalla de alta de estudiantes: {ex.Message}",
                    "OK");
            }
        }
    }

    private void ReiniciarMetricas()
    {
        EstudiantesOptimos = EstudiantesEnRiesgo = EstudiantesCriticos = EstudiantesSinDatos = Contactados = PendientesContacto = 0;
        PromedioAsistencia = PromedioEntregas = 0;
        ResumenesPorComision.Clear();
        OnPropertyChanged(nameof(MostrarResumenPorComision));
        NotificarMetricas();
    }

    private void NotificarMetricas()
    {
        OnPropertyChanged(nameof(EstudiantesOptimos));
        OnPropertyChanged(nameof(EstudiantesEnRiesgo));
        OnPropertyChanged(nameof(EstudiantesCriticos));
        OnPropertyChanged(nameof(EstudiantesSinDatos));
        OnPropertyChanged(nameof(PromedioAsistencia));
        OnPropertyChanged(nameof(PromedioEntregas));
        OnPropertyChanged(nameof(Contactados));
        OnPropertyChanged(nameof(PendientesContacto));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed record ParEstudianteRiesgo(Estudiante Estudiante, ResultadoRiesgo Resultado);

    private sealed record MetricasRiesgo(
        int Total,
        int Optimos,
        int EnRiesgo,
        int Criticos,
        int SinDatos,
        decimal PromedioAsistencia,
        decimal PromedioEntregas);
}
