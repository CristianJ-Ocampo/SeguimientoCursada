using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using SeguimientoCursada.Models;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public class CalendarioClasesViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<string> ComisionesDisponibles = new[] { "1° A", "1° B" };
    private readonly DatabaseService _databaseService;
    private string _comisionSeleccionada = "1° A";
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

    public IReadOnlyList<string> Comisiones => ComisionesDisponibles;

    public string ComisionSeleccionada
    {
        get => _comisionSeleccionada;
        set
        {
            if (SetProperty(ref _comisionSeleccionada, value))
            {
                _ = CargarDatosAsync();
            }
        }
    }

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
            var comision = ComisionSeleccionada.Trim();
            var fechaClase = await _databaseService.GetFechaClaseAsync(FechaSeleccionada, comision);
            var registro = fechaClase ?? new FechaClase();
            registro.Fecha = FechaSeleccionada.Date;
            registro.Descripcion = Descripcion.Trim();
            registro.EsDiaClase = EsDiaClase;
            registro.Comision = comision;

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
            var asistenciasAfectadas = await _databaseService
                .GetAsistenciasAfectadasPorEliminarFechaAsync(fechaClase);

            if (asistenciasAfectadas.Count > 0)
            {
                var mensaje = ConstruirMensajeEliminacion(fechaClase, asistenciasAfectadas);
                var confirmado = await MostrarConfirmacionEliminacionAsync(mensaje);

                if (!confirmado)
                {
                    Mensaje = "Eliminación cancelada.";
                    return;
                }
            }

            var asistenciasEliminadas = await _databaseService
                .DeleteFechaClaseConAsistenciasAsync(fechaClase);

            await CargarDatosAsync();
            Mensaje = asistenciasEliminadas > 0
                ? $"Fecha eliminada junto con {asistenciasEliminadas} registro{(asistenciasEliminadas == 1 ? string.Empty : "s")} de asistencia."
                : "Fecha eliminada correctamente.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo eliminar la fecha: {ex.Message}";
        }
    }

    private static string ConstruirMensajeEliminacion(
        FechaClase fechaClase,
        IReadOnlyList<Asistencia> asistencias)
    {
        var presentes = asistencias.Count(asistencia =>
            EstadoAsistencia.Normalizar(asistencia) == EstadoAsistencia.Presente);
        var ausentes = asistencias.Count(asistencia =>
            EstadoAsistencia.Normalizar(asistencia) == EstadoAsistencia.Ausente);
        var justificados = asistencias.Count(asistencia =>
            EstadoAsistencia.Normalizar(asistencia) == EstadoAsistencia.Justificado);

        var detalle = new List<string>();

        if (presentes > 0)
        {
            detalle.Add($"{presentes} presente{(presentes == 1 ? string.Empty : "s")}");
        }

        if (ausentes > 0)
        {
            detalle.Add($"{ausentes} ausente{(ausentes == 1 ? string.Empty : "s")}");
        }

        if (justificados > 0)
        {
            detalle.Add($"{justificados} justificado{(justificados == 1 ? string.Empty : "s")}");
        }

        var cantidad = asistencias.Count;
        var textoCantidad = cantidad == 1 ? "1 registro" : $"{cantidad} registros";

        return $"La fecha {fechaClase.Fecha:dd/MM/yyyy} ({fechaClase.ComisionMostrada}) tiene " +
               $"{textoCantidad} de asistencia: {string.Join(", ", detalle)}.\n\n" +
               "Si eliminás la clase, también se eliminarán esos registros y dejarán de verse " +
               "en la ficha de los alumnos.";
    }

    private static async Task<bool> MostrarConfirmacionEliminacionAsync(string mensaje)
    {
        var pagina = Application.Current?.Windows.FirstOrDefault()?.Page;

        if (pagina is null)
        {
            return false;
        }

        return await pagina.DisplayAlertAsync(
            "Eliminar clase",
            mensaje,
            "Eliminar",
            "Cancelar");
    }

    private async Task CargarDatosAsync()
    {
        try
        {
            var fechas = await _databaseService.GetFechasClaseAsync(ComisionSeleccionada);
            Fechas.Clear();

            foreach (var fecha in fechas)
            {
                Fechas.Add(fecha);
            }

            TotalClases = await _databaseService.GetTotalClasesAsync(ComisionSeleccionada);
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
