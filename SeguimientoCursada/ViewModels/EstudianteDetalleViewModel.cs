using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using SeguimientoCursada.Models;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

[QueryProperty(nameof(Legajo), "Legajo")]
public sealed class EstudianteDetalleViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private string _mensaje = string.Empty;
    private bool _estaCargando;
    private int _legajo;

    public EstudianteDetalleViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        VolverCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        ActualizarAsistenciaCommand = new Command<AsistenciaHistorialViewModel>(
            async asistencia => await ActualizarAsistenciaAsync(asistencia));
    }

    public Estudiante? Estudiante { get; private set; }

    public ObservableCollection<AsistenciaHistorialViewModel> Historial { get; } = new();

    public string Legajo
    {
        get => _legajo > 0
            ? _legajo.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        set
        {
            var valorRecibido = value ?? string.Empty;
            Debug.WriteLine($"[EstudianteDetalleViewModel] Legajo recibido: '{valorRecibido}'");

            if (!int.TryParse(
                    valorRecibido,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var legajo) || legajo <= 0)
            {
                Mensaje = $"El legajo recibido no es válido: '{valorRecibido}'.";
                OnPropertyChanged();
                return;
            }

            if (_legajo == legajo)
            {
                return;
            }

            _legajo = legajo;
            OnPropertyChanged();
            _ = CargarDatosAsync(legajo);
        }
    }

    public string Apellido => Estudiante?.Apellido ?? string.Empty;

    public string Nombre => Estudiante?.Nombre ?? string.Empty;

    public string Comision => Estudiante?.Comision ?? string.Empty;

    public int TotalInasistencias => Historial.Count(
        asistencia => asistencia.EstadoSeleccionado == EstadoAsistencia.Ausente);

    public int TotalClases => Historial.Count;

    public string NivelRiesgo { get; private set; } = "Sin datos";

    public string MotivoRiesgo { get; private set; } = string.Empty;

    public IReadOnlyList<string> EstadosAsistencia => EstadoAsistencia.Valores;

    public string Mensaje
    {
        get => _mensaje;
        private set => SetProperty(ref _mensaje, value);
    }

    public Command VolverCommand { get; }

    public Command<AsistenciaHistorialViewModel> ActualizarAsistenciaCommand { get; }

    public bool EstaCargando
    {
        get => _estaCargando;
        private set => SetProperty(ref _estaCargando, value);
    }

    private async Task ActualizarAsistenciaAsync(AsistenciaHistorialViewModel? historial)
    {
        if (historial is null || historial.Asistencia.Id <= 0)
        {
            Mensaje = "No se seleccionó una asistencia válida para actualizar.";
            return;
        }

        if (!EstadoAsistencia.Valores.Contains(historial.EstadoSeleccionado))
        {
            Mensaje = "El estado de asistencia seleccionado no es válido.";
            return;
        }

        try
        {
            EstadoAsistencia.Aplicar(historial.Asistencia, historial.EstadoSeleccionado);
            await _databaseService.UpdateAsistenciaAsync(historial.Asistencia);
            await CargarDatosAsync(_legajo);
            Mensaje = "Asistencia actualizada correctamente.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo actualizar la asistencia: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Task CargarAsync(int legajo)
    {
        return CargarDatosAsync(legajo);
    }

    private async Task CargarDatosAsync(int legajo)
    {
        if (legajo <= 0)
        {
            Mensaje = "No se recibió un legajo válido.";
            return;
        }

        EstaCargando = true;
        Mensaje = string.Empty;

        try
        {
            _legajo = legajo;

            var estudiante = await _databaseService.GetEstudiantePorLegajoAsync(_legajo);

            Historial.Clear();

            if (estudiante is null)
            {
                Estudiante = null;
                NivelRiesgo = "Sin datos";
                MotivoRiesgo = string.Empty;
                Mensaje = $"No se encontró el estudiante con legajo {legajo}.";
                NotificarDatos();
                return;
            }

            Estudiante = estudiante;

            var asistencias = await _databaseService.GetAsistenciasPorLegajoAsync(_legajo);
            foreach (var asistencia in asistencias)
            {
                Historial.Add(new AsistenciaHistorialViewModel(asistencia));
            }

            var resultadoRiesgo = RiesgoEstudianteService.Calcular(Estudiante, asistencias);
            NivelRiesgo = resultadoRiesgo.Nivel.ToString();
            MotivoRiesgo = resultadoRiesgo.Motivo;
            NotificarDatos();
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo cargar la ficha: {ex.Message}";
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private void NotificarDatos()
    {
        OnPropertyChanged(nameof(Estudiante));
        OnPropertyChanged(nameof(Legajo));
        OnPropertyChanged(nameof(Apellido));
        OnPropertyChanged(nameof(Nombre));
        OnPropertyChanged(nameof(Comision));
        OnPropertyChanged(nameof(TotalInasistencias));
        OnPropertyChanged(nameof(TotalClases));
        OnPropertyChanged(nameof(NivelRiesgo));
        OnPropertyChanged(nameof(MotivoRiesgo));
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

public sealed class AsistenciaHistorialViewModel
{
    private string _estadoSeleccionado;

    public AsistenciaHistorialViewModel(Asistencia asistencia)
    {
        Asistencia = asistencia;
        Fecha = asistencia.Fecha;
        _estadoSeleccionado = EstadoAsistencia.Normalizar(asistencia);
    }

    public Asistencia Asistencia { get; }

    public DateTime Fecha { get; }

    public bool Presente => EstadoSeleccionado == EstadoAsistencia.Presente;

    public string Estado => EstadoSeleccionado;

    public string EstadoSeleccionado
    {
        get => _estadoSeleccionado;
        set
        {
            if (_estadoSeleccionado == value)
            {
                return;
            }

            _estadoSeleccionado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Estado));
            OnPropertyChanged(nameof(Presente));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
