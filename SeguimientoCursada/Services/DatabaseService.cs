using SQLite;
using System.Diagnostics;
using SeguimientoCursada.Models;

namespace SeguimientoCursada.Services;

public sealed class DatabaseService
{
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public DatabaseService()
    {
        var databasePath = Path.Combine(
            FileSystem.AppDataDirectory,
            "seguimiento_cursada.db3");

        _database = new SQLiteAsyncConnection(databasePath);
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            await _database.CreateTableAsync<Estudiante>();
            await _database.CreateTableAsync<Asistencia>();
            await _database.CreateTableAsync<FechaClase>();
            await _database.CreateTableAsync<TrabajoPractico>();
            await _database.CreateTableAsync<EntregaTrabajo>();
            await _database.CreateTableAsync<ContactoEstudiante>();
            await MigrarEstadosAsistenciaAsync();
            await MigrarContactosAsync();
            await MigrarComisionTrabajosPracticosAsync();
            await MigrarComisionFechasClaseAsync();
            await MigrarFechaIngresoEstudiantesAsync();
            await LimpiarAsistenciasHuerfanasAsync();
            await RepararAsistenciasFaltantesAsync();
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<List<Estudiante>> GetEstudiantesAsync()
    {
        await InitializeAsync();
        return await _database.Table<Estudiante>().ToListAsync();
    }

    public async Task<Estudiante?> GetEstudianteAsync(int legajo)
    {
        return await GetEstudiantePorLegajoAsync(legajo);
    }

    public async Task<Estudiante?> GetEstudiantePorLegajoAsync(int legajo)
    {
        await InitializeAsync();
        Debug.WriteLine($"[DatabaseService] Buscando estudiante por Legajo={legajo}");

        var estudiante = await _database.Table<Estudiante>()
            .FirstOrDefaultAsync(e => e.Legajo == legajo);

        Debug.WriteLine(estudiante is null
            ? $"[DatabaseService] Estudiante no encontrado para Legajo={legajo}"
            : $"[DatabaseService] Estudiante encontrado para Legajo={legajo}: {estudiante.Apellido}, {estudiante.Nombre}");

        return estudiante;
    }

    public async Task<int> SaveEstudianteAsync(Estudiante estudiante)
    {
        ArgumentNullException.ThrowIfNull(estudiante);
        await InitializeAsync();
        return await _database.InsertOrReplaceAsync(estudiante);
    }

    public async Task<int> SaveEstudiantesAsync(IReadOnlyList<Estudiante> estudiantes)
    {
        ArgumentNullException.ThrowIfNull(estudiantes);

        if (estudiantes.Count == 0)
        {
            return 0;
        }

        await InitializeAsync();
        return await _database.InsertAllAsync(estudiantes, runInTransaction: true);
    }

    public async Task<int> DeleteEstudianteAsync(Estudiante estudiante)
    {
        ArgumentNullException.ThrowIfNull(estudiante);
        await InitializeAsync();
        return await _database.DeleteAsync(estudiante);
    }

    public async Task<List<Asistencia>> GetAsistenciasAsync()
    {
        await InitializeAsync();
        return await _database.Table<Asistencia>().ToListAsync();
    }

    public async Task<List<Asistencia>> GetAsistenciasPorLegajoAsync(int legajo)
    {
        await InitializeAsync();
        Debug.WriteLine($"[DatabaseService] Buscando asistencias para Legajo={legajo}");

        var asistencias = await _database.Table<Asistencia>()
            .Where(a => a.LegajoEstudiante == legajo)
            .ToListAsync();

        Debug.WriteLine($"[DatabaseService] Asistencias encontradas para Legajo={legajo}: {asistencias.Count}");

        return asistencias;
    }

    /// <summary>
    /// Crea registros de asistencia como Ausente para clases ya dictadas en las que el
    /// alumno no tiene registro, contemplando su fecha de ingreso. Solo considera alumnos
    /// que ya tienen al menos un registro de asistencia, para no alterar a quienes
    /// todavía no fueron registrados.
    /// Devuelve las fechas que se completaron.
    /// </summary>
    public async Task<List<DateTime>> CompletarAsistenciasFaltantesAsync(int? legajo = null)
    {
        await InitializeAsync();
        return await CompletarAsistenciasFaltantesCoreAsync(legajo);
    }

    private async Task<List<DateTime>> CompletarAsistenciasFaltantesCoreAsync(int? legajo = null)
    {
        var estudiantes = await _database.Table<Estudiante>().ToListAsync();
        var fechasClase = await _database.Table<FechaClase>()
            .Where(fecha => fecha.EsDiaClase)
            .ToListAsync();
        var asistencias = await _database.Table<Asistencia>().ToListAsync();

        var legajosConRegistros = asistencias
            .Select(asistencia => asistencia.LegajoEstudiante)
            .ToHashSet();

        var estudiantesObjetivo = estudiantes
            .Where(estudiante => legajo is null || estudiante.Legajo == legajo.Value)
            .Where(estudiante => legajosConRegistros.Contains(estudiante.Legajo))
            .ToList();

        var hoy = DateTime.Today;
        var fechasCompletadas = new List<DateTime>();

        foreach (var estudiante in estudiantesObjetivo)
        {
            var comisionEstudiante = (estudiante.Comision ?? string.Empty).Trim();
            var fechaIngreso = estudiante.FechaIngreso?.Date;

            foreach (var fechaClase in fechasClase)
            {
                var fecha = fechaClase.Fecha.Date;

                if (fecha > hoy)
                {
                    continue;
                }

                if (fechaIngreso is not null && fecha < fechaIngreso)
                {
                    continue;
                }

                var comisionClase = (fechaClase.Comision ?? string.Empty).Trim();

                if (!string.IsNullOrEmpty(comisionClase) &&
                    !string.Equals(comisionClase, comisionEstudiante, StringComparison.Ordinal))
                {
                    continue;
                }

                var comisionRegistro = string.IsNullOrEmpty(comisionClase)
                    ? comisionEstudiante
                    : comisionClase;

                var yaExiste = asistencias.Any(asistencia =>
                    asistencia.LegajoEstudiante == estudiante.Legajo &&
                    asistencia.Fecha.Date == fecha &&
                    string.Equals(
                        (asistencia.Comision ?? string.Empty).Trim(),
                        comisionRegistro,
                        StringComparison.Ordinal));

                if (yaExiste)
                {
                    continue;
                }

                var nuevaAsistencia = new Asistencia
                {
                    LegajoEstudiante = estudiante.Legajo,
                    Fecha = fecha,
                    Presente = false,
                    Estado = EstadoAsistencia.Ausente,
                    Comision = comisionRegistro
                };

                await _database.InsertAsync(nuevaAsistencia);
                asistencias.Add(nuevaAsistencia);

                if (!fechasCompletadas.Contains(fecha))
                {
                    fechasCompletadas.Add(fecha);
                }
            }
        }

        return fechasCompletadas
            .OrderBy(fecha => fecha)
            .ToList();
    }

    public async Task<int> SaveAsistenciaAsync(Asistencia asistencia)
    {
        ArgumentNullException.ThrowIfNull(asistencia);
        await InitializeAsync();
        EstadoAsistencia.Aplicar(asistencia, EstadoAsistencia.Normalizar(asistencia));
        return await _database.InsertOrReplaceAsync(asistencia);
    }

    public async Task<int> UpdateAsistenciaAsync(Asistencia asistencia)
    {
        ArgumentNullException.ThrowIfNull(asistencia);

        if (asistencia.Id <= 0)
        {
            throw new ArgumentException("La asistencia debe tener un Id válido.", nameof(asistencia));
        }

        await InitializeAsync();
        EstadoAsistencia.Aplicar(asistencia, EstadoAsistencia.Normalizar(asistencia));
        return await _database.UpdateAsync(asistencia);
    }

    public async Task<int> DeleteAsistenciaAsync(Asistencia asistencia)
    {
        ArgumentNullException.ThrowIfNull(asistencia);
        await InitializeAsync();
        return await _database.DeleteAsync(asistencia);
    }

    public async Task<List<FechaClase>> GetFechasClaseAsync(string? comision = null)
    {
        await InitializeAsync();
        var fechas = await _database.Table<FechaClase>()
            .OrderBy(fechaClase => fechaClase.Fecha)
            .ToListAsync();

        if (comision is null)
        {
            return fechas;
        }

        return fechas
            .Where(fechaClase => string.IsNullOrWhiteSpace(fechaClase.Comision) ||
                                 string.Equals(fechaClase.Comision, comision, StringComparison.Ordinal))
            .ToList();
    }

    public async Task<List<EntregaTrabajo>> GetEntregasAsync()
    {
        await InitializeAsync();
        return await _database.Table<EntregaTrabajo>().ToListAsync();
    }

    public async Task<FechaClase?> GetFechaClaseAsync(DateTime fecha, string comision)
    {
        await InitializeAsync();
        var fechaDia = fecha.Date;
        var comisionNormalizada = comision?.Trim() ?? string.Empty;

        return await _database.Table<FechaClase>()
            .FirstOrDefaultAsync(fechaClase => fechaClase.Fecha == fechaDia &&
                                               fechaClase.Comision == comisionNormalizada);
    }

    public async Task<int> SaveFechaClaseAsync(FechaClase fechaClase)
    {
        ArgumentNullException.ThrowIfNull(fechaClase);
        await InitializeAsync();

        fechaClase.Fecha = fechaClase.Fecha.Date;
        fechaClase.Comision = fechaClase.Comision?.Trim() ?? string.Empty;
        var existente = await GetFechaClaseAsync(fechaClase.Fecha, fechaClase.Comision);

        if (existente is not null && existente.Id != fechaClase.Id)
        {
            fechaClase.Id = existente.Id;
        }

        return fechaClase.Id > 0
            ? await _database.UpdateAsync(fechaClase)
            : await _database.InsertAsync(fechaClase);
    }

    public async Task<int> DeleteFechaClaseAsync(FechaClase fechaClase)
    {
        ArgumentNullException.ThrowIfNull(fechaClase);
        await InitializeAsync();
        return await _database.DeleteAsync(fechaClase);
    }

    /// <summary>
    /// Devuelve las asistencias que quedarían sin fecha de clase si se elimina la fecha indicada.
    /// Contempla que exista otra fecha global o específica para el mismo día y comisión.
    /// </summary>
    public async Task<List<Asistencia>> GetAsistenciasAfectadasPorEliminarFechaAsync(FechaClase fechaClase)
    {
        ArgumentNullException.ThrowIfNull(fechaClase);
        await InitializeAsync();

        var fecha = fechaClase.Fecha.Date;

        var fechasDelDia = await _database.Table<FechaClase>()
            .Where(registro => registro.Fecha == fecha)
            .ToListAsync();

        var asistenciasDelDia = await _database.Table<Asistencia>()
            .Where(asistencia => asistencia.Fecha == fecha)
            .ToListAsync();

        var fechasRestantes = fechasDelDia
            .Where(registro => registro.Id != fechaClase.Id)
            .ToList();

        return asistenciasDelDia
            .Where(asistencia =>
            {
                var comision = (asistencia.Comision ?? string.Empty).Trim();

                return FechaAplicaAComision(fechasDelDia, fecha, comision) &&
                       !FechaAplicaAComision(fechasRestantes, fecha, comision);
            })
            .ToList();
    }

    /// <summary>
    /// Elimina la fecha de clase junto con las asistencias que quedarían huérfanas.
    /// Devuelve la cantidad de asistencias eliminadas.
    /// </summary>
    public async Task<int> DeleteFechaClaseConAsistenciasAsync(FechaClase fechaClase)
    {
        ArgumentNullException.ThrowIfNull(fechaClase);
        await InitializeAsync();

        var asistenciasAfectadas = await GetAsistenciasAfectadasPorEliminarFechaAsync(fechaClase);

        await _database.RunInTransactionAsync(conexion =>
        {
            foreach (var asistencia in asistenciasAfectadas)
            {
                conexion.Delete(asistencia);
            }

            conexion.Delete(fechaClase);
        });

        return asistenciasAfectadas.Count;
    }

    private static bool FechaAplicaAComision(
        IEnumerable<FechaClase> fechas,
        DateTime fecha,
        string comision)
    {
        var fechaDia = fecha.Date;

        return fechas.Any(fechaClase =>
            fechaClase.Fecha.Date == fechaDia &&
            fechaClase.EsDiaClase &&
            (string.IsNullOrWhiteSpace(fechaClase.Comision) ||
             string.Equals(
                 fechaClase.Comision.Trim(),
                 comision,
                 StringComparison.Ordinal)));
    }

    /// <summary>
    /// Garantiza que la fecha exista en el calendario como día de cursada para la comisión.
    /// Devuelve true si se creó o se actualizó.
    /// </summary>
    public async Task<bool> EnsureFechaClaseAsync(DateTime fecha, string comision)
    {
        await InitializeAsync();
        var fechaDia = fecha.Date;
        var comisionNormalizada = comision?.Trim() ?? string.Empty;

        var existente = await GetFechaClaseAsync(fechaDia, comisionNormalizada);
        if (existente is not null)
        {
            if (existente.EsDiaClase)
            {
                return false;
            }

            existente.EsDiaClase = true;
            await _database.UpdateAsync(existente);
            return true;
        }

        // Un día de cursada global ya cubre a esta comisión.
        var global = await GetFechaClaseAsync(fechaDia, string.Empty);
        if (global is not null && global.EsDiaClase)
        {
            return false;
        }

        await _database.InsertAsync(new FechaClase
        {
            Fecha = fechaDia,
            EsDiaClase = true,
            Comision = comisionNormalizada
        });
        return true;
    }

    public async Task<int> GetTotalClasesAsync(string? comision = null)
    {
        var fechas = await GetFechasClaseAsync(comision);

        return fechas
            .Where(fechaClase => fechaClase.EsDiaClase)
            .Select(fechaClase => fechaClase.Fecha.Date)
            .Distinct()
            .Count();
    }

    public async Task<List<TrabajoPractico>> GetTrabajosPracticosAsync(
        bool soloObligatorios = false,
        string? comision = null)
    {
        await InitializeAsync();
        var trabajos = await _database.Table<TrabajoPractico>()
            .Where(trabajo => trabajo.Activo)
            .ToListAsync();

        if (comision is not null)
        {
            trabajos = trabajos
                .Where(trabajo => string.IsNullOrWhiteSpace(trabajo.Comision) ||
                                  string.Equals(trabajo.Comision, comision, StringComparison.Ordinal))
                .ToList();
        }

        return soloObligatorios
            ? trabajos.Where(trabajo => trabajo.Obligatorio).ToList()
            : trabajos;
    }

    public async Task<TrabajoPractico?> GetTrabajoPracticoAsync(int id)
    {
        await InitializeAsync();
        return await _database.Table<TrabajoPractico>()
            .FirstOrDefaultAsync(trabajo => trabajo.Id == id);
    }

    public async Task<int> SaveTrabajoPracticoAsync(TrabajoPractico trabajo)
    {
        ArgumentNullException.ThrowIfNull(trabajo);

        if (string.IsNullOrWhiteSpace(trabajo.Titulo))
        {
            throw new ArgumentException("El título del trabajo práctico es obligatorio.", nameof(trabajo));
        }

        await InitializeAsync();
        trabajo.Titulo = trabajo.Titulo.Trim();
        trabajo.Descripcion = trabajo.Descripcion?.Trim() ?? string.Empty;
        trabajo.Comision = trabajo.Comision?.Trim() ?? string.Empty;

        return trabajo.Id > 0
            ? await _database.UpdateAsync(trabajo)
            : await _database.InsertAsync(trabajo);
    }

    public async Task<int> DeleteTrabajoPracticoAsync(TrabajoPractico trabajo)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        await InitializeAsync();

        var entregas = await _database.Table<EntregaTrabajo>()
            .Where(entrega => entrega.TrabajoPracticoId == trabajo.Id)
            .ToListAsync();

        foreach (var entrega in entregas)
        {
            await _database.DeleteAsync(entrega);
        }

        return await _database.DeleteAsync(trabajo);
    }

