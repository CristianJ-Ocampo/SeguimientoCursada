using Microsoft.Maui.Controls;

namespace SeguimientoCursada.Controls;

/// <summary>
/// Diálogo modal para indicar la fecha de la primera clase cuando todavía no
/// hay fechas cargadas. Permite fechas futuras, porque suele usarse al preparar
/// la cursada antes de que comience.
/// </summary>
public sealed class DialogoFecha : ContentPage
{
    private readonly TaskCompletionSource<DateTime?> _resultado = new();
    private readonly DatePicker _selector;
    private bool _confirmado;

    private DialogoFecha(string comision, DateTime inicial)
    {
        Title = "Fecha de la primera clase";

        _selector = new DatePicker
        {
            Date = inicial,
            Format = "dd/MM/yyyy"
        };

        var confirmar = new Button { Text = "Confirmar" };
        var cancelar = new Button { Text = "Cancelar" };

        confirmar.Clicked += ConfirmarClicked;
        cancelar.Clicked += CancelarClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24),
                Spacing = 16,
                Children =
                {
                    new Label
                    {
                        Text = $"No hay fechas de clase cargadas para {comision}. " +
                               "Indicá la fecha de la primera clase: los alumnos quedarán " +
                               "como ingresados desde el inicio."
                    },
                    new Label { Text = "Fecha de la primera clase" },
                    _selector,
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children = { confirmar, cancelar }
                    }
                }
            }
        };
    }

    public static async Task<DateTime?> MostrarAsync(string comision)
    {
        var pagina = new DialogoFecha(comision, DateTime.Today);
        var navegacion = Shell.Current?.Navigation
            ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

        if (navegacion is null)
        {
            return null;
        }

        await navegacion.PushModalAsync(pagina);
        return await pagina._resultado.Task;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (!_confirmado)
        {
            _resultado.TrySetResult(null);
        }
    }

    private async void ConfirmarClicked(object? sender, EventArgs e)
    {
        _confirmado = true;
        var fecha = (_selector.Date ?? DateTime.Today).Date;
        await Navigation.PopModalAsync();
        _resultado.TrySetResult(fecha);
    }

    private async void CancelarClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _resultado.TrySetResult(null);
    }
}
