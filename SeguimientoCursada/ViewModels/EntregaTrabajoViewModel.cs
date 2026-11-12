using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeguimientoCursada.Models;

namespace SeguimientoCursada.ViewModels;

public sealed class EntregaTrabajoViewModel : INotifyPropertyChanged
{
    private bool _entregado;

    public EntregaTrabajoViewModel(TrabajoPractico trabajo, EntregaTrabajo? entrega)
    {
        Trabajo = trabajo;
        Entrega = entrega;
        _entregado = entrega?.Entregado ?? false;
    }

    public TrabajoPractico Trabajo { get; }

    public EntregaTrabajo? Entrega { get; private set; }

    public bool Entregado
    {
        get => _entregado;
        set
        {
            if (_entregado == value)
            {
                return;
            }

            _entregado = value;
            OnPropertyChanged();
        }
    }

    public string Titulo => Trabajo.Titulo;

    public string Descripcion => Trabajo.Descripcion;

    public bool TieneDescripcion => !string.IsNullOrWhiteSpace(Descripcion);

    public void ActualizarEntrega(EntregaTrabajo? entrega)
    {
        Entrega = entrega;
        _entregado = entrega?.Entregado ?? false;
        OnPropertyChanged(nameof(Entrega));
        OnPropertyChanged(nameof(Entregado));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