    public async Task<List<EntregaTrabajo>> GetEntregasPorLegajoAsync(int legajo)
    {
        await InitializeAsync();
        return await _database.Table<EntregaTrabajo>()
            .Where(entrega => entrega.LegajoEstudiante == legajo)
            .ToListAsync();
    }

    public async Task<EntregaTrabajo?> GetEntregaTrabajoAsync(int legajo, int trabajoPracticoId)
    {
        await InitializeAsync();
        return await _database.Table<EntregaTrabajo>()
            .FirstOrDefaultAsync(entrega => entrega.LegajoEstudiante == legajo &&
                                            entrega.TrabajoPracticoId == trabajoPracticoId);
    }

    public async Task<int> SaveEntregaTrabajoAsync(EntregaTrabajo entrega)
    {
        ArgumentNullException.ThrowIfNull(entrega);

        if (entrega.LegajoEstudiante <= 0 || entrega.TrabajoPracticoId <= 0)
        {
            throw new ArgumentException("La entrega debe tener un estudiante y un trabajo práctico válidos.", nameof(entrega));
        }

        await InitializeAsync();
        var existente = await GetEntregaTrabajoAsync(entrega.LegajoEstudiante, entrega.TrabajoPracticoId);

        if (existente is not null && existente.Id != entrega.Id)
        {
            entrega.Id = existente.Id;
        }

        entrega.FechaEntrega = entrega.Entregado
            ? entrega.FechaEntrega ?? DateTime.Now
            : null;

        return entrega.Id > 0
            ? await _database.UpdateAsync(entrega)
            : await _database.InsertAsync(entrega);
    }

