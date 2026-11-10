using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeguimientoCursada.Models;

namespace SeguimientoCursada.ViewModels;

public class EstudiantesViewModel : INotifyPropertyChanged
{
    private string _inputLegajo = string.Empty;
    private string _apellido = string.Empty;
    private string _nombre = string.Empty;
    private string _comisionSeleccionada = "1° A";
    private string _mensajeValidacion = string.Empty;

    public EstudiantesViewModel()
    {
        AgregarEstudianteCommand = new Command(async () => await AgregarEstudianteAsync());
        ActualizarNominaFiltrada();
    }

    public static ObservableCollection<Estudiante> TodosLosEstudiantes { get; } = new();

    public ObservableCollection<Estudiante> Estudiantes => TodosLosEstudiantes;

    public ObservableCollection<Estudiante> EstudiantesFiltrados { get; } = new();

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

        Estudiantes.Add(new Estudiante
        {
            Legajo = legajo,
            Apellido = Apellido.Trim(),
            Nombre = Nombre.Trim(),
            Comision = ComisionSeleccionada
        });

        InputLegajo = string.Empty;
        Apellido = string.Empty;
        Nombre = string.Empty;
        MensajeValidacion = string.Empty;
        ActualizarNominaFiltrada();
    }

    private static Task MostrarAlertaAsync(string mensaje)
    {
        return Application.Current?.MainPage?.DisplayAlert("Atención", mensaje, "OK")
            ?? Task.CompletedTask;
    }

    private void ActualizarNominaFiltrada()
    {
        EstudiantesFiltrados.Clear();

        foreach (var estudiante in Estudiantes
                     .Where(estudiante => estudiante.Comision == ComisionSeleccionada)
                     .OrderBy(estudiante => estudiante.Apellido, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(estudiante => estudiante.Nombre, StringComparer.CurrentCultureIgnoreCase))
        {
            EstudiantesFiltrados.Add(estudiante);
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
