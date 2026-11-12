using SQLite;

namespace SeguimientoCursada.Models;

public class EntregaTrabajo
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int LegajoEstudiante { get; set; }

    public int TrabajoPracticoId { get; set; }

    public bool Entregado { get; set; }

    public DateTime? FechaEntrega { get; set; }
}
