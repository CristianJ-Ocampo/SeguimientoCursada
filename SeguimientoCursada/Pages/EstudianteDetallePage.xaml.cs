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

}
