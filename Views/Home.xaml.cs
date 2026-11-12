using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using ViandriveApp.Helpers;

namespace ViandriveApp.Views
{
    public partial class Home : ContentPage
    {
        private bool conductorDisponible = false;
        private readonly HttpClient _httpClient;

        public Home()
        {
            InitializeComponent();

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // REGLA: Si no hay sesión activa, redirigir inmediatamente al Login y bloquear datos
            bool estaLogueado = Preferences.Get("ConductorSesion", false);
            if (!estaLogueado)
            {
                RedirigirAlLogin();
                return;
            }

            CargarDatosLocales();
            await CargarDatosConductorDesdeApiAsync();
        }

        private void CargarDatosLocales()
        {
            string nombre = Preferences.Get("NombreConductor", "Conductor");
            int conductorId = Preferences.Get("ConductorId", 0);

            lblBienvenida.Text = $"Bienvenido, {nombre}";
            lblNombre.Text = nombre;
            lblClienteId.Text = conductorId > 0 ? $"ID: {conductorId}" : "ID: --";
        }

        private async Task CargarDatosConductorDesdeApiAsync()
        {
            try
            {
                int conductorId = Preferences.Get("ConductorId", 0);
                if (conductorId <= 0) return;

                string baseUrl = GetBaseUrl();
                string url = $"{baseUrl}/api/ConductorApi/{conductorId}";

                var respuesta = await _httpClient.GetAsync(url);

                if (respuesta.IsSuccessStatusCode)
                {
                    var contenidoJson = await respuesta.Content.ReadAsStringAsync();

                    using var doc = JsonDocument.Parse(contenidoJson);
                    var root = doc.RootElement;

                    string nombreReal = root.TryGetProperty("nombre", out var n) ? n.GetString() : "Conductor";
                    decimal calificacion = root.TryGetProperty("calificacion", out var c) ? c.GetDecimal() : 5.0m;
                    int viajesCompletados = root.TryGetProperty("viajesCompletados", out var v) ? v.GetInt32() : 0;
                    decimal gananciasHoy = root.TryGetProperty("gananciasHoy", out var g) ? g.GetDecimal() : 0.0m;
                    decimal saldo = root.TryGetProperty("saldo", out var sal) ? sal.GetDecimal() : 0.0m;
                    string selfieBase64 = root.TryGetProperty("selfieBase64", out var s) ? s.GetString() : null;
                    bool disponibleApi = root.TryGetProperty("disponible", out var disp) && disp.ValueKind == JsonValueKind.True;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        conductorDisponible = disponibleApi;
                        DisponibilidadSwitch.IsToggled = disponibleApi;
                        lblNombre.Text = nombreReal;
                        lblBienvenida.Text = $"Bienvenido, {nombreReal}";
                        lblCalificacion.Text = $"{calificacion:0.0} ★";
                        lblViajes.Text = viajesCompletados.ToString();
                        lblGanancias.Text = $"${gananciasHoy:N0}";
                        lblSaldo.Text = $"${saldo:N0}";

                        if (!string.IsNullOrWhiteSpace(selfieBase64))
                        {
                            try
                            {
                                if (selfieBase64.Contains(","))
                                {
                                    selfieBase64 = selfieBase64.Substring(selfieBase64.IndexOf(",") + 1);
                                }

                                byte[] imageBytes = Convert.FromBase64String(selfieBase64);
                                SelfieImage.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                            }
                            catch (Exception imgEx)
                            {
                                Console.WriteLine($"Error procesando imagen: {imgEx.Message}");
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sincronizando datos: {ex.Message}");
            }
        }

        private async void DisponibilidadSwitch_Toggled(object sender, ToggledEventArgs e)
        {
            conductorDisponible = e.Value;
            Preferences.Set("ConductorDisponible", conductorDisponible);

            if (conductorDisponible)
            {
                lblEstado.Text = "DISPONIBLE";
                lblEstado.TextColor = Color.FromArgb("#00E676");
                lblEstadoDescripcion.Text = "Recibiendo solicitudes de viaje";
                lblEstadoDescripcion.TextColor = Color.FromArgb("#00E676");
            }
            else
            {
                lblEstado.Text = "NO DISPONIBLE";
                lblEstado.TextColor = Color.FromArgb("#FFFFFF");
                lblEstadoDescripcion.Text = "No recibirás solicitudes de viaje";
                lblEstadoDescripcion.TextColor = Color.FromArgb("#A0A2B1");
            }

            await ActualizarDisponibilidadEnApiAsync(conductorDisponible);
        }

        private async Task ActualizarDisponibilidadEnApiAsync(bool disponible)
        {
            try
            {
                int conductorId = Preferences.Get("ConductorId", 0);
                if (conductorId <= 0) return;

                string baseUrl = GetBaseUrl();
                string url = $"{baseUrl}/api/ConductorApi/actualizar-estado";

                var payload = new { ConductorId = conductorId, Disponible = disponible };
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar disponibilidad: {ex.Message}");
            }
        }

        private async void AbrirMapa_Clicked(object sender, EventArgs e)
        {
            bool estaLogueado = Preferences.Get("ConductorSesion", false);

            if (estaLogueado)
            {
                // Constructor corregido sin argumentos
                await Navigation.PushAsync(new ModoConductorInicioPage());
            }
            else
            {
                await DisplayAlert("Sesión requerida", "Debes iniciar sesión como conductor.", "OK");
                RedirigirAlLogin();
            }
        }

        private async void CerrarSesion_Clicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Cerrar sesión", "¿Deseas cerrar la sesión?", "Sí", "Cancelar");

            if (!confirmar) return;

            Preferences.Remove("ConductorSesion");
            Preferences.Remove("ConductorId");
            Preferences.Remove("NombreConductor");
            Preferences.Remove("Token");

            RedirigirAlLogin();
        }

        private void RedirigirAlLogin()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current.MainPage = new NavigationPage(new LoginConductorPage());
            });
        }

        private string GetBaseUrl() => ApiConfig.BaseUrl;
    }
}