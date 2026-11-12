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

    [Ignore]
    public bool EsOpcional => !Obligatorio;
}
