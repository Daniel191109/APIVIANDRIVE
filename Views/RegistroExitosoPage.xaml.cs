using Microsoft.Maui.Controls;

namespace ViandriveApp.Views
{
    [QueryProperty(nameof(Nombre), "nombre")]
    [QueryProperty(nameof(Placa), "placa")]
    [QueryProperty(nameof(Color), "color")]
    public partial class RegistroExitosoPage : ContentPage
    {
        public string Nombre
        {
            set => NombreLabel.Text = $"Conductor: {value}";
        }

        public string Placa
        {
            set => PlacaLabel.Text = $"Placa: {value}";
        }

        public string Color
        {
            set => ColorLabel.Text = $"Color: {value}";
        }

        public RegistroExitosoPage()
        {
            InitializeComponent();
        }

        private async void OnIrInicioClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}