    public async Task<int> DeleteEntregaTrabajoAsync(EntregaTrabajo entrega)
    {
        ArgumentNullException.ThrowIfNull(entrega);
        await InitializeAsync();
        return await _database.DeleteAsync(entrega);
    }

    public async Task<List<ContactoEstudiante>> GetContactosPorLegajoAsync(int legajo)
    {
        await InitializeAsync();
        return await _database.Table<ContactoEstudiante>()
            .Where(contacto => contacto.LegajoEstudiante == legajo)
            .OrderByDescending(contacto => contacto.FechaHora)
            .ToListAsync();
    }

    public async Task<List<ContactoEstudiante>> GetUltimosContactosAsync()
    {
        await InitializeAsync();
        var contactos = await _database.Table<ContactoEstudiante>().ToListAsync();

        return contactos
            .GroupBy(contacto => contacto.LegajoEstudiante)
            .Select(grupo => grupo.OrderByDescending(contacto => contacto.FechaHora).First())
            .ToList();
    }

    public async Task<int> SaveContactoAsync(ContactoEstudiante contacto)
    {
        ArgumentNullException.ThrowIfNull(contacto);

        if (contacto.LegajoEstudiante <= 0)
        {
            throw new ArgumentException("El contacto debe tener un estudiante válido.", nameof(contacto));
        }

        if (string.IsNullOrWhiteSpace(contacto.Medio) ||
            !ContactoEstudiante.MediosDisponibles.Contains(contacto.Medio, StringComparer.Ordinal))
        {
            throw new ArgumentException("El medio de contacto no es válido.", nameof(contacto));
        }

        if (string.IsNullOrWhiteSpace(contacto.Motivo) ||
            !ContactoEstudiante.MotivosDisponibles.Contains(contacto.Motivo, StringComparer.Ordinal))
        {
            throw new ArgumentException("El motivo de contacto no es válido.", nameof(contacto));
        }

        if (string.IsNullOrWhiteSpace(contacto.Resultado))
        {
            throw new ArgumentException("El resultado u observación del contacto es obligatorio.", nameof(contacto));
        }

        await InitializeAsync();
        contacto.Medio = contacto.Medio.Trim();
        contacto.Motivo = contacto.Motivo.Trim();
        contacto.Resultado = contacto.Resultado.Trim();
        contacto.FechaHora = contacto.FechaHora == default ? DateTime.Now : contacto.FechaHora;

        return contacto.Id > 0
            ? await _database.UpdateAsync(contacto)
            : await _database.InsertAsync(contacto);
    }

