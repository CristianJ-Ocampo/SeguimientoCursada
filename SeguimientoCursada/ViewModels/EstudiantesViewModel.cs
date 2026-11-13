using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SeguimientoCursada.Models;
using SeguimientoCursada.Pages;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public class EstudiantesViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private string _inputLegajo = string.Empty;
    private string _apellido = string.Empty;
    private string _nombre = string.Empty;
    private string _comisionSeleccionada = "1° A";
    private string _mensajeValidacion = string.Empty;
    private readonly ConfiguracionRiesgoService _configuracionRiesgoService;
    private bool _importandoCsv;
    private int _totalClases;
    private IReadOnlyList<TrabajoPractico> _trabajos = Array.Empty<TrabajoPractico>();
    private IReadOnlyList<Asistencia> _asistencias = Array.Empty<Asistencia>();
    private IReadOnlyList<EntregaTrabajo> _entregas = Array.Empty<EntregaTrabajo>();

    public EstudiantesViewModel()
        : this(ObtenerDatabaseService())
    {
    }

    public EstudiantesViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _configuracionRiesgoService = Application.Current?.Handler?.MauiContext?.Services
            .GetRequiredService<ConfiguracionRiesgoService>()
            ?? new ConfiguracionRiesgoService();
        AgregarEstudianteCommand = new Command(async () => await AgregarEstudianteAsync());
        ImportarCsvCommand = new Command(async () => await ImportarCsvAsync());
        AbrirDetalleCommand = new Command(async parameter =>
        {
            if (TryObtenerLegajo(parameter, out var legajo))
            {
                await AbrirDetalleAsync(legajo);
            }
        });
        ActualizarNominaFiltrada();
        _ = CargarEstudiantesAsync();
    }

    public static ObservableCollection<Estudiante> TodosLosEstudiantes { get; } = new();

    public ObservableCollection<Estudiante> Estudiantes => TodosLosEstudiantes;

    public ObservableCollection<EstudianteListadoViewModel> EstudiantesFiltrados { get; } = new();

    private readonly Dictionary<int, ContactoEstudiante> _ultimosContactos = new();

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

    public Command ImportarCsvCommand { get; }

    public Command AbrirDetalleCommand { get; }

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

        var estudiante = new Estudiante
        {
            Legajo = legajo,
            Apellido = Apellido.Trim(),
            Nombre = Nombre.Trim(),
            Comision = ComisionSeleccionada
        };

        try
        {
            await _databaseService.SaveEstudianteAsync(estudiante);
        }
        catch (Exception ex)
        {
            MensajeValidacion = $"No se pudo guardar el estudiante: {ex.Message}";
            return;
        }

        Estudiantes.Add(estudiante);

        InputLegajo = string.Empty;
        Apellido = string.Empty;
        Nombre = string.Empty;
        MensajeValidacion = string.Empty;
        ActualizarNominaFiltrada();
    }

    private async Task ImportarCsvAsync()
    {
        if (_importandoCsv)
        {
            return;
        }

        var comision = ComisionSeleccionada;

        if (string.IsNullOrWhiteSpace(comision))
        {
            await MostrarAlertaAsync("Debe seleccionar una comisión antes de importar estudiantes.");
            return;
        }

        _importandoCsv = true;

        try
        {
            var opciones = new PickOptions
            {
                PickerTitle = "Seleccione un archivo CSV",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".csv" } },
                        { DevicePlatform.Android, new[] { ".csv" } },
                        { DevicePlatform.iOS, new[] { ".csv" } },
                        { DevicePlatform.MacCatalyst, new[] { ".csv" } }
                    })
            };

            var archivo = await FilePicker.Default.PickAsync(opciones);

            if (archivo is null)
            {
                return;
            }

            if (!string.Equals(
                    Path.GetExtension(archivo.FileName),
                    ".csv",
                    StringComparison.OrdinalIgnoreCase))
            {
                await MostrarAlertaAsync("El archivo seleccionado debe tener extensión .csv.");
                return;
            }

            var resultado = await LeerEstudiantesCsvAsync(archivo, comision);

            if (resultado.Estudiantes.Count == 0)
            {
                MensajeValidacion = resultado.FilasOmitidas switch
                {
                    0 => "El archivo no contiene estudiantes para importar.",
                    1 => "No se encontraron filas válidas. Se omitió 1 fila.",
                    _ => $"No se encontraron filas válidas. Se omitieron {resultado.FilasOmitidas} filas."
                };

                await MostrarAlertaAsync(MensajeValidacion);
                return;
            }

            int importados;

            try
            {
                importados = await _databaseService.SaveEstudiantesAsync(resultado.Estudiantes);
            }
            catch (Exception ex)
            {
                MensajeValidacion = $"No se pudo guardar la importación: {ex.Message}";
                await MostrarAlertaAsync(MensajeValidacion);
                return;
            }

            await RecargarAsync();

            var mensaje = importados == 1
                ? $"Se importó 1 estudiante en la comisión {comision}."
                : $"Se importaron {importados} estudiantes en la comisión {comision}.";

            if (resultado.FilasOmitidas == 1)
            {
                mensaje += "\nSe omitió 1 fila por datos inválidos o legajos repetidos.";
            }
            else if (resultado.FilasOmitidas > 1)
            {
                mensaje += $"\nSe omitieron {resultado.FilasOmitidas} filas por datos inválidos o legajos repetidos.";
            }

            MensajeValidacion = string.Empty;
            await MostrarAlertaAsync(mensaje);
        }
        catch (Exception ex)
        {
            MensajeValidacion = $"No se pudo importar el archivo: {ex.Message}";
            await MostrarAlertaAsync(MensajeValidacion);
        }
        finally
        {
            _importandoCsv = false;
        }
    }

    private async Task<ResultadoImportacionCsv> LeerEstudiantesCsvAsync(
        FileResult archivo,
        string comision)
    {
        List<string> lineas;

        try
        {
            lineas = await LeerLineasAsync(
                archivo,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true));
        }
        catch (DecoderFallbackException)
        {
            lineas = await LeerLineasAsync(archivo, Encoding.Latin1);
        }

        var resultado = new ResultadoImportacionCsv();
        var legajosExistentes = Estudiantes
            .Select(estudiante => estudiante.Legajo)
            .ToHashSet();
        var legajosArchivo = new HashSet<int>();
        var primeraLinea = true;

        foreach (var linea in lineas)
        {
            if (string.IsNullOrWhiteSpace(linea))
            {
                continue;
            }

            var delimitador = DetectarDelimitador(linea);
            var campos = ParsearLineaCsv(linea, delimitador);

            if (primeraLinea)
            {
                primeraLinea = false;

                if (EsCabecera(campos))
                {
                    continue;
                }
            }

            if (campos.Count < 3)
            {
                resultado.FilasOmitidas++;
                continue;
            }

            if (!int.TryParse(campos[0].Trim(), out var legajo) || legajo <= 0)
            {
                resultado.FilasOmitidas++;
                continue;
            }

            var apellido = campos[1].Trim();
            var nombre = campos[2].Trim();

            if (string.IsNullOrWhiteSpace(apellido) ||
                string.IsNullOrWhiteSpace(nombre))
            {
                resultado.FilasOmitidas++;
                continue;
            }

            if (legajosExistentes.Contains(legajo) ||
                !legajosArchivo.Add(legajo))
            {
                resultado.FilasOmitidas++;
                continue;
            }

            resultado.Estudiantes.Add(new Estudiante
            {
                Legajo = legajo,
                Apellido = apellido,
                Nombre = nombre,
                Comision = comision
            });
        }

        return resultado;
    }

    private static async Task<List<string>> LeerLineasAsync(
        FileResult archivo,
        Encoding codificacion)
    {
        var lineas = new List<string>();

        using var stream = await archivo.OpenReadAsync();
        using var lector = new StreamReader(
            stream,
            codificacion,
            detectEncodingFromByteOrderMarks: true);

        string? linea;

        while ((linea = await lector.ReadLineAsync()) is not null)
        {
            lineas.Add(linea);
        }

        return lineas;
    }

    private static bool EsCabecera(IReadOnlyList<string> campos)
    {
        if (campos.Count == 0)
        {
            return false;
        }

        var primerCampo = campos[0]
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty);

        return primerCampo.Contains("legajo", StringComparison.OrdinalIgnoreCase);
    }

    private static char DetectarDelimitador(string linea)
    {
        var puntoYComa = 0;
        var coma = 0;
        var entreComillas = false;

        for (var i = 0; i < linea.Length; i++)
        {
            var caracter = linea[i];

            if (caracter == '"')
            {
                if (entreComillas &&
                    i + 1 < linea.Length &&
                    linea[i + 1] == '"')
                {
                    i++;
                }
                else
                {
                    entreComillas = !entreComillas;
                }
            }
            else if (!entreComillas)
            {
                if (caracter == ';')
                {
                    puntoYComa++;
                }
                else if (caracter == ',')
                {
                    coma++;
                }
            }
        }

        return puntoYComa >= coma ? ';' : ',';
    }

    private static List<string> ParsearLineaCsv(string linea, char delimitador)
    {
        var campos = new List<string>();
        var campo = new StringBuilder();
        var entreComillas = false;

        for (var i = 0; i < linea.Length; i++)
        {
            var caracter = linea[i];

            if (caracter == '"')
            {
                if (entreComillas &&
                    i + 1 < linea.Length &&
                    linea[i + 1] == '"')
                {
                    campo.Append('"');
                    i++;
                }
                else
                {
                    entreComillas = !entreComillas;
                }
            }
            else if (caracter == delimitador && !entreComillas)
            {
                campos.Add(campo.ToString().Trim());
                campo.Clear();
            }
            else
            {
                campo.Append(caracter);
            }
        }

        campos.Add(campo.ToString().Trim());

        return campos;
    }

    private sealed class ResultadoImportacionCsv
    {
        public List<Estudiante> Estudiantes { get; } = new();

        public int FilasOmitidas { get; set; }
    }

    private async Task CargarEstudiantesAsync()
    {
        try
        {
            var estudiantes = await _databaseService.GetEstudiantesAsync();
            var contactos = await _databaseService.GetUltimosContactosAsync();
            var totalClases = await _databaseService.GetTotalClasesAsync();
            var trabajos = await _databaseService.GetTrabajosPracticosAsync(soloObligatorios: true);
            var asistencias = await _databaseService.GetAsistenciasAsync();
            var entregas = await _databaseService.GetEntregasAsync();

            _ultimosContactos.Clear();
            foreach (var contacto in contactos)
            {
                _ultimosContactos[contacto.LegajoEstudiante] = contacto;
            }

            Estudiantes.Clear();
            foreach (var estudiante in estudiantes)
            {
                Estudiantes.Add(estudiante);
            }

            _totalClases = totalClases;
            _trabajos = trabajos;
            _asistencias = asistencias;
            _entregas = entregas;

            ActualizarNominaFiltrada();
        }
        catch (Exception ex)
        {
            MensajeValidacion = $"No se pudieron cargar los estudiantes: {ex.Message}";
        }
    }

    public async Task RecargarAsync()
    {
        await CargarEstudiantesAsync();
    }

    private static DatabaseService ObtenerDatabaseService()
    {
        return Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<DatabaseService>()
            ?? throw new InvalidOperationException("DatabaseService no está disponible.");
    }

    private static Task MostrarAlertaAsync(string mensaje)
    {
        return Application.Current?.MainPage?.DisplayAlert("Atención", mensaje, "OK")
            ?? Task.CompletedTask;
    }

    private static async Task AbrirDetalleAsync(int legajo)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(EstudianteDetallePage)}?Legajo={legajo.ToString(CultureInfo.InvariantCulture)}");
    }

    private static bool TryObtenerLegajo(object? parameter, out int legajo)
    {
        return int.TryParse(
            Convert.ToString(parameter, CultureInfo.InvariantCulture),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out legajo) && legajo > 0;
    }

    private void ActualizarNominaFiltrada()
    {
        EstudiantesFiltrados.Clear();

        foreach (var estudiante in Estudiantes
                     .Where(estudiante => estudiante.Comision == ComisionSeleccionada)
                     .OrderBy(estudiante => estudiante.Apellido, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(estudiante => estudiante.Nombre, StringComparer.CurrentCultureIgnoreCase))
        {
            _ultimosContactos.TryGetValue(estudiante.Legajo, out var contacto);
            var resultado = RiesgoEstudianteService.Calcular(
                estudiante,
                _asistencias,
                _totalClases,
                _trabajos,
                _entregas,
                _configuracionRiesgoService);
            EstudiantesFiltrados.Add(new EstudianteListadoViewModel(estudiante, contacto, resultado));
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
