namespace SeguimientoCursada.ViewModels;

public sealed class ResumenComisionViewModel
{
    public ResumenComisionViewModel(
        string comision,
        int totalAlumnos,
        int estudiantesOptimos,
        int estudiantesEnRiesgo,
        int estudiantesCriticos,
        int estudiantesSinDatos,
        decimal promedioAsistencia,
        decimal promedioEntregas)
    {
        Comision = comision;
        TotalAlumnos = totalAlumnos;
        EstudiantesOptimos = estudiantesOptimos;
        EstudiantesEnRiesgo = estudiantesEnRiesgo;
        EstudiantesCriticos = estudiantesCriticos;
        EstudiantesSinDatos = estudiantesSinDatos;
        PromedioAsistencia = promedioAsistencia;
        PromedioEntregas = promedioEntregas;
    }

    public string Comision { get; }

    public int TotalAlumnos { get; }

    public int EstudiantesOptimos { get; }

    public int EstudiantesEnRiesgo { get; }

    public int EstudiantesCriticos { get; }

    public int EstudiantesSinDatos { get; }

    public int TotalConDatos => EstudiantesOptimos + EstudiantesEnRiesgo + EstudiantesCriticos;

    public bool TieneDatos => TotalConDatos > 0;

    public decimal PromedioAsistencia { get; }

    public decimal PromedioEntregas { get; }
}
