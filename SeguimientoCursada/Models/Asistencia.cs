namespace SeguimientoCursada.Models;

public class Asistencia
{
    public int Id { get; set; }
    public int LegajoEstudiante { get; set; }
    public DateTime Fecha { get; set; }
    public bool Presente { get; set; }
    public string Comision { get; set; } = string.Empty;
}
