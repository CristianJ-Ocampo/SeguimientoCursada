using SQLite;

namespace SeguimientoCursada.Models;

public sealed class ContactoEstudiante
{
    public static IReadOnlyList<string> MediosDisponibles { get; } =
        new[] { "Llamada telefónica", "Mensaje / WhatsApp", "Correo electrónico", "Presencial" };

    public static IReadOnlyList<string> MotivosDisponibles { get; } =
        new[]
        {
            "Inasistencias reiteradas",
            "Falta de entrega de TPs",
            "Ambos (Asistencia y TPs)",
            "Otro"
        };

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int LegajoEstudiante { get; set; }

    public DateTime FechaHora { get; set; }

    public string Medio { get; set; } = string.Empty;

    public string Motivo { get; set; } = string.Empty;

    public string Resultado { get; set; } = string.Empty;
}
