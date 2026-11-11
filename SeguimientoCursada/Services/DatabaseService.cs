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
        return await _database.InsertOrReplaceAsync(asistencia);
    }

    public async Task<int> DeleteAsistenciaAsync(Asistencia asistencia)
    {
        ArgumentNullException.ThrowIfNull(asistencia);
        await InitializeAsync();
        return await _database.DeleteAsync(asistencia);
    }
}
