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
    private DateTime? _primeraFechaClase;
    private bool _editando;
    private string _legajoEditable = string.Empty;
    private string _apellidoEditable = string.Empty;
    private string _nombreEditable = string.Empty;
    private string _comisionEditable = string.Empty;

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
        EditarDatosCommand = new Command(IniciarEdicion);
        GuardarCambiosCommand = new Command(async () => await GuardarCambiosAsync());
        CancelarEdicionCommand = new Command(CancelarEdicion);
        EliminarEstudianteCommand = new Command(async () => await EliminarEstudianteAsync());
    }

    public Estudiante? Estudiante { get; private set; }

    public ObservableCollection<AsistenciaHistorialViewModel> Historial { get; } = new();

    public ObservableCollection<EntregaTrabajoViewModel> TrabajosPracticos { get; } = new();

    public ObservableCollection<ContactoHistorialViewModel> Contactos { get; } = new();

    public IReadOnlyList<string> Comisiones { get; } = new[] { "1° A", "1° B" };

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

    public bool Editando
    {
        get => _editando;
        private set
        {
            if (SetProperty(ref _editando, value))
            {
                OnPropertyChanged(nameof(MostrarDatosLectura));
                OnPropertyChanged(nameof(PuedeEditar));
                OnPropertyChanged(nameof(PuedeEliminar));
            }
        }
    }

    public bool MostrarDatosLectura => !Editando && Estudiante is not null;

    public bool PuedeEditar => Estudiante is not null && !Editando;

    public bool PuedeEliminar => Estudiante is not null && !Editando;

    public string LegajoEditable
    {
        get => _legajoEditable;
        set => SetProperty(ref _legajoEditable, value ?? string.Empty);
    }

    public string ApellidoEditable
    {
        get => _apellidoEditable;
        set => SetProperty(ref _apellidoEditable, value ?? string.Empty);
    }

    public string NombreEditable
    {
        get => _nombreEditable;
        set => SetProperty(ref _nombreEditable, value ?? string.Empty);
    }

    public string ComisionEditable
    {
        get => _comisionEditable;
        set => SetProperty(ref _comisionEditable, value ?? string.Empty);
    }

    public DateTime? FechaIngreso => Estudiante?.FechaIngreso;

    public string FechaIngresoTexto => Estudiante?.FechaIngreso is { } fecha
        ? EsPrimeraFechaClase(fecha)
            ? $"{fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} (desde el inicio)"
            : fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        : "Desde el inicio";

    public bool HayPrimeraFechaClase => _primeraFechaClase is not null;

    public string PrimeraFechaClaseTexto => _primeraFechaClase is { } fecha
        ? $"Primera clase: {fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}"
        : "Sin fechas de clase cargadas.";

    private bool EsPrimeraFechaClase(DateTime fecha) =>
        _primeraFechaClase is { } primera && fecha.Date == primera;

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
        "Óptimo" => Colors.Green,
        "Moderado" => Colors.Goldenrod,
        "Sin datos" => Colors.Gray,
        _ => Colors.DarkRed
    };

    public string MotivoRiesgo { get; private set; } = string.Empty;

    public int AusenciasSeguidas { get; private set; }

    public bool MostrarAusenciasSeguidas => AusenciasSeguidas >= 2;

    public string TextoAusenciasSeguidas => $"Faltas seguidas: {AusenciasSeguidas}";

    public decimal PorcentajeAsistencia { get; private set; }

    public int ClasesAsistidas { get; private set; }

    public int TotalClasesConfiguradas { get; private set; }

    public decimal PorcentajeEntregas { get; private set; }

    public int TrabajosEntregados { get; private set; }

    public int TotalTrabajosObligatorios { get; private set; }

    public int TrabajosNoEntregados =>
        Math.Max(0, TotalTrabajosObligatorios - TrabajosEntregados);

    public string ResumenEntregas
    {
        get
        {
            if (TotalTrabajosObligatorios == 0)
            {
                return "Sin trabajos prácticos aplicables.";
            }

            var etiqueta = TrabajosNoEntregados == 1
                ? "trabajo práctico no entregado"
                : "trabajos prácticos no entregados";
            var totales = TotalTrabajosObligatorios == 1 ? "total" : "totales";

            return $"{TrabajosNoEntregados} {etiqueta} de {TotalTrabajosObligatorios} {totales}.";
        }
    }

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

    public Command EditarDatosCommand { get; }

    public Command GuardarCambiosCommand { get; }

    public Command CancelarEdicionCommand { get; }

    public Command EliminarEstudianteCommand { get; }

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

        var eraDesdeInicio = CursaDesdeInicio;
        var fechaIngreso = CursaDesdeInicio
            ? _primeraFechaClase
            : FechaIngresoSeleccionada.Date;

        if (!CursaDesdeInicio && fechaIngreso > DateTime.Today)
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
                ? "El alumno quedará como desde el inicio hasta que se cargue la primera fecha de clase."
                : eraDesdeInicio
                    ? $"El alumno cuenta desde la primera clase ({fechaIngreso:dd/MM/yyyy})."
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

    private void IniciarEdicion()
    {
        if (Estudiante is null)
        {
            Mensaje = "No se seleccionó un estudiante válido.";
            return;
        }

        LegajoEditable = Estudiante.Legajo.ToString(CultureInfo.InvariantCulture);
        ApellidoEditable = Estudiante.Apellido;
        NombreEditable = Estudiante.Nombre;
        ComisionEditable = Estudiante.Comision;
        Mensaje = string.Empty;
        Editando = true;
    }

    private void CancelarEdicion()
    {
        Editando = false;
        Mensaje = "Edición cancelada.";
    }

    private async Task GuardarCambiosAsync()
    {
        if (Estudiante is null)
        {
            Mensaje = "No se seleccionó un estudiante válido.";
            return;
        }

        var legajoTexto = LegajoEditable.Trim();

        if (string.IsNullOrWhiteSpace(legajoTexto) ||
            !legajoTexto.All(char.IsDigit) ||
            !int.TryParse(legajoTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nuevoLegajo) ||
            nuevoLegajo <= 0)
        {
            Mensaje = "El legajo debe ser un número mayor que cero.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ApellidoEditable))
        {
            Mensaje = "El apellido es obligatorio.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NombreEditable))
        {
            Mensaje = "El nombre es obligatorio.";
            return;
        }

        if (!Comisiones.Contains(ComisionEditable))
        {
            Mensaje = "La comisión seleccionada no es válida.";
            return;
        }

        var original = Estudiante;

        try
        {
            if (nuevoLegajo != original.Legajo)
            {
                var legajoEnUso = EstudiantesViewModel.TodosLosEstudiantes
                        .Any(estudiante => estudiante.Legajo == nuevoLegajo) ||
                    await _databaseService.GetEstudiantePorLegajoAsync(nuevoLegajo) is not null;

                if (legajoEnUso)
                {
                    Mensaje = $"El legajo {nuevoLegajo} ya está registrado.";
                    await MostrarAlertaAsync(Mensaje);
                    return;
                }
            }

            var actualizado = new Estudiante
            {
                Legajo = nuevoLegajo,
                Apellido = ApellidoEditable.Trim(),
                Nombre = NombreEditable.Trim(),
                Comision = ComisionEditable,
                FechaIngreso = original.FechaIngreso
            };

            var guardado = await _databaseService.ActualizarEstudianteAsync(original, actualizado);

            if (!guardado)
            {
                Mensaje = "No se pudieron guardar los cambios.";
                return;
            }

            ActualizarColeccionesEnMemoria(nuevoLegajo, actualizado);

            _legajo = nuevoLegajo;
            Editando = false;
            await CargarDatosAsync(_legajo);
            Mensaje = "Datos del estudiante actualizados correctamente.";
        }
        catch (InvalidOperationException ex)
        {
            Mensaje = ex.Message;
            await MostrarAlertaAsync(Mensaje);
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudieron guardar los cambios: {ex.Message}";
        }
    }

    private async Task EliminarEstudianteAsync()
    {
        if (Estudiante is null)
        {
            Mensaje = "No se seleccionó un estudiante válido.";
            return;
        }

        var pagina = Application.Current?.Windows.FirstOrDefault()?.Page;

        if (pagina is null)
        {
            Mensaje = "No se pudo mostrar la confirmación.";
            return;
        }

        try
        {
            var asistencias = (await _databaseService.GetAsistenciasPorLegajoAsync(Estudiante.Legajo)).Count;
            var entregas = (await _databaseService.GetEntregasPorLegajoAsync(Estudiante.Legajo)).Count;
            var contactos = (await _databaseService.GetContactosPorLegajoAsync(Estudiante.Legajo)).Count;

            var confirmado = await pagina.DisplayAlertAsync(
                "Eliminar estudiante",
                $"Se eliminará a {Estudiante.Apellido}, {Estudiante.Nombre} (legajo {Estudiante.Legajo}) " +
                $"junto con {asistencias} asistencia(s), {entregas} entrega(s) y {contactos} contacto(s). " +
                "Esta acción no se puede deshacer. ¿Continuar?",
                "Eliminar",
                "Cancelar");

            if (!confirmado)
            {
                Mensaje = "Eliminación cancelada.";
                return;
            }

            var legajo = Estudiante.Legajo;
            await _databaseService.DeleteEstudianteConDependenciasAsync(Estudiante);
            QuitarDeColeccionesEnMemoria(legajo);
            Estudiante = null;
            Editando = false;
            Mensaje = "Estudiante eliminado correctamente.";

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo eliminar el estudiante: {ex.Message}";
        }
    }

    private void ActualizarColeccionesEnMemoria(int nuevoLegajo, Estudiante actualizado)
    {
        var enMemoria = EstudiantesViewModel.TodosLosEstudiantes
            .FirstOrDefault(estudiante => estudiante.Legajo == _legajo);

        if (enMemoria is not null)
        {
            EstudiantesViewModel.TodosLosEstudiantes.Remove(enMemoria);
        }

        if (!EstudiantesViewModel.TodosLosEstudiantes.Any(estudiante => estudiante.Legajo == nuevoLegajo))
        {
            EstudiantesViewModel.TodosLosEstudiantes.Add(actualizado);
        }

        if (nuevoLegajo == _legajo)
        {
            return;
        }

        foreach (var asistencia in AsistenciaViewModel.TodasLasAsistencias
                     .Where(asistencia => asistencia.LegajoEstudiante == _legajo)
                     .ToList())
        {
            asistencia.LegajoEstudiante = nuevoLegajo;
        }
    }

    private static void QuitarDeColeccionesEnMemoria(int legajo)
    {
        var enMemoria = EstudiantesViewModel.TodosLosEstudiantes
            .FirstOrDefault(estudiante => estudiante.Legajo == legajo);

        if (enMemoria is not null)
        {
            EstudiantesViewModel.TodosLosEstudiantes.Remove(enMemoria);
        }

        foreach (var asistencia in AsistenciaViewModel.TodasLasAsistencias
                     .Where(asistencia => asistencia.LegajoEstudiante == legajo)
                     .ToList())
        {
            AsistenciaViewModel.TodasLasAsistencias.Remove(asistencia);
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
                _primeraFechaClase = null;
                CursaDesdeInicio = true;
                FechaIngresoSeleccionada = DateTime.Today;
                NivelRiesgo = "Sin datos";
                EstadoSeguimiento = "Sin datos";
                PuedeRegistrarContacto = false;
                MotivoRiesgo = string.Empty;
                AusenciasSeguidas = 0;
                Editando = false;
                Mensaje = $"No se encontró el estudiante con legajo {legajo}.";
                NotificarDatos();
                return;
            }

            Estudiante = estudiante;
            _primeraFechaClase = await _databaseService.GetPrimeraFechaClaseAsync(estudiante.Comision);
            CursaDesdeInicio = estudiante.FechaIngreso is null ||
                (_primeraFechaClase is { } primera &&
                 estudiante.FechaIngreso.Value.Date == primera);
            FechaIngresoSeleccionada = estudiante.FechaIngreso ?? _primeraFechaClase ?? DateTime.Today;

            if (!Editando)
            {
                LegajoEditable = estudiante.Legajo.ToString(CultureInfo.InvariantCulture);
                ApellidoEditable = estudiante.Apellido;
                NombreEditable = estudiante.Nombre;
                ComisionEditable = estudiante.Comision;
            }

            var asistencias = await _databaseService.GetAsistenciasPorLegajoAsync(_legajo);
            var fechaIngreso = estudiante.FechaIngreso?.Date;

            foreach (var asistencia in asistencias)
            {
                Historial.Add(new AsistenciaHistorialViewModel(
                    asistencia,
                    fechaIngreso is not null && asistencia.Fecha.Date < fechaIngreso));
            }

            var trabajos = await _databaseService.GetTrabajosPracticosAsync(
                comision: estudiante.Comision);
            var entregas = await _databaseService.GetEntregasPorLegajoAsync(_legajo);
            TrabajosPracticos.Clear();

            foreach (var trabajo in trabajos
                         .Where(trabajo => RiesgoEstudianteService.AplicaAlIngreso(
                             trabajo,
                             estudiante.FechaIngreso))
                         .OrderBy(trabajo => trabajo.Titulo))
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
        OnPropertyChanged(nameof(MostrarDatosLectura));
        OnPropertyChanged(nameof(PuedeEditar));
        OnPropertyChanged(nameof(PuedeEliminar));
        OnPropertyChanged(nameof(Legajo));
        OnPropertyChanged(nameof(Apellido));
        OnPropertyChanged(nameof(Nombre));
        OnPropertyChanged(nameof(Comision));
        OnPropertyChanged(nameof(FechaIngreso));
        OnPropertyChanged(nameof(FechaIngresoTexto));
        OnPropertyChanged(nameof(HayPrimeraFechaClase));
        OnPropertyChanged(nameof(PrimeraFechaClaseTexto));
        OnPropertyChanged(nameof(RegistrosAntesDelIngreso));
        OnPropertyChanged(nameof(TieneRegistrosAntesDelIngreso));
        OnPropertyChanged(nameof(TotalInasistencias));
        OnPropertyChanged(nameof(TotalClases));
        OnPropertyChanged(nameof(NivelRiesgo));
        OnPropertyChanged(nameof(EstadoSeguimiento));
        OnPropertyChanged(nameof(PuedeRegistrarContacto));
        OnPropertyChanged(nameof(ColorNivelRiesgo));
        OnPropertyChanged(nameof(MotivoRiesgo));
        OnPropertyChanged(nameof(AusenciasSeguidas));
        OnPropertyChanged(nameof(MostrarAusenciasSeguidas));
        OnPropertyChanged(nameof(TextoAusenciasSeguidas));
        OnPropertyChanged(nameof(PorcentajeAsistencia));
        OnPropertyChanged(nameof(ClasesAsistidas));
        OnPropertyChanged(nameof(TotalClasesConfiguradas));
        OnPropertyChanged(nameof(PorcentajeEntregas));
        OnPropertyChanged(nameof(TrabajosEntregados));
        OnPropertyChanged(nameof(TotalTrabajosObligatorios));
        OnPropertyChanged(nameof(TrabajosNoEntregados));
        OnPropertyChanged(nameof(ResumenEntregas));
        OnPropertyChanged(nameof(TrabajosPracticos));
        OnPropertyChanged(nameof(Contactos));
    }

    private async Task RecalcularRiesgoAsync()
    {
        if (Estudiante is null)
        {
            return;
        }

        // Se necesitan todas las asistencias para saber en qué clases se tomó
        // presentismo; el servicio filtra por legajo.
        var asistencias = await _databaseService.GetAsistenciasAsync();
        var fechasClase = await _databaseService.GetFechasClaseAsync();
        var trabajos = await _databaseService.GetTrabajosPracticosAsync(
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
        AusenciasSeguidas = resultado.AusenciasSeguidas;
        PorcentajeAsistencia = resultado.PorcentajeAsistencia;
        ClasesAsistidas = resultado.ClasesAsistidas;
        TotalClasesConfiguradas = resultado.TotalClasesConfiguradas;
        PorcentajeEntregas = resultado.PorcentajeEntregas;
        TrabajosEntregados = resultado.TrabajosEntregados;
        TotalTrabajosObligatorios = resultado.TotalTrabajosObligatorios;
        NotificarDatos();
    }

    private static Task MostrarAlertaAsync(string mensaje)
    {
        var pagina = Application.Current?.Windows.FirstOrDefault()?.Page;
        return pagina?.DisplayAlertAsync("Atención", mensaje, "OK") ?? Task.CompletedTask;
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

