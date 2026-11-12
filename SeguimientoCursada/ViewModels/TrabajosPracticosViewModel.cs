using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using SeguimientoCursada.Models;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public sealed class TrabajosPracticosViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private TrabajoPractico? _trabajoSeleccionado;
    private string _titulo = string.Empty;
    private string _descripcion = string.Empty;
    private bool _obligatorio = true;
    private string _mensaje = string.Empty;

    public TrabajosPracticosViewModel()
        : this(ObtenerDatabaseService())
    {
    }

    public TrabajosPracticosViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        GuardarCommand = new Command(async () => await GuardarAsync());
        NuevoCommand = new Command(LimpiarFormulario);
        _ = CargarAsync();
    }

    public ObservableCollection<TrabajoPractico> Trabajos { get; } = new();

    public TrabajoPractico? TrabajoSeleccionado
    {
        get => _trabajoSeleccionado;
        set
        {
            if (SetProperty(ref _trabajoSeleccionado, value) && value is not null)
            {
                Titulo = value.Titulo;
                Descripcion = value.Descripcion;
                Obligatorio = value.Obligatorio;
            }
        }
    }

    public string Titulo
    {
        get => _titulo;
        set => SetProperty(ref _titulo, value);
    }

    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    public bool Obligatorio
    {
        get => _obligatorio;
        set => SetProperty(ref _obligatorio, value);
    }

    public string Mensaje
    {
        get => _mensaje;
        private set => SetProperty(ref _mensaje, value);
    }

    public Command GuardarCommand { get; }
    public Command NuevoCommand { get; }
    public Command<TrabajoPractico> EliminarCommand => new(async trabajo => await EliminarAsync(trabajo));

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task CargarAsync()
    {
        try
        {
            var trabajos = await _databaseService.GetTrabajosPracticosAsync();
            Trabajos.Clear();
            foreach (var trabajo in trabajos.OrderBy(trabajo => trabajo.Titulo))
            {
                Trabajos.Add(trabajo);
            }
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudieron cargar los trabajos prácticos: {ex.Message}";
        }
    }

    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(Titulo))
        {
            Mensaje = "Ingresá un título para el trabajo práctico.";
            return;
        }

        try
        {
            var trabajo = TrabajoSeleccionado ?? new TrabajoPractico();
            trabajo.Titulo = Titulo;
            trabajo.Descripcion = Descripcion;
            trabajo.Obligatorio = Obligatorio;
            trabajo.Activo = true;

            await _databaseService.SaveTrabajoPracticoAsync(trabajo);
            await CargarAsync();
            LimpiarFormulario();
            Mensaje = "Trabajo práctico guardado correctamente.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo guardar el trabajo práctico: {ex.Message}";
        }
    }

    private async Task EliminarAsync(TrabajoPractico? trabajo)
    {
        if (trabajo is null)
        {
            return;
        }

        try
        {
            await _databaseService.DeleteTrabajoPracticoAsync(trabajo);
            await CargarAsync();
            if (TrabajoSeleccionado?.Id == trabajo.Id)
            {
                LimpiarFormulario();
            }

            Mensaje = "Trabajo práctico eliminado correctamente.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo eliminar el trabajo práctico: {ex.Message}";
        }
    }

    private void LimpiarFormulario()
    {
        TrabajoSeleccionado = null;
        Titulo = string.Empty;
        Descripcion = string.Empty;
        Obligatorio = true;
    }

    private static DatabaseService ObtenerDatabaseService()
    {
        return Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<DatabaseService>()
            ?? throw new InvalidOperationException("DatabaseService no está disponible.");
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
