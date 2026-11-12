using Microsoft.Maui.Storage;

namespace SeguimientoCursada.Services;

public sealed class ConfiguracionRiesgoService
{
    public const decimal AsistenciaRegularPredeterminada = 75m;
    public const decimal AsistenciaCriticaPredeterminada = 60m;
    public const decimal EntregasPredeterminadas = 75m;

    private const string AsistenciaRegularKey = "riesgo.asistencia_regular";
    private const string AsistenciaCriticaKey = "riesgo.asistencia_critica";
    private const string EntregasKey = "riesgo.entregas";

    public event EventHandler? ConfiguracionCambió;

    public decimal AsistenciaRegular => LeerPorcentaje(
        AsistenciaRegularKey, AsistenciaRegularPredeterminada);

    public decimal AsistenciaCritica => LeerPorcentaje(
        AsistenciaCriticaKey, AsistenciaCriticaPredeterminada);

    public decimal EntregasMinimas => LeerPorcentaje(
        EntregasKey, EntregasPredeterminadas);

    public void Guardar(decimal asistenciaRegular, decimal asistenciaCritica, decimal entregasMinimas)
    {
        Validar(asistenciaRegular, asistenciaCritica, entregasMinimas);

        Preferences.Default.Set(AsistenciaRegularKey, (double)asistenciaRegular);
        Preferences.Default.Set(AsistenciaCriticaKey, (double)asistenciaCritica);
        Preferences.Default.Set(EntregasKey, (double)entregasMinimas);
        ConfiguracionCambió?.Invoke(this, EventArgs.Empty);
    }

    public void RestaurarPredeterminados()
    {
        Guardar(
            AsistenciaRegularPredeterminada,
            AsistenciaCriticaPredeterminada,
            EntregasPredeterminadas);
    }

    private static void Validar(decimal asistenciaRegular, decimal asistenciaCritica, decimal entregasMinimas)
    {
        if (asistenciaRegular is < 0 or > 100 ||
            asistenciaCritica is < 0 or > 100 ||
            entregasMinimas is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(asistenciaRegular),
                "Los porcentajes deben estar entre 0 y 100.");
        }

        if (asistenciaCritica > asistenciaRegular)
        {
            throw new ArgumentException(
                "El umbral de asistencia crítica no puede superar el umbral regular.");
        }
    }

    private static decimal LeerPorcentaje(string key, decimal valorPredeterminado)
    {
        try
        {
            var valor = Preferences.Default.Get(key, (double)valorPredeterminado);
            return valor is >= 0 and <= 100
                ? (decimal)valor
                : valorPredeterminado;
        }
        catch (Exception)
        {
            return valorPredeterminado;
        }
    }
}
