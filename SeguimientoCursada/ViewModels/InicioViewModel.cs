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
        RegistrarPrimerAlumnoCommand = new Command(async () => await Shell.Current.GoToAsync("EstudiantesPage"));
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
            }
        }
    }

    public bool MostrarEstadoVacio => !HayEstudiantes;
    public bool MostrarMetricas => HayEstudiantes;
    public bool EstaCargando { get => _estaCargando; private set => SetProperty(ref _estaCargando, value); }
    public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

    public int EstudiantesOptimos { get; private set; }
    public int EstudiantesEnRiesgo { get; private set; }
    public int EstudiantesCriticos { get; private set; }
    public decimal PromedioAsistencia { get; private set; }
    public decimal PromedioEntregas { get; private set; }
    public int Contactados { get; private set; }
    public int PendientesContacto { get; private set; }

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

            var totalClases = await _databaseService.GetTotalClasesAsync();
            var trabajos = await _databaseService.GetTrabajosPracticosAsync(true);
            var asistencias = await _databaseService.GetAsistenciasAsync();
            var entregas = await _databaseService.GetEntregasAsync();
            var contactos = await _databaseService.GetUltimosContactosAsync();
            var resultados = estudiantes.Select(e => RiesgoEstudianteService.Calcular(e, asistencias, totalClases, trabajos, entregas, _configuracionRiesgoService)).ToList();

            EstudiantesOptimos = resultados.Count(r => r.ObtenerEstadoSeguimiento() == EstadoSeguimiento.Optimo);
            EstudiantesEnRiesgo = resultados.Count(r => r.ObtenerEstadoSeguimiento() == EstadoSeguimiento.EnRiesgo);
            EstudiantesCriticos = resultados.Count(r => r.ObtenerEstadoSeguimiento() == EstadoSeguimiento.Critico);
            PromedioAsistencia = resultados.Any() ? Math.Round(resultados.Average(r => r.PorcentajeAsistencia), 2) : 0;
            PromedioEntregas = resultados.Any() ? Math.Round(resultados.Average(r => r.PorcentajeEntregas), 2) : 0;
            var intervenibles = resultados.Where(r => r.RequiereContacto()).ToList();
            Contactados = estudiantes.Zip(resultados).Count(pair => pair.Second.RequiereContacto() && contactos.Any(c => c.LegajoEstudiante == pair.First.Legajo));
            PendientesContacto = intervenibles.Count - Contactados;
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

    private void ReiniciarMetricas()
    {
        EstudiantesOptimos = EstudiantesEnRiesgo = EstudiantesCriticos = Contactados = PendientesContacto = 0;
        PromedioAsistencia = PromedioEntregas = 0;
        NotificarMetricas();
    }

    private void NotificarMetricas()
    {
        OnPropertyChanged(nameof(EstudiantesOptimos));
        OnPropertyChanged(nameof(EstudiantesEnRiesgo));
        OnPropertyChanged(nameof(EstudiantesCriticos));
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
}
