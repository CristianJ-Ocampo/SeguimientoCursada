using SeguimientoCursada.Models;

namespace SeguimientoCursada.Services;

public enum NivelRiesgo
{
    Bajo,
    Medio,
    Alto
}

public sealed record ResultadoRiesgo(NivelRiesgo Nivel, int Inasistencias, string Motivo);

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
}
