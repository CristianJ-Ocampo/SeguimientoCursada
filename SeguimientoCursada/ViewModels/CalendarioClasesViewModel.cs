using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using SeguimientoCursada.Models;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public class CalendarioClasesViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private DateTime _fechaSeleccionada = DateTime.Today;
    private string _descripcion = string.Empty;
    private bool _esDiaClase = true;
    private int _totalClases;
    private string _mensaje = string.Empty;

    public CalendarioClasesViewModel()
        : this(ObtenerDatabaseService())
    {
    }

    public CalendarioClasesViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        GuardarFechaCommand = new Command(async () => await GuardarFechaAsync());
        CargarFechasCommand = new Command(async () => await CargarDatosAsync());
        _ = CargarDatosAsync();
    }

    public ObservableCollection<FechaClase> Fechas { get; } = new();

    public DateTime FechaSeleccionada
    {
        get => _fechaSeleccionada;
        set => SetProperty(ref _fechaSeleccionada, value);
    }

    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    public bool EsDiaClase
    {
        get => _esDiaClase;
        set => SetProperty(ref _esDiaClase, value);
    }

    public int TotalClases
    {
        get => _totalClases;
        private set => SetProperty(ref _totalClases, value);
    }

    public string Mensaje
    {
        get => _mensaje;
        private set => SetProperty(ref _mensaje, value);
    }

    public Command GuardarFechaCommand { get; }

    public Command CargarFechasCommand { get; }

    public Command<FechaClase> EliminarFechaCommand =>
        new(async fechaClase => await EliminarFechaAsync(fechaClase));

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task GuardarFechaAsync()
    {
        try
        {
            var fechaClase = await _databaseService.GetFechaClaseAsync(FechaSeleccionada);
            var registro = fechaClase ?? new FechaClase();
            registro.Fecha = FechaSeleccionada.Date;
            registro.Descripcion = Descripcion.Trim();
            registro.EsDiaClase = EsDiaClase;

            await _databaseService.SaveFechaClaseAsync(registro);
            await CargarDatosAsync();

            Mensaje = "Fecha guardada correctamente.";
            Descripcion = string.Empty;
            EsDiaClase = true;
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo guardar la fecha: {ex.Message}";
        }
    }

    private async Task EliminarFechaAsync(FechaClase? fechaClase)
    {
        if (fechaClase is null)
        {
            return;
        }

        try
        {
            await _databaseService.DeleteFechaClaseAsync(fechaClase);
            await CargarDatosAsync();
            Mensaje = "Fecha eliminada correctamente.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo eliminar la fecha: {ex.Message}";
        }
    }

    private async Task CargarDatosAsync()
    {
        try
        {
            var fechas = await _databaseService.GetFechasClaseAsync();
            Fechas.Clear();

            foreach (var fecha in fechas)
            {
                Fechas.Add(fecha);
            }

            TotalClases = await _databaseService.GetTotalClasesAsync();
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudieron cargar las fechas: {ex.Message}";
        }
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