    private async Task MigrarEstadosAsistenciaAsync()
    {
        var columnaExiste = await _database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('Asistencia') WHERE name = 'Estado'");

        if (columnaExiste == 0)
        {
            await _database.ExecuteAsync(
                "ALTER TABLE Asistencia ADD COLUMN Estado TEXT NOT NULL DEFAULT ''");
        }

        var asistencias = await _database.Table<Asistencia>().ToListAsync();

        foreach (var asistencia in asistencias)
        {
            var estadoNormalizado = EstadoAsistencia.Normalizar(asistencia);

            if (asistencia.Estado != estadoNormalizado ||
                asistencia.Presente != (estadoNormalizado == EstadoAsistencia.Presente))
            {
                EstadoAsistencia.Aplicar(asistencia, estadoNormalizado);
                await _database.UpdateAsync(asistencia);
            }
        }
    }

    private async Task MigrarContactosAsync()
    {
        var columnaExiste = await _database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('ContactoEstudiante') WHERE name = 'Motivo'");

        if (columnaExiste == 0)
        {
            await _database.ExecuteAsync(
                "ALTER TABLE ContactoEstudiante ADD COLUMN Motivo TEXT NOT NULL DEFAULT ''");
        }
    }

    private async Task MigrarFechaIngresoEstudiantesAsync()
    {
        var columnaExiste = await _database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('Estudiante') WHERE name = 'FechaIngreso'");

        if (columnaExiste == 0)
        {
            await _database.ExecuteAsync(
                "ALTER TABLE Estudiante ADD COLUMN FechaIngreso INTEGER");
        }
    }

