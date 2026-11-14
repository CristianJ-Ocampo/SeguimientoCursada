using SQLite;

namespace SeguimientoCursada.Models;

public class FechaClase
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime Fecha { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public bool EsDiaClase { get; set; }

    /// <summary>
    /// Comisión a la que aplica la fecha. El valor vacío solo existe por compatibilidad
    /// histórica y se interpreta como aplicable a todas las comisiones.
    /// </summary>
    public string Comision { get; set; } = string.Empty;

    [Ignore]
    public bool TieneDescripcion => !string.IsNullOrWhiteSpace(Descripcion);

    [Ignore]
    public string ComisionMostrada => string.IsNullOrWhiteSpace(Comision)
        ? "Sin comisión"
        : Comision;

    [Ignore]
    public bool EsDiaNoClase => !EsDiaClase;
}
