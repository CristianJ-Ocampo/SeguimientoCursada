using SQLite;

namespace SeguimientoCursada.Models
{
    public class Estudiante
    {
        [PrimaryKey]
        public int Legajo { get; set; }
        public string Apellido { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Comision { get; set; } = string.Empty;
    }
}
