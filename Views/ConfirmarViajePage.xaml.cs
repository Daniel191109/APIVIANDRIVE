using Microsoft.Maui.Controls;
using ViaDriveApp.Views;
using ViandriveApp.Views;

namespace ViandriveApp.Views
{
    public partial class ConfirmarViajePage : ContentPage
    {
        private readonly string origen;
        private readonly string destino;
        private readonly string duracion;
        private readonly decimal oferta;
        private readonly string medioPago;
        private readonly int solicitudId;

        public ConfirmarViajePage(string origen, string destino, string duracion, decimal oferta, string medioPago, int solicitudId)
        {
            InitializeComponent();

            this.origen = origen;
            this.destino = destino;
            this.duracion = duracion;
            this.oferta = oferta;
            this.medioPago = medioPago;
            this.solicitudId = solicitudId;

            ResumenLabel.Text = $"📍 Origen: {origen}\n🏁 Destino: {destino}\n🕒 Duración: {duracion}\n💰 Oferta: ${oferta:N0}\n💳 Medio de pago: {medioPago}";
        }

        private async void ConfirmarViaje_Clicked(object sender, EventArgs e)
        {
            //await DisplayAlert("Solicitud enviada", $"Destino: {destino}\nOferta: ${oferta:N0}", "OK");

            // Aquí volvemos a enviar al usuario a tu vista de revisión original con el ID
            await Navigation.PushAsync(new OfertaEnRevisionPage(origen, destino, solicitudId));
        }

        private async void Cancelar_Clicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}