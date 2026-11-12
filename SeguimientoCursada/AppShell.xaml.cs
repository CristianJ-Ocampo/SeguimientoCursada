using SeguimientoCursada.Pages;

namespace SeguimientoCursada
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(EstudianteDetallePage), typeof(EstudianteDetallePage));
            Routing.RegisterRoute(nameof(CalendarioClasesPage), typeof(CalendarioClasesPage));
            Routing.RegisterRoute(nameof(TrabajosPracticosPage), typeof(TrabajosPracticosPage));
            Routing.RegisterRoute(nameof(ConfiguracionPage), typeof(ConfiguracionPage));
        }
    }
}
