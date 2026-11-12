using SeguimientoCursada.Services;
using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class EstudianteDetallePage : ContentPage
{
    private readonly EstudianteDetalleViewModel _viewModel;

    public EstudianteDetallePage(DatabaseService databaseService)
    {
        InitializeComponent();
        _viewModel = new EstudianteDetalleViewModel(databaseService);
        BindingContext = _viewModel;
    }

    private async void OnEntregaChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox { BindingContext: EntregaTrabajoViewModel entrega })
        {
            _viewModel.ActualizarEntregaCommand.Execute(entrega);
        }
    }

}
