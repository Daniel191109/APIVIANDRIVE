using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ViandriveApp.Helpers;
using ViandriveApp.Models;

namespace ViandriveApp.Views
{
    public partial class SolicitudViajeFormPage : ContentPage
    {
        private bool bloqueandoFormato = false;
        private readonly string origen;
        private readonly string destino;
        private readonly string duracion;
        private readonly decimal oferta;

        public SolicitudViajeFormPage(string origen, string destino, string duracion, decimal oferta)
        {
            InitializeComponent();
            this.origen = origen;
            this.destino = destino;
            this.duracion = duracion;
            this.oferta = oferta;

            ClienteLabel.Text = $"Hola, {Preferences.Get("Nombre", "Usuario")}";
            OrigenLabel.Text = origen;
            DestinoLabel.Text = destino;
            DuracionLabel.Text = $"Duración estimada: {duracion}";
            OfertaLabel.Text = $"Oferta: ${oferta:N0}";
        }


        private void OfertaEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (bloqueandoFormato) return;

            var texto = e.NewTextValue?.Replace(".", "").Replace(",", "").Trim();

            if (int.TryParse(texto, out int valor) && valor > 0)
            {
                bloqueandoFormato = true;
                OfertaEntry.Text = valor.ToString("N0", CultureInfo.InvariantCulture);
                OfertaEntry.CursorPosition = OfertaEntry.Text.Length;
                bloqueandoFormato = false;
            }
        }

        private async void SolicitarViaje_Clicked(object sender, EventArgs e)
        {
            int clienteId = Preferences.Get("ClienteId", 0);
            if (clienteId == 0)
            {
                await DisplayAlert("Error", "No se pudo obtener el ID del cliente. Inicia sesión nuevamente.", "OK");
                return;
            }
            string valorOferta = OfertaEntry.Text;
            string medioPago = MedioPagoPicker.SelectedItem?.ToString();
            bool ofertaValida = decimal.TryParse(OfertaEntry.Text?.Replace(".", "").Replace(",", ""), out decimal oferta);

            if (string.IsNullOrEmpty(origen) || string.IsNullOrEmpty(destino) || string.IsNullOrEmpty(medioPago) || !ofertaValida)
            {
                await DisplayAlert("Error", "Completa todos los campos correctamente.", "OK");
                return;
            }

            var solicitud = new SolicitudServicioDto
            {
                ClienteId = clienteId,
                Origen = origen,
                Destino = destino,
                DistanciaKm = 3.5, // Puedes calcularlo si integras Google Directions
                TiempoEstimadoMin = 12,
                HoraPico = false,
                OfertaPasajero = oferta,
                MedioPago = medioPago
            };

            bool exito = await EnviarSolicitudAlBackend(solicitud);

            if (exito)
            {
                await DisplayAlert("Solicitud enviada", $"Destino: {destino}\nOferta: ${oferta:N0}", "OK");
            }
            else
            {
                await DisplayAlert("Error", "No se pudo registrar la solicitud. Intenta nuevamente.", "OK");
            }
        }

        private async Task<bool> EnviarSolicitudAlBackend(SolicitudServicioDto solicitud)
        {
            try
            {
                using var httpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var json = JsonSerializer.Serialize(solicitud);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var baseUrl = ApiConfig.BaseUrl.TrimEnd('/');
                var url = $"{baseUrl}/api/solicitudes";

                var response = await httpClient.PostAsync(url, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al enviar solicitud: " + ex.Message);
                return false;
            }
        }
    }
}