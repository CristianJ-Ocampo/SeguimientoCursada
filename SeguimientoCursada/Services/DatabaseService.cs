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

    public async Task<List<FechaClase>> GetFechasClaseAsync()
    {
        await InitializeAsync();
        return await _database.Table<FechaClase>()
            .OrderBy(fechaClase => fechaClase.Fecha)
            .ToListAsync();
    }

    public async Task<List<EntregaTrabajo>> GetEntregasAsync()
    {
        await InitializeAsync();
        return await _database.Table<EntregaTrabajo>().ToListAsync();
    }

    public async Task<FechaClase?> GetFechaClaseAsync(DateTime fecha)
    {
        await InitializeAsync();
        var fechaDia = fecha.Date;

        return await _database.Table<FechaClase>()
            .FirstOrDefaultAsync(fechaClase => fechaClase.Fecha == fechaDia);
    }

    public async Task<int> SaveFechaClaseAsync(FechaClase fechaClase)
    {
        ArgumentNullException.ThrowIfNull(fechaClase);
        await InitializeAsync();

        fechaClase.Fecha = fechaClase.Fecha.Date;
        var existente = await GetFechaClaseAsync(fechaClase.Fecha);

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

    public async Task<int> GetTotalClasesAsync()
    {
        await InitializeAsync();
        return await _database.Table<FechaClase>()
            .Where(fechaClase => fechaClase.EsDiaClase)
            .CountAsync();
    }

    public async Task<List<TrabajoPractico>> GetTrabajosPracticosAsync(bool soloObligatorios = false)
    {
        await InitializeAsync();
        var trabajos = await _database.Table<TrabajoPractico>()
            .Where(trabajo => trabajo.Activo)
            .ToListAsync();

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
}
