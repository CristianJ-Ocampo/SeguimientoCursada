using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeguimientoCursada.Models;

namespace SeguimientoCursada.ViewModels;

public class AsistenciaViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<string> ComisionesDisponibles = new[] { "1° A", "1° B" };
    public static ObservableCollection<Asistencia> TodasLasAsistencias { get; } = new();
    private string _comisionSeleccionada = "1° A";
    private DateTime _fechaSeleccionada = DateTime.Today;
    private string _mensajeValidacion = string.Empty;

    public AsistenciaViewModel()
    {
        GuardarAsistenciaCommand = new Command(async () => await GuardarAsistenciaAsync());
        EstudiantesViewModel.TodosLosEstudiantes.CollectionChanged += (_, _) => ActualizarEstudiantes();
        ActualizarEstudiantes();
    }

    public IReadOnlyList<string> Comisiones => ComisionesDisponibles;

    public string ComisionSeleccionada
    {
        get => _comisionSeleccionada;
        set
        {
            if (SetProperty(ref _comisionSeleccionada, value))
            {
                ActualizarEstudiantes();
            }
        }
    }

    public DateTime FechaSeleccionada
    {
        get => _fechaSeleccionada;
        set => SetProperty(ref _fechaSeleccionada, value);
    }

    public ObservableCollection<EstudianteAsistenciaViewModel> Estudiantes { get; } = new();

    public ObservableCollection<Asistencia> Asistencias => TodasLasAsistencias;

    public string MensajeValidacion
    {
        get => _mensajeValidacion;
        private set => SetProperty(ref _mensajeValidacion, value);
    }

    public Command GuardarAsistenciaCommand { get; }

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
        var registrosAnteriores = Asistencias
            .Where(asistencia => asistencia.Comision == ComisionSeleccionada && asistencia.Fecha.Date == fecha)
            .ToList();

        foreach (var registro in registrosAnteriores)
        {
            Asistencias.Remove(registro);
        }

        var siguienteId = Asistencias.Count == 0 ? 1 : Asistencias.Max(asistencia => asistencia.Id) + 1;

        foreach (var estudiante in Estudiantes)
        {
            Asistencias.Add(new Asistencia
            {
                Id = siguienteId++,
                LegajoEstudiante = estudiante.Legajo,
                Fecha = fecha,
                Presente = estudiante.Presente,
                Comision = ComisionSeleccionada
            });
        }

        MensajeValidacion = "Asistencia guardada correctamente.";
        await MostrarAlertaAsync(MensajeValidacion);
    }

    private void ActualizarEstudiantes()
    {
        Estudiantes.Clear();

        foreach (var estudiante in EstudiantesViewModel.TodosLosEstudiantes
                     .Where(estudiante => estudiante.Comision == ComisionSeleccionada)
                     .OrderBy(estudiante => estudiante.Apellido, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(estudiante => estudiante.Nombre, StringComparer.CurrentCultureIgnoreCase))
        {
            Estudiantes.Add(new EstudianteAsistenciaViewModel(estudiante));
        }
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

    public EstudianteAsistenciaViewModel(Estudiante estudiante)
    {
        Legajo = estudiante.Legajo;
        Apellido = estudiante.Apellido;
        Nombre = estudiante.Nombre;
    }

    public int Legajo { get; }

    public string Apellido { get; }

    public string Nombre { get; }

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Presente)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
