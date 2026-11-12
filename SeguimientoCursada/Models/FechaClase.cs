using SQLite;

namespace SeguimientoCursada.Models;

public class FechaClase
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime Fecha { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public bool EsDiaClase { get; set; }

    [Ignore]
    public bool TieneDescripcion => !string.IsNullOrWhiteSpace(Descripcion);

    [Ignore]
    public bool EsDiaNoClase => !EsDiaClase;
}
