using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using SeguimientoCursada.Controls;
using SeguimientoCursada.Models;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public class AltaEstudianteViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private readonly HashSet<int> _legajosExistentes = new();
    private string _inputLegajo = string.Empty;
    private string _apellido = string.Empty;
    private string _nombre = string.Empty;
    private string _comisionSeleccionada = "1° A";
    private string _mensajeValidacion = string.Empty;
    private bool _mensajeEsError;
    private bool _seIncorporoTarde;
    private DateTime _fechaIngresoSeleccionada = DateTime.Today;
    private bool _importandoCsv;

    public AltaEstudianteViewModel()
        : this(ObtenerDatabaseService())
    {
    }

    public AltaEstudianteViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        AgregarEstudianteCommand = new Command(async () => await AgregarEstudianteAsync());
        ImportarCsvCommand = new Command(async () => await ImportarCsvAsync());
        VerNominaCommand = new Command(async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//EstudiantesPage");
            }
        });
    }

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
        set => SetProperty(ref _comisionSeleccionada, value);
    }

    public bool SeIncorporoTarde
    {
        get => _seIncorporoTarde;
        set
        {
            if (SetProperty(ref _seIncorporoTarde, value))
            {
                OnPropertyChanged(nameof(MostrarSelectorFechaIngreso));
            }
        }
    }

    public bool MostrarSelectorFechaIngreso => SeIncorporoTarde;

    public DateTime FechaIngresoSeleccionada
    {
        get => _fechaIngresoSeleccionada;
        set => SetProperty(ref _fechaIngresoSeleccionada, value);
    }

    public DateTime FechaMaximaIngreso => DateTime.Today;

    public string MensajeValidacion
    {
        get => _mensajeValidacion;
        private set => SetProperty(ref _mensajeValidacion, value);
    }

    public Color ColorMensaje => _mensajeEsError ? Colors.Red : Colors.DarkGreen;

    public Command AgregarEstudianteCommand { get; }

    public Command ImportarCsvCommand { get; }

    public Command VerNominaCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RecargarAsync()
    {
        try
        {
            var estudiantes = await _databaseService.GetEstudiantesAsync();

            _legajosExistentes.Clear();
            foreach (var estudiante in estudiantes)
            {
                _legajosExistentes.Add(estudiante.Legajo);
            }
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudieron cargar los legajos: {ex.Message}", esError: true);
        }
    }

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

        if (SeIncorporoTarde && FechaIngresoSeleccionada.Date > DateTime.Today)
        {
            await MostrarAlertaAsync("La fecha de ingreso no puede ser futura.");
            return;
        }

        if (_legajosExistentes.Contains(legajo) ||
            EstudiantesViewModel.TodosLosEstudiantes.Any(estudiante => estudiante.Legajo == legajo))
        {
            await MostrarAlertaAsync($"El legajo {legajo} ya está registrado.");
            return;
        }

        var fechaIngreso = SeIncorporoTarde
            ? FechaIngresoSeleccionada.Date
            : await ObtenerFechaIngresoDesdeInicioAsync();

        if (fechaIngreso is null)
        {
            MostrarMensaje(
                "El alta se canceló porque no se indicó la fecha de la primera clase.",
                esError: true);
            return;
        }

        var estudiante = new Estudiante
        {
            Legajo = legajo,
            Apellido = Apellido.Trim(),
            Nombre = Nombre.Trim(),
            Comision = ComisionSeleccionada,
            FechaIngreso = fechaIngreso
        };

        try
        {
            await _databaseService.SaveEstudianteAsync(estudiante);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo guardar el estudiante: {ex.Message}", esError: true);
            return;
        }

        _legajosExistentes.Add(legajo);

        if (!EstudiantesViewModel.TodosLosEstudiantes.Any(e => e.Legajo == legajo))
        {
            EstudiantesViewModel.TodosLosEstudiantes.Add(estudiante);
        }

        var asistenciasCreadas = new List<Asistencia>();
        var mensaje = $"Se agregó a {estudiante.Apellido}, {estudiante.Nombre} en {estudiante.Comision}.";
        var huboErrorAsistencias = false;

        try
        {
            asistenciasCreadas = await _databaseService.CrearAsistenciasInicialesAsync(legajo);

            foreach (var asistencia in asistenciasCreadas)
            {
                if (!AsistenciaViewModel.TodasLasAsistencias.Any(
                        existente => existente.Id == asistencia.Id))
                {
                    AsistenciaViewModel.TodasLasAsistencias.Add(asistencia);
                }
            }
        }
        catch (Exception ex)
        {
            huboErrorAsistencias = true;
            mensaje += $" No se pudieron crear los registros de asistencia: {ex.Message}";
        }

        InputLegajo = string.Empty;
        Apellido = string.Empty;
        Nombre = string.Empty;
        SeIncorporoTarde = false;
        FechaIngresoSeleccionada = DateTime.Today;
        MostrarMensaje(mensaje, esError: huboErrorAsistencias);

        if (asistenciasCreadas.Count > 0)
        {
            await MostrarAlertaAsync(
                "Actualice el presentismo del alumno para las clases ya dictadas si es necesario");
        }
    }

    /// <summary>
    /// Para un alumno que cursó desde el inicio, devuelve la primera fecha de
    /// clase de la comisión. Si todavía no hay clases cargadas, pide confirmar
    /// la fecha de la primera clase y la crea en el calendario.
    /// </summary>
    private async Task<DateTime?> ObtenerFechaIngresoDesdeInicioAsync()
    {
        var primeraClase = await _databaseService.GetPrimeraFechaClaseAsync(ComisionSeleccionada);

        if (primeraClase is not null)
        {
            return primeraClase;
        }

        var fechaConfirmada = await DialogoFecha.MostrarAsync(ComisionSeleccionada);

        if (fechaConfirmada is null)
        {
            return null;
        }

        await _databaseService.SaveFechaClaseAsync(new FechaClase
        {
            Fecha = fechaConfirmada.Value.Date,
            Comision = ComisionSeleccionada.Trim(),
            EsDiaClase = true
        });

        return fechaConfirmada.Value.Date;
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
                var mensajeVacio = resultado.FilasOmitidas switch
                {
                    0 => "El archivo no contiene estudiantes para importar.",
                    1 => "No se encontraron filas válidas. Se omitió 1 fila.",
                    _ => $"No se encontraron filas válidas. Se omitieron {resultado.FilasOmitidas} filas."
                };

                MostrarMensaje(mensajeVacio, esError: true);
                await MostrarAlertaAsync(mensajeVacio);
                return;
            }

            int importados;

            try
            {
                importados = await _databaseService.SaveEstudiantesAsync(resultado.Estudiantes);
            }
            catch (Exception ex)
            {
                MostrarMensaje($"No se pudo guardar la importación: {ex.Message}", esError: true);
                await MostrarAlertaAsync(MensajeValidacion);
                return;
            }

            foreach (var estudiante in resultado.Estudiantes)
            {
                _legajosExistentes.Add(estudiante.Legajo);

                if (!EstudiantesViewModel.TodosLosEstudiantes.Any(e => e.Legajo == estudiante.Legajo))
                {
                    EstudiantesViewModel.TodosLosEstudiantes.Add(estudiante);
                }
            }

            var mensaje = importados == 1
                ? $"Se importó 1 estudiante en la comisión {comision}."
                : $"Se importaron {importados} estudiantes en la comisión {comision}.";

            if (resultado.FilasOmitidas == 1)
            {
                mensaje += "\nSe omitió 1 fila por datos inválidos, fechas futuras o legajos repetidos.";
            }
            else if (resultado.FilasOmitidas > 1)
            {
                mensaje += $"\nSe omitieron {resultado.FilasOmitidas} filas por datos inválidos, fechas futuras o legajos repetidos.";
            }

            MostrarMensaje(mensaje);
            await MostrarAlertaAsync(mensaje);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo importar el archivo: {ex.Message}", esError: true);
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
        var legajosArchivo = new HashSet<int>();
        var primeraLinea = true;
        var primeraClaseComision = await _databaseService.GetPrimeraFechaClaseAsync(comision);

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

            DateTime? fechaIngreso = primeraClaseComision;

            if (campos.Count >= 4 && !string.IsNullOrWhiteSpace(campos[3]))
            {
                if (!TryParseFechaIngreso(campos[3], out var fecha))
                {
                    resultado.FilasOmitidas++;
                    continue;
                }

                fecha = fecha.Date;

                if (fecha > DateTime.Today)
                {
                    resultado.FilasOmitidas++;
                    continue;
                }

                fechaIngreso = fecha;
            }

            if (_legajosExistentes.Contains(legajo) ||
                EstudiantesViewModel.TodosLosEstudiantes.Any(e => e.Legajo == legajo) ||
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
                Comision = comision,
                FechaIngreso = fechaIngreso
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

    private static bool TryParseFechaIngreso(string valor, out DateTime fecha)
    {
        var formatos = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy" };

        return DateTime.TryParseExact(
                   valor.Trim(),
                   formatos,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out fecha) ||
               DateTime.TryParse(
                   valor.Trim(),
                   CultureInfo.CurrentCulture,
                   DateTimeStyles.None,
                   out fecha);
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

    private void MostrarMensaje(string mensaje, bool esError = false)
    {
        MensajeValidacion = mensaje;
        _mensajeEsError = esError;
        OnPropertyChanged(nameof(ColorMensaje));
    }

    private static DatabaseService ObtenerDatabaseService()
    {
        return Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<DatabaseService>()
            ?? throw new InvalidOperationException("DatabaseService no está disponible.");
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

    private sealed class ResultadoImportacionCsv
    {
        public List<Estudiante> Estudiantes { get; } = new();

        public int FilasOmitidas { get; set; }
    }
}
