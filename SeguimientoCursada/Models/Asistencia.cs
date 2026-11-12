using SQLite;

namespace SeguimientoCursada.Models;

public class Asistencia
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int LegajoEstudiante { get; set; }
    public DateTime Fecha { get; set; }
    public bool Presente { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Comision { get; set; } = string.Empty;
}

public static class EstadoAsistencia
{
    public const string Presente = "Presente";
    public const string Ausente = "Ausente";
    public const string Justificado = "Justificado";

    public static readonly IReadOnlyList<string> Valores =
        new[] { Presente, Ausente, Justificado };

    public static string Normalizar(Asistencia asistencia)
    {
        if (Valores.Contains(asistencia.Estado))
        {
            return asistencia.Estado;
        }

        return asistencia.Presente ? Presente : Ausente;
    }

    public static void Aplicar(Asistencia asistencia, string estado)
    {
        if (!Valores.Contains(estado))
        {
            throw new ArgumentException($"Estado de asistencia no válido: {estado}", nameof(estado));
        }

        asistencia.Estado = estado;
        asistencia.Presente = estado == Presente;
    }
}
