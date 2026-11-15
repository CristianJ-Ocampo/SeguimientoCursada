using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada.Pages;

public partial class AltaEstudiantePage : ContentPage
{
    private readonly AltaEstudianteViewModel _viewModel;

    public AltaEstudiantePage()
    {
        InitializeComponent();
        _viewModel = new AltaEstudianteViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RecargarAsync();
    }
}
