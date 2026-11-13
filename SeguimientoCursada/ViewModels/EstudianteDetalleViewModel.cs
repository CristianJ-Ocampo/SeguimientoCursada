using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SeguimientoCursada.Models;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

[QueryProperty(nameof(Legajo), "Legajo")]
public sealed class EstudianteDetalleViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private readonly ConfiguracionRiesgoService _configuracionRiesgoService;
    private string _mensaje = string.Empty;
    private bool _estaCargando;
    private int _legajo;
    private DateTime _fechaContacto = DateTime.Today;
    private TimeSpan _horaContacto = DateTime.Now.TimeOfDay;
    private string _medioContacto = ContactoEstudiante.MediosDisponibles[0];
    private string _motivoContacto = ContactoEstudiante.MotivosDisponibles[0];
    private string _resultadoContacto = string.Empty;

    public EstudianteDetalleViewModel(
        DatabaseService databaseService,
        ConfiguracionRiesgoService configuracionRiesgoService)
    {
        _databaseService = databaseService;
        _configuracionRiesgoService = configuracionRiesgoService;
        VolverCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        ActualizarAsistenciaCommand = new Command<AsistenciaHistorialViewModel>(
            async asistencia => await ActualizarAsistenciaAsync(asistencia));
        ActualizarEntregaCommand = new Command<EntregaTrabajoViewModel>(
            async entrega => await ActualizarEntregaAsync(entrega));
        RegistrarContactoCommand = new Command(async () => await RegistrarContactoAsync());
    }

    public Estudiante? Estudiante { get; private set; }

    public ObservableCollection<AsistenciaHistorialViewModel> Historial { get; } = new();

    public ObservableCollection<EntregaTrabajoViewModel> TrabajosPracticos { get; } = new();

    public ObservableCollection<ContactoHistorialViewModel> Contactos { get; } = new();

    public IReadOnlyList<string> MediosContacto => ContactoEstudiante.MediosDisponibles;

    public IReadOnlyList<string> MotivosContacto => ContactoEstudiante.MotivosDisponibles;

    public DateTime FechaContacto
    {
        get => _fechaContacto;
        set => SetProperty(ref _fechaContacto, value);
    }

    public string MotivoContacto
    {
        get => _motivoContacto;
        set => SetProperty(ref _motivoContacto, value ?? string.Empty);
    }

    public TimeSpan HoraContacto
    {
        get => _horaContacto;
        set => SetProperty(ref _horaContacto, value);
    }

    public string MedioContacto
    {
        get => _medioContacto;
        set => SetProperty(ref _medioContacto, value ?? string.Empty);
    }

    public string ResultadoContacto
    {
        get => _resultadoContacto;
        set => SetProperty(ref _resultadoContacto, value ?? string.Empty);
    }

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

    public string EstadoSeguimiento { get; private set; } = "Sin datos";

    public bool PuedeRegistrarContacto { get; private set; }

    public Color ColorNivelRiesgo => NivelRiesgo switch
    {
        "Bajo" => Colors.Green,
        "Medio" => Colors.Goldenrod,
        _ => Colors.DarkRed
    };

    public string MotivoRiesgo { get; private set; } = string.Empty;

    public decimal PorcentajeAsistencia { get; private set; }

    public int ClasesAsistidas { get; private set; }

    public int TotalClasesConfiguradas { get; private set; }

    public decimal PorcentajeEntregas { get; private set; }

    public int TrabajosEntregados { get; private set; }

    public int TotalTrabajosObligatorios { get; private set; }

    public IReadOnlyList<string> EstadosAsistencia => EstadoAsistencia.Valores;

    public string Mensaje
    {
        get => _mensaje;
        private set => SetProperty(ref _mensaje, value);
    }

    public Command VolverCommand { get; }

    public Command<AsistenciaHistorialViewModel> ActualizarAsistenciaCommand { get; }

    public Command<EntregaTrabajoViewModel> ActualizarEntregaCommand { get; }

    public Command RegistrarContactoCommand { get; }

    public bool EstaCargando
    {
        get => _estaCargando;
        private set => SetProperty(ref _estaCargando, value);
    }

    private async Task RegistrarContactoAsync()
    {
        if (!PuedeRegistrarContacto)
        {
            Mensaje = "Este estudiante no requiere una intervención de seguimiento.";
            return;
        }

        if (_legajo <= 0)
        {
            Mensaje = "No se seleccionó un estudiante válido.";
            return;
        }

        if (string.IsNullOrWhiteSpace(MedioContacto) ||
            string.IsNullOrWhiteSpace(MotivoContacto) ||
            string.IsNullOrWhiteSpace(ResultadoContacto))
        {
            Mensaje = "Seleccioná un medio, un motivo e ingresá el resultado u observación.";
            return;
        }

        try
        {
            await _databaseService.SaveContactoAsync(new ContactoEstudiante
            {
                LegajoEstudiante = _legajo,
                FechaHora = FechaContacto.Date.Add(HoraContacto),
                Medio = MedioContacto,
                Motivo = MotivoContacto,
                Resultado = ResultadoContacto
            });

            await CargarContactosAsync(_legajo);
            FechaContacto = DateTime.Today;
            HoraContacto = DateTime.Now.TimeOfDay;
            MedioContacto = ContactoEstudiante.MediosDisponibles[0];
            MotivoContacto = ContactoEstudiante.MotivosDisponibles[0];
            ResultadoContacto = string.Empty;
            Mensaje = "Contacto registrado correctamente.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo registrar el contacto: {ex.Message}";
        }
    }

    private async Task ActualizarEntregaAsync(EntregaTrabajoViewModel? trabajo)
    {
        if (trabajo is null || trabajo.Trabajo.Id <= 0 || _legajo <= 0)
        {
            Mensaje = "No se seleccionó un trabajo práctico válido.";
            return;
        }

        try
        {
            await _databaseService.SaveEntregaTrabajoAsync(new EntregaTrabajo
            {
                Id = trabajo.Entrega?.Id ?? 0,
                LegajoEstudiante = _legajo,
                TrabajoPracticoId = trabajo.Trabajo.Id,
                Entregado = trabajo.Entregado,
                FechaEntrega = trabajo.Entrega?.FechaEntrega
            });

            var entrega = await _databaseService.GetEntregaTrabajoAsync(_legajo, trabajo.Trabajo.Id);
            trabajo.ActualizarEntrega(entrega);
            await RecalcularRiesgoAsync();
            Mensaje = "Entrega actualizada correctamente.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo actualizar la entrega: {ex.Message}";
        }
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
            Contactos.Clear();

            if (estudiante is null)
            {
                Estudiante = null;
                NivelRiesgo = "Sin datos";
                EstadoSeguimiento = "Sin datos";
                PuedeRegistrarContacto = false;
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

            var trabajos = await _databaseService.GetTrabajosPracticosAsync(soloObligatorios: true);
            var entregas = await _databaseService.GetEntregasPorLegajoAsync(_legajo);
            TrabajosPracticos.Clear();

            foreach (var trabajo in trabajos.OrderBy(trabajo => trabajo.Titulo))
            {
                TrabajosPracticos.Add(new EntregaTrabajoViewModel(
                    trabajo,
                    entregas.FirstOrDefault(entrega => entrega.TrabajoPracticoId == trabajo.Id)));
            }

            await RecalcularRiesgoAsync();
            await CargarContactosAsync(_legajo);
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

    private async Task CargarContactosAsync(int legajo)
    {
        Contactos.Clear();
        if (!PuedeRegistrarContacto)
        {
            return;
        }

        var contactos = await _databaseService.GetContactosPorLegajoAsync(legajo);

        foreach (var contacto in contactos)
        {
            Contactos.Add(new ContactoHistorialViewModel(contacto));
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
        OnPropertyChanged(nameof(EstadoSeguimiento));
        OnPropertyChanged(nameof(PuedeRegistrarContacto));
        OnPropertyChanged(nameof(ColorNivelRiesgo));
        OnPropertyChanged(nameof(MotivoRiesgo));
        OnPropertyChanged(nameof(PorcentajeAsistencia));
        OnPropertyChanged(nameof(ClasesAsistidas));
        OnPropertyChanged(nameof(TotalClasesConfiguradas));
        OnPropertyChanged(nameof(PorcentajeEntregas));
        OnPropertyChanged(nameof(TrabajosEntregados));
        OnPropertyChanged(nameof(TotalTrabajosObligatorios));
        OnPropertyChanged(nameof(TrabajosPracticos));
        OnPropertyChanged(nameof(Contactos));
    }

    private async Task RecalcularRiesgoAsync()
    {
        if (Estudiante is null)
        {
            return;
        }

        var asistencias = await _databaseService.GetAsistenciasPorLegajoAsync(_legajo);
        var totalClases = await _databaseService.GetTotalClasesAsync();
        var trabajos = await _databaseService.GetTrabajosPracticosAsync(soloObligatorios: true);
        var entregas = await _databaseService.GetEntregasPorLegajoAsync(_legajo);
        var resultado = RiesgoEstudianteService.Calcular(
            Estudiante,
            asistencias,
            totalClases,
            trabajos,
            entregas,
            _configuracionRiesgoService);

        NivelRiesgo = resultado.Nivel.ToString();
        EstadoSeguimiento = resultado.ObtenerEstadoSeguimiento().ToString();
        PuedeRegistrarContacto = resultado.RequiereContacto();
        MotivoRiesgo = resultado.Motivo;
        PorcentajeAsistencia = resultado.PorcentajeAsistencia;
        ClasesAsistidas = resultado.ClasesAsistidas;
        TotalClasesConfiguradas = resultado.TotalClasesConfiguradas;
        PorcentajeEntregas = resultado.PorcentajeEntregas;
        TrabajosEntregados = resultado.TrabajosEntregados;
        TotalTrabajosObligatorios = resultado.TotalTrabajosObligatorios;
        NotificarDatos();
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

