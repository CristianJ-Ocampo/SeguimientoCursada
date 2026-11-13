using System.Globalization;
using Microsoft.Maui.Graphics;
using SeguimientoCursada.Models;
using SeguimientoCursada.Services;

namespace SeguimientoCursada.ViewModels;

public sealed class EstudianteListadoViewModel
{
    public EstudianteListadoViewModel(
        Estudiante estudiante,
        ContactoEstudiante? ultimoContacto,
        ResultadoRiesgo resultado)
    {
        Estudiante = estudiante;
        Resultado = resultado;
        UltimoContacto = resultado.RequiereContacto() ? ultimoContacto : null;
    }

    public Estudiante Estudiante { get; }

    public int Legajo => Estudiante.Legajo;

    public string Apellido => Estudiante.Apellido;

    public string Nombre => Estudiante.Nombre;

    public string Comision => Estudiante.Comision;

    public ContactoEstudiante? UltimoContacto { get; }

    public ResultadoRiesgo Resultado { get; }

    public string EstadoSeguimiento => Resultado.ObtenerEstadoSeguimiento().ToString();

    public bool PuedeMostrarSeguimiento => Resultado.RequiereContacto();

    public bool TieneContacto => UltimoContacto is not null;

    public string TextoSeguimiento => !PuedeMostrarSeguimiento
        ? string.Empty
        : UltimoContacto is null
            ? "Pendiente de contacto"
            : $"Contactado: {UltimoContacto.FechaHora.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)}";

    public Color ColorSeguimiento => TieneContacto ? Colors.DarkGreen : Colors.DarkRed;
}