    /// <summary>
    /// Repara los registros faltantes de alumnos ya registrados para que el historial
    /// refleje todas las clases desde su ingreso. Se ejecuta una sola vez.
    /// </summary>
    private async Task RepararAsistenciasFaltantesAsync()
    {
        const string clave = "migracion_asistencias_faltantes_v1";

        if (Preferences.Default.Get(clave, false))
        {
            return;
        }

        await CompletarAsistenciasFaltantesCoreAsync();
        Preferences.Default.Set(clave, true);
    }

    private async Task MigrarComisionTrabajosPracticosAsync()
    {
        var columnaExiste = await _database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('TrabajoPractico') WHERE name = 'Comision'");

        if (columnaExiste == 0)
        {
            await _database.ExecuteAsync(
                "ALTER TABLE TrabajoPractico ADD COLUMN Comision TEXT NOT NULL DEFAULT ''");
        }

        // Si la columna la agregó sqlite-net, las filas previas pueden quedar en NULL.
        await _database.ExecuteAsync(
            "UPDATE TrabajoPractico SET Comision = '' WHERE Comision IS NULL");

        const string clave = "migracion_comision_trabajos_v1";

        if (Preferences.Default.Get(clave, false))
        {
            return;
        }

        await InferirComisionesTrabajosAsync();
        Preferences.Default.Set(clave, true);
    }

