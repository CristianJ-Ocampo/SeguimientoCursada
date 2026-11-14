using SeguimientoCursada.Models;

namespace SeguimientoCursada.Services;

public enum NivelRiesgo
{
    Bajo,
    Medio,
    Alto,
    SinDatos
}

public enum EstadoSeguimiento
{
    Optimo,
    Regular,
    EnRiesgo,
    Critico,
    SinDatos
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
    decimal PorcentajeEntregas = 0,
    bool SinRegistrosAsistencia = false);

public static class ResultadoRiesgoExtensions
{
    public static EstadoSeguimiento ObtenerEstadoSeguimiento(this ResultadoRiesgo resultado)
    {
        return resultado.Nivel switch
        {
            NivelRiesgo.Bajo => EstadoSeguimiento.Optimo,
            NivelRiesgo.Medio => EstadoSeguimiento.EnRiesgo,
            NivelRiesgo.Alto => EstadoSeguimiento.Critico,
            NivelRiesgo.SinDatos => EstadoSeguimiento.SinDatos,
            _ => EstadoSeguimiento.Optimo
        };
    }

    public static bool RequiereContacto(this ResultadoRiesgo resultado) =>
        resultado.ObtenerEstadoSeguimiento() is EstadoSeguimiento.EnRiesgo or EstadoSeguimiento.Critico;

    public static string ObtenerEtiqueta(this NivelRiesgo nivel) => nivel switch
    {
        NivelRiesgo.SinDatos => "Sin datos",
        _ => nivel.ToString()
    };

    public static string ObtenerEtiqueta(this EstadoSeguimiento estado) => estado switch
    {
        EstadoSeguimiento.SinDatos => "Sin datos",
        _ => estado.ToString()
    };
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
        IEnumerable<FechaClase> fechasClase,
        IEnumerable<TrabajoPractico> trabajosObligatorios,
        IEnumerable<EntregaTrabajo> entregas,
        ConfiguracionRiesgoService? configuracion = null)
    {
        ArgumentNullException.ThrowIfNull(estudiante);
        ArgumentNullException.ThrowIfNull(asistencias);
        ArgumentNullException.ThrowIfNull(fechasClase);
        ArgumentNullException.ThrowIfNull(trabajosObligatorios);
        ArgumentNullException.ThrowIfNull(entregas);
        configuracion ??= new ConfiguracionRiesgoService();

        var fechaIngreso = estudiante.FechaIngreso?.Date;

        // Solo cuentan las fechas de cursada que aplican a la comisión del estudiante
        // y que son iguales o posteriores a su fecha de ingreso.
        var fechasComputables = fechasClase
            .Where(fechaClase => fechaClase.EsDiaClase &&
                                 (fechaIngreso is null || fechaClase.Fecha.Date >= fechaIngreso) &&
                                 (string.IsNullOrWhiteSpace(fechaClase.Comision) ||
                                  string.Equals(
                                      fechaClase.Comision,
                                      estudiante.Comision,
                                      StringComparison.Ordinal)))
            .Select(fechaClase => fechaClase.Fecha.Date)
            .ToHashSet();

        var totalClasesConfiguradas = fechasComputables.Count;

        var asistenciasEstudiante = asistencias
            .Where(asistencia => asistencia.LegajoEstudiante == estudiante.Legajo)
            .ToList();

        // El numerador también se limita a las fechas computables para evitar
        // porcentajes > 100% y para excluir registros anteriores al ingreso.
        var asistenciasComputables = asistenciasEstudiante
            .Where(asistencia => fechasComputables.Contains(asistencia.Fecha.Date))
            .ToList();

        var sinRegistrosAsistencia = asistenciasComputables.Count == 0;

        var inasistencias = asistenciasComputables.Count(asistencia =>
            EstadoAsistencia.Normalizar(asistencia) == EstadoAsistencia.Ausente);
        var clasesAsistidas = asistenciasComputables.Count(asistencia =>
            EstadoAsistencia.Normalizar(asistencia) is EstadoAsistencia.Presente or EstadoAsistencia.Justificado);
        var porcentajeAsistencia = totalClasesConfiguradas > 0
            ? Math.Round(clasesAsistidas * 100m / totalClasesConfiguradas, 2)
            : 0m;

        var trabajos = trabajosObligatorios
            .Where(trabajo => trabajo.Obligatorio &&
                              trabajo.Activo &&
                              (string.IsNullOrWhiteSpace(trabajo.Comision) ||
                               string.Equals(
                                   trabajo.Comision,
                                   estudiante.Comision,
                                   StringComparison.Ordinal)))
            .ToList();
        var entregasEstudiante = entregas
            .Where(entrega => entrega.LegajoEstudiante == estudiante.Legajo && entrega.Entregado)
            .Select(entrega => entrega.TrabajoPracticoId)
            .ToHashSet();
        var trabajosEntregados = trabajos.Count(trabajo => entregasEstudiante.Contains(trabajo.Id));
        var porcentajeEntregas = trabajos.Count > 0
            ? Math.Round(trabajosEntregados * 100m / trabajos.Count, 2)
            : 100m;

        var sinDatos = sinRegistrosAsistencia && trabajos.Count == 0;
        var asistenciaCritica = !sinRegistrosAsistencia &&
            totalClasesConfiguradas > 0 &&
            porcentajeAsistencia < configuracion.AsistenciaCritica;
        var asistenciaInsuficiente = !sinRegistrosAsistencia &&
            totalClasesConfiguradas > 0 &&
            porcentajeAsistencia < configuracion.AsistenciaRegular;
        var entregasInsuficientes = trabajos.Count > 0 &&
            porcentajeEntregas < configuracion.EntregasMinimas;
        var nivel = sinDatos
            ? NivelRiesgo.SinDatos
            : asistenciaInsuficiente || entregasInsuficientes
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

        var motivoSinAsistencia = fechaIngreso is null
            ? "Sin ningún registro de asistencia."
            : $"Sin registros de asistencia desde el ingreso ({fechaIngreso:dd/MM/yyyy}).";

        var motivo = sinDatos
            ? motivoSinAsistencia
            : motivos.Count == 0
                ? sinRegistrosAsistencia
                    ? motivoSinAsistencia
                    : "Asistencia y entregas dentro de los umbrales establecidos."
                : $"Riesgo por: {string.Join(" y ", motivos)}." +
                  (sinRegistrosAsistencia ? " " + motivoSinAsistencia : string.Empty);

        return new ResultadoRiesgo(
            nivel,
            inasistencias,
            motivo,
            clasesAsistidas,
            totalClasesConfiguradas,
            porcentajeAsistencia,
            trabajosEntregados,
            trabajos.Count,
            porcentajeEntregas,
            sinRegistrosAsistencia);
    }
}
