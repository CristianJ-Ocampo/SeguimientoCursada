using SeguimientoCursada.Models;

namespace SeguimientoCursada.Services;

public enum NivelRiesgo
{
    Bajo,
    Medio,
    Alto
}

public enum EstadoSeguimiento
{
    Optimo,
    Regular,
    EnRiesgo,
    Critico
}

public sealed record ResultadoRiesgo(
    NivelRiesgo Nivel,
    int Inasistencias,
    string Motivo,
    int ClasesAsistidas = 0,
    int TotalClasesConfiguradas = 0,
    decimal PorcentajeAsistencia = 0,
    int TrabajosEntregados = 0,
    int TotalTrabajosObligatorios = 0,
    decimal PorcentajeEntregas = 0);

public static class ResultadoRiesgoExtensions
{
    public static EstadoSeguimiento ObtenerEstadoSeguimiento(this ResultadoRiesgo resultado)
    {
        return resultado.Nivel switch
        {
            NivelRiesgo.Bajo => EstadoSeguimiento.Optimo,
            NivelRiesgo.Medio => EstadoSeguimiento.EnRiesgo,
            NivelRiesgo.Alto => EstadoSeguimiento.Critico,
            _ => EstadoSeguimiento.Optimo
        };
    }

    public static bool RequiereContacto(this ResultadoRiesgo resultado) =>
        resultado.ObtenerEstadoSeguimiento() is EstadoSeguimiento.EnRiesgo or EstadoSeguimiento.Critico;
}

public static class RiesgoEstudianteService
{
    public const int UmbralRiesgoAlto = 3;
    public const int UmbralRiesgoMedio = 1;

    public static ResultadoRiesgo Calcular(Estudiante estudiante, IEnumerable<Asistencia> asistencias)
    {
        var inasistencias = asistencias.Count(asistencia =>
            asistencia.LegajoEstudiante == estudiante.Legajo &&
            EstadoAsistencia.Normalizar(asistencia) == EstadoAsistencia.Ausente);

        var nivel = inasistencias switch
        {
            >= UmbralRiesgoAlto => NivelRiesgo.Alto,
            >= UmbralRiesgoMedio => NivelRiesgo.Medio,
            _ => NivelRiesgo.Bajo
        };

        var motivo = inasistencias switch
        {
            0 => "Sin inasistencias registradas.",
            1 => "1 inasistencia acumulada.",
            _ => $"{inasistencias} inasistencias acumuladas."
        };

        return new ResultadoRiesgo(nivel, inasistencias, motivo);
    }

    public static ResultadoRiesgo Calcular(
        Estudiante estudiante,
        IEnumerable<Asistencia> asistencias,
        int totalClasesConfiguradas,
        IEnumerable<TrabajoPractico> trabajosObligatorios,
        IEnumerable<EntregaTrabajo> entregas,
        ConfiguracionRiesgoService? configuracion = null)
    {
        ArgumentNullException.ThrowIfNull(estudiante);
        ArgumentNullException.ThrowIfNull(asistencias);
        ArgumentNullException.ThrowIfNull(trabajosObligatorios);
        ArgumentNullException.ThrowIfNull(entregas);
        configuracion ??= new ConfiguracionRiesgoService();

        var asistenciasEstudiante = asistencias
            .Where(asistencia => asistencia.LegajoEstudiante == estudiante.Legajo)
            .ToList();
        var inasistencias = asistenciasEstudiante.Count(asistencia =>
            EstadoAsistencia.Normalizar(asistencia) == EstadoAsistencia.Ausente);
        var clasesAsistidas = asistenciasEstudiante.Count(asistencia =>
            EstadoAsistencia.Normalizar(asistencia) is EstadoAsistencia.Presente or EstadoAsistencia.Justificado);
        var porcentajeAsistencia = totalClasesConfiguradas > 0
            ? Math.Round(clasesAsistidas * 100m / totalClasesConfiguradas, 2)
            : 0m;

        var trabajos = trabajosObligatorios.Where(trabajo => trabajo.Obligatorio && trabajo.Activo).ToList();
        var entregasEstudiante = entregas
            .Where(entrega => entrega.LegajoEstudiante == estudiante.Legajo && entrega.Entregado)
            .Select(entrega => entrega.TrabajoPracticoId)
            .ToHashSet();
        var trabajosEntregados = trabajos.Count(trabajo => entregasEstudiante.Contains(trabajo.Id));
        var porcentajeEntregas = trabajos.Count > 0
            ? Math.Round(trabajosEntregados * 100m / trabajos.Count, 2)
            : 100m;

        var asistenciaCritica = totalClasesConfiguradas > 0 &&
            porcentajeAsistencia < configuracion.AsistenciaCritica;
        var asistenciaInsuficiente = totalClasesConfiguradas > 0 &&
            porcentajeAsistencia < configuracion.AsistenciaRegular;
        var entregasInsuficientes = trabajos.Count > 0 &&
            porcentajeEntregas < configuracion.EntregasMinimas;
        var nivel = asistenciaInsuficiente || entregasInsuficientes
            ? (asistenciaCritica || inasistencias >= UmbralRiesgoAlto ||
                (asistenciaInsuficiente && entregasInsuficientes)
                ? NivelRiesgo.Alto
                : NivelRiesgo.Medio)
            : NivelRiesgo.Bajo;

        var motivos = new List<string>();
        if (asistenciaInsuficiente)
        {
            motivos.Add($"asistencia {porcentajeAsistencia:0.##}% (mínimo {configuracion.AsistenciaRegular:0}%)");
            if (asistenciaCritica)
            {
                motivos[^1] += $", por debajo del límite crítico de {configuracion.AsistenciaCritica:0}%";
            }
        }

        if (entregasInsuficientes)
        {
            motivos.Add($"entregas {porcentajeEntregas:0.##}% (mínimo {configuracion.EntregasMinimas:0}%)");
        }

        var motivo = motivos.Count == 0
            ? "Asistencia y entregas dentro de los umbrales establecidos."
            : $"Riesgo por: {string.Join(" y ", motivos)}.";

        return new ResultadoRiesgo(
            nivel,
            inasistencias,
            motivo,
            clasesAsistidas,
            totalClasesConfiguradas,
            porcentajeAsistencia,
            trabajosEntregados,
            trabajos.Count,
            porcentajeEntregas);
    }
}
