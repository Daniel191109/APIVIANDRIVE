using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using ViandriveApp.Helpers;
using ViandriveApp.Models;

namespace ViandriveApp.Views
{
    public partial class LoginConductorPage : ContentPage
    {
        private readonly HttpClient cliente;

        // Variable para controlar si la contraseña está oculta o visible
        private bool _isPasswordHidden = true;

        public LoginConductorPage()
        {
            InitializeComponent();
            cliente = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(60) // Ampliado a 60s para soportar el arranque en frío de Render
            };
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            try
            {
                string documento = CedulaEntry.Text?.Trim();
                string password = PasswordEntry.Text?.Trim();

                if (string.IsNullOrWhiteSpace(documento))
                {
                    await DisplayAlert("Validación", "Ingresa tu número de documento.", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    await DisplayAlert("Validación", "Ingresa tu contraseña.", "OK");
                    return;
                }

                var login = new LoginDto
                {
                    Email = documento,
                    Password = password
                };

                string json = JsonSerializer.Serialize(login);

                using var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

                // URL oficial de Render obtenida desde ApiConfig de forma directa
                string baseUrl = ApiConfig.BaseUrl.TrimEnd('/');
                string url = $"{baseUrl}/api/Auth/login-conductor";

                IsBusy = true;

                var response = await cliente.PostAsync(url, content);
                string respuestaJson = await response.Content.ReadAsStringAsync();

                Console.WriteLine("===== LOGIN CONDUCTOR =====");
                Console.WriteLine($"Documento enviado: {documento}");
                Console.WriteLine($"Código HTTP: {(int)response.StatusCode}");
                Console.WriteLine($"Respuesta: {respuestaJson}");

                if (!response.IsSuccessStatusCode)
                {
                    string mensaje = "Documento o contraseña incorrectos.";

                    try
                    {
                        using var doc = JsonDocument.Parse(respuestaJson);
                        if (doc.RootElement.TryGetProperty("error", out var errorElement))
                        {
                            mensaje = errorElement.GetString() ?? mensaje;
                        }
                    }
                    catch
                    {
                        if (!string.IsNullOrWhiteSpace(respuestaJson))
                            mensaje = respuestaJson;
                    }

                    await DisplayAlert($"Error {(int)response.StatusCode}", mensaje, "OK");
                    return;
                }

                var conductor = JsonSerializer.Deserialize<ConductorDto>(
                    respuestaJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (conductor == null || string.IsNullOrWhiteSpace(conductor.Nombre))
                {
                    await DisplayAlert("Error", "No se pudieron procesar los datos del conductor devueltos por el servidor.", "OK");
                    return;
                }

                Preferences.Set("ConductorSesion", true);
                Preferences.Set("ConductorId", conductor.Id);
                Preferences.Set("ConductorNombre", conductor.Nombre ?? "Sin nombre");
                Preferences.Set("ConductorDocumento", documento);
                Preferences.Set("ConductorEmail", conductor.Email ?? "Sin email");
                Preferences.Set("ConductorPlaca", conductor.Placa ?? "Sin placa");
                Preferences.Set("ConductorModelo", conductor.Modelo ?? "Sin modelo");
                Preferences.Set("ConductorColor", conductor.Color ?? "Sin color");
                Preferences.Set("ConductorAnio", conductor.Anio);

                await Navigation.PushAsync(new ModoConductorInicioPage());
            }
            catch (HttpRequestException ex)
            {
                await DisplayAlert("Servidor no disponible", "No se pudo conectar con la API. Verifica que la API esté en ejecución y la IP sea alcanzable.\n\n" + ex.Message, "OK");
            }
            catch (TaskCanceledException)
            {
                await DisplayAlert("Tiempo agotado", "El servidor tardó demasiado en responder. Si está en Render, recuerda que puede tardar unos segundos en despertar.", "OK");
            }
            catch (JsonException ex)
            {
                await DisplayAlert("Error de respuesta", $"La API devolvió una respuesta que no se pudo procesar.\n\n{ex.Message}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error crítico", ex.Message, "OK");
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnAccesoRapidoClicked(object sender, EventArgs e)
        {
            Preferences.Set("ConductorSesion", true);
            Preferences.Set("ConductorId", 999);
            Preferences.Set("ConductorNombre", "Conductor de Prueba");
            Preferences.Set("ConductorDocumento", "0000000000");
            Preferences.Set("ConductorEmail", "prueba@viandrive.com");
            Preferences.Set("ConductorPlaca", "TEST-123");
            Preferences.Set("ConductorModelo", "Coche de Pruebas");
            Preferences.Set("ConductorColor", "Negro");
            Preferences.Set("ConductorAnio", 2024);

            await Navigation.PushAsync(new ModoConductorInicioPage());
        }

        private async void OnOlvidePasswordTapped(object sender, EventArgs e)
        {
            // Navegar a la nueva vista de recuperación
            await Navigation.PushAsync(new RecuperarPasswordPage());
        }

        // =========================================
        // MOSTRAR / OCULTAR CONTRASEÑA (EL OJITO)
        // =========================================
        private void OnTogglePasswordVisibilityTapped(object sender, EventArgs e)
        {
            _isPasswordHidden = !_isPasswordHidden;

            // Alterna la propiedad IsPassword del Entry
            PasswordEntry.IsPassword = _isPasswordHidden;

            // Cambia el emoji y la opacidad para dar retroalimentación visual
            TogglePasswordIcon.Text = _isPasswordHidden ? "👁️" : "🙈";
            TogglePasswordIcon.Opacity = _isPasswordHidden ? 0.5 : 1.0;
        }
    }
}