using SeguimientoCursada.Pages;
using SeguimientoCursada.ViewModels;

namespace SeguimientoCursada
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            BindingContext = new AppShellViewModel();
            Routing.RegisterRoute(nameof(InicioPage), typeof(InicioPage));
            Routing.RegisterRoute(nameof(EstudianteDetallePage), typeof(EstudianteDetallePage));
            Routing.RegisterRoute(nameof(CalendarioClasesPage), typeof(CalendarioClasesPage));
            Routing.RegisterRoute(nameof(TrabajosPracticosPage), typeof(TrabajosPracticosPage));
            Routing.RegisterRoute(nameof(ConfiguracionPage), typeof(ConfiguracionPage));
        }
    }
}
