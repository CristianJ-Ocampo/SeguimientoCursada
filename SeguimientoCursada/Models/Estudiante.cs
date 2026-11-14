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

        /// <summary>
        /// Fecha en la que el alumno se incorporó a la comisión. Null indica que
        /// cursó desde la primera clase.
        /// </summary>
        public DateTime? FechaIngreso { get; set; }

        [Ignore]
        public bool CursaDesdeInicio => FechaIngreso is null;
    }
}
