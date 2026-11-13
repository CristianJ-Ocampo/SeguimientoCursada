using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeguimientoCursada.Models;

namespace SeguimientoCursada.ViewModels;

public sealed class AsistenciaHistorialViewModel
{
    private string _estadoSeleccionado;

    public AsistenciaHistorialViewModel(Asistencia asistencia)
    {
        Asistencia = asistencia;
        Fecha = asistencia.Fecha;
        _estadoSeleccionado = EstadoAsistencia.Normalizar(asistencia);
    }

    public Asistencia Asistencia { get; }
    public DateTime Fecha { get; }
    public bool Presente => EstadoSeleccionado == EstadoAsistencia.Presente;
    public string Estado => EstadoSeleccionado;

    public string EstadoSeleccionado
    {
        get => _estadoSeleccionado;
        set
        {
            if (_estadoSeleccionado == value) return;
            _estadoSeleccionado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Estado));
            OnPropertyChanged(nameof(Presente));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
