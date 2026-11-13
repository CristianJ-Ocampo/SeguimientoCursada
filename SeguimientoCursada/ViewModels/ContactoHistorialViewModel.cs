using SeguimientoCursada.Models;

namespace SeguimientoCursada.ViewModels;

public sealed class ContactoHistorialViewModel
{
    public ContactoHistorialViewModel(ContactoEstudiante contacto)
    {
        FechaHora = contacto.FechaHora;
        Medio = contacto.Medio;
        Motivo = contacto.Motivo;
        Resultado = contacto.Resultado;
    }

    public DateTime FechaHora { get; }
    public string Medio { get; }
    public string Motivo { get; }
    public string Resultado { get; }
}
