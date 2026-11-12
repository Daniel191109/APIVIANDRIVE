using ViandriveApp.Views;

namespace ViandriveApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Rutas registradas para navegación en la aplicación
            Routing.RegisterRoute("Usuario", typeof(Usuario));
            Routing.RegisterRoute("MapaPage", typeof(MapaPage));
            Routing.RegisterRoute("LoginPage", typeof(LoginPage));
            Routing.RegisterRoute("WelcomePage", typeof(WelcomePage)); // ✅ Ruta de bienvenida registrada

            // Registro de la ruta de recuperación de contraseña
            Routing.RegisterRoute(nameof(ForgotPasswordPage), typeof(ForgotPasswordPage));

            Routing.RegisterRoute("MainPage", typeof(MainPage));

            // ✅ Registro de la ruta para el Mapa de Oferta
            Routing.RegisterRoute(nameof(MapaOfertaPage), typeof(MapaOfertaPage));
        }

        public AppShell(bool esConductor) : this()
        {
            if (esConductor)
            {
                var conductorItem = new FlyoutItem
                {
                    Title = "Panel del Conductor",
                    Icon = "car_icon.png"
                };

                conductorItem.Items.Add(
                    new ShellContent
                    {
                        Title = "Panel",
                        ContentTemplate = new DataTemplate(typeof(Home)),
                        Route = "WelcomePage"
                    });

                this.Items.Add(conductorItem);
            }
        }
    }
}