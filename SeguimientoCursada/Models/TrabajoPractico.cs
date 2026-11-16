using SQLite;

namespace SeguimientoCursada.Models;

public class TrabajoPractico
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public bool Obligatorio { get; set; } = true;

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Fecha límite de entrega. Null solo existe por compatibilidad histórica y se
    /// interpreta como un trabajo sin vencimiento.
    /// </summary>
    public DateTime? FechaVencimiento { get; set; }

    /// <summary>
    /// Comisión a la que aplica el trabajo práctico. El valor vacío solo existe por
    /// compatibilidad histórica y se interpreta como aplicable a todas las comisiones.
    /// </summary>
    public string Comision { get; set; } = string.Empty;

    [Ignore]
    public string ComisionMostrada => string.IsNullOrWhiteSpace(Comision)
        ? "Sin comisión"
        : Comision;

    [Ignore]
    public string VencimientoMostrado => FechaVencimiento is { } fecha
        ? $"Vence: {fecha:dd/MM/yyyy}"
        : "Sin vencimiento";
}