    private async Task InferirComisionesTrabajosAsync()
    {
        var trabajos = await _database.Table<TrabajoPractico>().ToListAsync();

        if (trabajos.Count == 0)
        {
            return;
        }

        var entregas = await _database.Table<EntregaTrabajo>()
            .Where(entrega => entrega.Entregado)
            .ToListAsync();

        if (entregas.Count == 0)
        {
            return;
        }

        var estudiantes = await _database.Table<Estudiante>().ToListAsync();
        var comisionPorLegajo = estudiantes
            .Where(estudiante => !string.IsNullOrWhiteSpace(estudiante.Comision))
            .GroupBy(estudiante => estudiante.Legajo)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First().Comision);

        foreach (var trabajo in trabajos.Where(trabajo => string.IsNullOrWhiteSpace(trabajo.Comision)))
        {
            var comisiones = entregas
                .Where(entrega => entrega.TrabajoPracticoId == trabajo.Id)
                .Select(entrega => comisionPorLegajo.TryGetValue(entrega.LegajoEstudiante, out var comision)
                    ? comision
                    : null)
                .Where(comision => !string.IsNullOrWhiteSpace(comision))
                .Select(comision => comision!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (comisiones.Count != 1)
            {
                continue;
            }

            trabajo.Comision = comisiones[0];
            await _database.UpdateAsync(trabajo);
        }
    }

