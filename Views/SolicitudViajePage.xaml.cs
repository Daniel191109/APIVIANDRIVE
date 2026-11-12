using Microsoft.Maui.Controls;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using ViandriveApp.Models;
using ViandriveApp.Helpers; // <-- Necesario para ApiConfig

namespace ViandriveApp.Views
{
    public partial class SolicitudViajePage : ContentPage
    {
        public SolicitudViajePage()
        {
            InitializeComponent();
            CargarAutocompletado();
        }

        private void CargarAutocompletado()
        {
            var html = @"
<!DOCTYPE html>
<html>
<head>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <style>
    html, body {
      height: 100%;
      margin: 0;
      padding: 0;
      font-family: sans-serif;
      background-color: #f5f5f5;
    }
    #autocomplete {
      width: 90%;
      margin: 20px auto;
      padding: 12px;
      font-size: 16px;
      border: 1px solid #ccc;
      border-radius: 6px;
      display: block;
    }
  </style>
  <script src='https://www.google.com/maps/api/js?key=AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw&libraries=places'></script>
  <script>
    function initAutocomplete() {
      const input = document.getElementById('autocomplete');
      const autocomplete = new google.maps.places.Autocomplete(input);
      autocomplete.addListener('place_changed', () => {
        const place = autocomplete.getPlace();
        window.location.href = 'place://' + encodeURIComponent(place.formatted_address);
      });
    }
    window.onload = initAutocomplete;
  </script>
</head>
<body>
  <input id='autocomplete' placeholder='Ingresa tu destino' type='text' />
</body>
</html>";

            var webView = new WebView
            {
                Source = new HtmlWebViewSource { Html = html },
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand
            };

            webView.Navigating += WebView_Navigating;
            MapaContenedor.Content = webView;
        }

        private void WebView_Navigating(object sender, WebNavigatingEventArgs e)
        {
            if (e.Url.StartsWith("place://"))
            {
                e.Cancel = true;
                string destino = Uri.UnescapeDataString(e.Url.Replace("place://", ""));
                DestinoEntry.Text = destino;
                TarifaLabel.Text = "Tarifa estimada: $12.000"; // Simulado
            }
        }

        private async void EnviarSolicitud_Clicked(object sender, EventArgs e)
        {
            string destino = DestinoEntry.Text?.Trim();
            int clienteId = Preferences.Get("ClienteId", 0);

            if (clienteId == 0 || string.IsNullOrEmpty(destino))
            {
                await DisplayAlert("Error", "No se pudo obtener el ID del cliente o el destino. Inicia sesión nuevamente.", "OK");
                return;
            }

            var solicitud = new SolicitudServicioDto
            {
                ClienteId = clienteId,
                Origen = "Ubicación actual",
                Destino = destino,
                DistanciaKm = 3.5,
                TiempoEstimadoMin = 12,
                HoraPico = false,
                OfertaPasajero = 6500, // Aquí podrías amarrarlo a un Entry si el pasajero digita la oferta
                MedioPago = "Efectivo"
            };

            if (sender is Button btn) btn.IsEnabled = false;

            int solicitudId = await EnviarSolicitudAlBackend(solicitud);

            if (solicitudId > 0)
            {
                await DisplayAlert("Solicitud enviada", $"Destino: {destino}\nTarifa: $6.500", "OK");
                // AQUÍ: Redirigir al pasajero a la pantalla donde espera que el conductor acepte
                // Ejemplo: await Navigation.PushAsync(new EsperandoConductorPage(solicitudId));
            }
            else
            {
                await DisplayAlert("Error", "No se pudo registrar la solicitud. Intenta nuevamente.", "OK");
                if (sender is Button btnRecuperar) btnRecuperar.IsEnabled = true;
            }
        }

        // Modificado para retornar int (el ID) y usar ApiConfig
        private async Task<int> EnviarSolicitudAlBackend(SolicitudServicioDto solicitud)
        {
            try
            {
                var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var json = JsonSerializer.Serialize(solicitud);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"{ApiConfig.BaseUrl}/api/solicitudes";
                var response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseContent);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("solicitud", out var solElement) && solElement.TryGetProperty("id", out var idProp))
                    {
                        return idProp.GetInt32();
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al enviar solicitud: " + ex.Message);
                return 0;
            }
        }
    }
}