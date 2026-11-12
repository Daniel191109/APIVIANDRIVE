using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ViandriveApp.Helpers;
using ViandriveApp.Models;

namespace ViandriveApp.Views
{
    public partial class MainPage : ContentPage
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private string _tokenRegistro = string.Empty;
        private string _correoVerificado = string.Empty;

        public MainPage()
        {
            InitializeComponent();
        }

        private static string GenerarToken()
            => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        private async Task<bool> SolicitarEnvioCorreoAsync(string correo, string token)
        {
            try
            {
                // "MainPage" es el tipo reconocido por el backend para el correo de registro.
                var requestData = new { Email = correo, Token = token, Tipo = "MainPage" };
                using var content = new StringContent(
                    JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                using var response = await Http.PostAsync(
                    ApiConfig.BuildUrl("api/EmailApi/enviar-token"), content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enviando token de registro: {ex}");
                await DisplayAlert("Error de red", "No se pudo conectar con el servicio de correo.", "OK");
                return false;
            }
        }

        private async void OnRegistrarUsuarioClicked(object sender, EventArgs e)
            => await IniciarVerificacionCorreoAsync();

        private async void OnContinuarRegistroClicked(object sender, EventArgs e)
            => await IniciarVerificacionCorreoAsync();

        private async Task IniciarVerificacionCorreoAsync()
        {
            string nombre = NombreEntry.Text?.Trim() ?? string.Empty;
            string correo = CorreoEntry.Text?.Trim() ?? string.Empty;
            string clave = ClaveEntry.Text ?? string.Empty;
            string celular = TelefonoEntry.Text?.Trim() ?? string.Empty;
            string metodoPago = MetodoPagoPicker.SelectedItem?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(correo) ||
                string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(celular) ||
                string.IsNullOrWhiteSpace(metodoPago))
            {
                await DisplayAlert("Validación", "Completa todos los campos, incluido el celular.", "OK");
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(correo, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                await DisplayAlert("Correo inválido", "Ingresa una dirección de correo válida.", "OK");
                return;
            }

            // Coincide con la validación que aplica ClienteApiController.
            if (clave.Length < 8 || !clave.Any(char.IsLetter) || !clave.Any(char.IsDigit) ||
                !clave.Any(c => !char.IsLetterOrDigit(c)))
            {
                await DisplayAlert("Contraseña débil", "Usa al menos 8 caracteres e incluye letras, números y un símbolo.", "OK");
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(celular, @"^\d{10}$"))
            {
                await DisplayAlert("Celular inválido", "El backend requiere un número de celular de exactamente 10 dígitos.", "OK");
                return;
            }

            ContinuarButton.IsEnabled = false;
            try
            {
                _tokenRegistro = GenerarToken();
                _correoVerificado = correo;
                bool enviado = await SolicitarEnvioCorreoAsync(correo, _tokenRegistro);
                if (!enviado)
                {
                    _tokenRegistro = string.Empty;
                    await DisplayAlert("Error", "No se pudo enviar el código de verificación.", "OK");
                    return;
                }

                TokenEntry.Text = string.Empty;
                PanelDatosRegistro.IsVisible = false;
                PanelTokenVerification.IsVisible = true;
                await DisplayAlert("Código enviado", $"Revisa el correo {correo} e ingresa el código de 6 dígitos.", "OK");
            }
            finally
            {
                ContinuarButton.IsEnabled = true;
            }
        }

        private async void OnRegistrarUsuarioFinalClicked(object sender, EventArgs e)
            => await CompletarRegistroAsync();

        private async Task CompletarRegistroAsync()
        {
            string tokenIngresado = TokenEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_tokenRegistro) ||
                !string.Equals(tokenIngresado, _tokenRegistro, StringComparison.Ordinal))
            {
                await DisplayAlert("Código incorrecto", "El código no coincide. Revisa el correo e inténtalo nuevamente.", "OK");
                return;
            }

            var cliente = new ClienteRegistroDto
            {
                NombreCompleto = NombreEntry.Text?.Trim() ?? string.Empty,
                Correo = _correoVerificado,
                Clave = ClaveEntry.Text ?? string.Empty,
                Celular = TelefonoEntry.Text?.Trim() ?? string.Empty,
                MetodoPagoPreferido = MetodoPagoPicker.SelectedItem?.ToString() ?? "Efectivo"
            };

            RegistrarFinalButton.IsEnabled = false;
            RegistrarFinalButton.Text = "Registrando...";
            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(cliente), Encoding.UTF8, "application/json");
                using var response = await Http.PostAsync(
                    ApiConfig.BuildUrl("api/ClienteApi/registrar"), content);
                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    await DisplayAlert($"Error {(int)response.StatusCode}",
                        string.IsNullOrWhiteSpace(body) ? "No se pudo guardar el cliente." : body, "OK");
                    return;
                }

                _tokenRegistro = string.Empty;
                await DisplayAlert("Registro exitoso", "El cliente fue guardado en la base de datos.", "OK");

                if (lblOrigen != null)
                {
                    var direccion = await UbicacionHelper.ObtenerDireccionOrigenAsync();
                    lblOrigen.Text = $"Origen detectado: {direccion}";
                }

                await Shell.Current.GoToAsync("///LoginPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error de conexión", ex.Message, "OK");
            }
            finally
            {
                RegistrarFinalButton.IsEnabled = true;
                RegistrarFinalButton.Text = "Registrar cliente";
            }
        }

        private void OnVolverDatosClicked(object sender, EventArgs e)
        {
            PanelTokenVerification.IsVisible = false;
            PanelDatosRegistro.IsVisible = true;
            TokenEntry.Text = string.Empty;
            _tokenRegistro = string.Empty;
        }
    }
}