    private async Task MigrarComisionFechasClaseAsync()
    {
        var columnaExiste = await _database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('FechaClase') WHERE name = 'Comision'");

        if (columnaExiste == 0)
        {
            await _database.ExecuteAsync(
                "ALTER TABLE FechaClase ADD COLUMN Comision TEXT NOT NULL DEFAULT ''");
        }

        await _database.ExecuteAsync(
            "UPDATE FechaClase SET Comision = '' WHERE Comision IS NULL");

        const string clave = "migracion_comision_fechas_v1";

        if (Preferences.Default.Get(clave, false))
        {
            return;
        }

        await BackfillFechasClaseDesdeAsistenciasAsync();
        Preferences.Default.Set(clave, true);
    }

    private async Task BackfillFechasClaseDesdeAsistenciasAsync()
    {
        var asistencias = await _database.Table<Asistencia>().ToListAsync();

        if (asistencias.Count == 0)
        {
            return;
        }

        var fechas = await _database.Table<FechaClase>().ToListAsync();

        foreach (var grupo in asistencias.GroupBy(asistencia => new
                 {
                     Fecha = asistencia.Fecha.Date,
                     Comision = (asistencia.Comision ?? string.Empty).Trim()
                 }))
        {
            var fecha = grupo.Key.Fecha;
            var comision = grupo.Key.Comision;

            var yaExiste = fechas.Any(fechaClase =>
                fechaClase.Fecha.Date == fecha &&
                fechaClase.EsDiaClase &&
                (string.IsNullOrWhiteSpace(fechaClase.Comision) ||
                 string.Equals(fechaClase.Comision, comision, StringComparison.Ordinal)));

            if (yaExiste)
            {
                continue;
            }

            var nuevaFecha = new FechaClase
            {
                Fecha = fecha,
                EsDiaClase = true,
                Descripcion = "Clase registrada por asistencia",
                Comision = comision
            };

            await _database.InsertAsync(nuevaFecha);
            fechas.Add(nuevaFecha);
        }
    }

    /// <summary>
    /// Elimina asistencias que hayan quedado sin una fecha de clase aplicable para su comisión.
    /// Se ejecuta una sola vez para reconciliar datos de versiones anteriores.
    /// </summary>
    private async Task LimpiarAsistenciasHuerfanasAsync()
    {
        const string clave = "migracion_limpieza_asistencias_huerfanas_v2";

        if (Preferences.Default.Get(clave, false))
        {
            return;
        }

        var fechas = await _database.Table<FechaClase>()
            .Where(fechaClase => fechaClase.EsDiaClase)
            .ToListAsync();

        var asistencias = await _database.Table<Asistencia>().ToListAsync();

        foreach (var asistencia in asistencias)
        {
            var comision = (asistencia.Comision ?? string.Empty).Trim();

            if (!FechaAplicaAComision(fechas, asistencia.Fecha, comision))
            {
                await _database.DeleteAsync(asistencia);
            }
        }

        Preferences.Default.Set(clave, true);
    }
}
