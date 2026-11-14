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
    private bool _cursaDesdeInicio = true;
    private DateTime _fechaIngresoSeleccionada = DateTime.Today;

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
        GuardarFechaIngresoCommand = new Command(async () => await GuardarFechaIngresoAsync());
        CompletarRegistrosCommand = new Command(async () => await CompletarRegistrosAsync());
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

    public DateTime? FechaIngreso => Estudiante?.FechaIngreso;

    public string FechaIngresoTexto => Estudiante?.FechaIngreso is { } fecha
        ? fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        : "Desde el inicio";

    public bool CursaDesdeInicio
    {
        get => _cursaDesdeInicio;
        set
        {
            if (SetProperty(ref _cursaDesdeInicio, value))
            {
                OnPropertyChanged(nameof(MostrarSelectorFechaIngreso));
            }
        }
    }

    public bool MostrarSelectorFechaIngreso => !CursaDesdeInicio;

    public DateTime FechaIngresoSeleccionada
    {
        get => _fechaIngresoSeleccionada;
        set => SetProperty(ref _fechaIngresoSeleccionada, value);
    }

    public DateTime FechaMaximaIngreso => DateTime.Today;

    public int RegistrosAntesDelIngreso => Historial.Count(asistencia => asistencia.EsAnteriorAlIngreso);

    public bool TieneRegistrosAntesDelIngreso => RegistrosAntesDelIngreso > 0;

    public int TotalInasistencias => Historial.Count(
        asistencia => !asistencia.EsAnteriorAlIngreso &&
                      asistencia.EstadoSeleccionado == EstadoAsistencia.Ausente);

    public int TotalClases => Historial.Count(asistencia => !asistencia.EsAnteriorAlIngreso);

    public string NivelRiesgo { get; private set; } = "Sin datos";

    public string EstadoSeguimiento { get; private set; } = "Sin datos";

    public bool PuedeRegistrarContacto { get; private set; }

    public Color ColorNivelRiesgo => NivelRiesgo switch
    {
        "Bajo" => Colors.Green,
        "Medio" => Colors.Goldenrod,
        "Sin datos" => Colors.Gray,
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

    public Command GuardarFechaIngresoCommand { get; }

    public Command CompletarRegistrosCommand { get; }

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

    private async Task GuardarFechaIngresoAsync()
    {
        if (Estudiante is null)
        {
            Mensaje = "No se seleccionó un estudiante válido.";
            return;
        }

        var fechaIngreso = CursaDesdeInicio
            ? (DateTime?)null
            : FechaIngresoSeleccionada.Date;

        if (fechaIngreso is not null && fechaIngreso > DateTime.Today)
        {
            Mensaje = "La fecha de ingreso no puede ser futura.";
            return;
        }

        try
        {
            Estudiante.FechaIngreso = fechaIngreso;
            await _databaseService.SaveEstudianteAsync(Estudiante);
            await CargarDatosAsync(_legajo);

            Mensaje = fechaIngreso is null
                ? "El alumno ahora cuenta desde la primera clase."
                : $"Fecha de ingreso actualizada al {fechaIngreso:dd/MM/yyyy}.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo guardar la fecha de ingreso: {ex.Message}";
        }
    }

    private async Task CompletarRegistrosAsync()
    {
        if (Estudiante is null || _legajo <= 0)
        {
            Mensaje = "No se seleccionó un estudiante válido.";
            return;
        }

        if (Historial.Count == 0)
        {
            Mensaje = "El alumno no tiene registros de asistencia. Registralos desde la pantalla de Asistencia.";
            return;
        }

        var pagina = Application.Current?.Windows.FirstOrDefault()?.Page;

        if (pagina is null)
        {
            Mensaje = "No se pudo mostrar la confirmación.";
            return;
        }

        var confirmado = await pagina.DisplayAlertAsync(
            "Completar registros",
            "Se crearán registros como Ausente para las clases pasadas sin asistencia, " +
            "contemplando la fecha de ingreso del alumno. ¿Continuar?",
            "Completar",
            "Cancelar");

        if (!confirmado)
        {
            Mensaje = "Operación cancelada.";
            return;
        }

        try
        {
            var fechas = await _databaseService.CompletarAsistenciasFaltantesAsync(_legajo);
            await CargarDatosAsync(_legajo);

            Mensaje = fechas.Count == 0
                ? "No había registros de asistencia faltantes."
                : $"Se completaron {fechas.Count} fecha{(fechas.Count == 1 ? string.Empty : "s")}: " +
                  string.Join(", ", fechas.Select(fecha => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))) + ".";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudieron completar los registros: {ex.Message}";
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
                CursaDesdeInicio = true;
                FechaIngresoSeleccionada = DateTime.Today;
                NivelRiesgo = "Sin datos";
                EstadoSeguimiento = "Sin datos";
                PuedeRegistrarContacto = false;
                MotivoRiesgo = string.Empty;
                Mensaje = $"No se encontró el estudiante con legajo {legajo}.";
                NotificarDatos();
                return;
            }

            Estudiante = estudiante;
            CursaDesdeInicio = estudiante.FechaIngreso is null;
            FechaIngresoSeleccionada = estudiante.FechaIngreso ?? DateTime.Today;

            var asistencias = await _databaseService.GetAsistenciasPorLegajoAsync(_legajo);
            var fechaIngreso = estudiante.FechaIngreso?.Date;

            foreach (var asistencia in asistencias)
            {
                Historial.Add(new AsistenciaHistorialViewModel(
                    asistencia,
                    fechaIngreso is not null && asistencia.Fecha.Date < fechaIngreso));
            }

            var trabajos = await _databaseService.GetTrabajosPracticosAsync(
                soloObligatorios: true,
                comision: estudiante.Comision);
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
        OnPropertyChanged(nameof(FechaIngreso));
        OnPropertyChanged(nameof(FechaIngresoTexto));
        OnPropertyChanged(nameof(RegistrosAntesDelIngreso));
        OnPropertyChanged(nameof(TieneRegistrosAntesDelIngreso));
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
        var fechasClase = await _databaseService.GetFechasClaseAsync();
        var trabajos = await _databaseService.GetTrabajosPracticosAsync(
            soloObligatorios: true,
            comision: Estudiante.Comision);
        var entregas = await _databaseService.GetEntregasPorLegajoAsync(_legajo);
        var resultado = RiesgoEstudianteService.Calcular(
            Estudiante,
            asistencias,
            fechasClase,
            trabajos,
            entregas,
            _configuracionRiesgoService);

        NivelRiesgo = resultado.Nivel.ObtenerEtiqueta();
        EstadoSeguimiento = resultado.ObtenerEstadoSeguimiento().ObtenerEtiqueta();
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

