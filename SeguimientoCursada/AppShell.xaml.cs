using SeguimientoCursada.Pages;

namespace SeguimientoCursada
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(EstudianteDetallePage), typeof(EstudianteDetallePage));
        }
    }
}
