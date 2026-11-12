using Microsoft.Maui.Controls;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ViandriveApp.Helpers;

namespace ViandriveApp.Views
{
    public partial class ForgotPasswordPage : ContentPage
    {
        private string _codigoGenerado = string.Empty;
        private string _correoUsuario = string.Empty;
        private readonly HttpClient _httpClient;

        public ForgotPasswordPage()
        {
            InitializeComponent();

            // Inicializamos HttpClient al igual que en LoginPage
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        #region Método de Comunicación con API

        private async Task<bool> SolicitarEnvioCorreoAsync(string correo, string token)
        {
            try
            {
                // Enviamos el tipo "recuperacion" para que el backend sepa qué texto mostrar en el correo.
                // Ajusta este string según lo que requiera tu API.
                var requestData = new { Email = correo, Token = token, Tipo = "recuperacion" };
                var json = JsonSerializer.Serialize(requestData);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                // IMPORTANTE: Cambia "localhost" por "10.0.2.2" si usas el emulador de Android
                var url = ApiConfig.BuildUrl("api/EmailApi/enviar-token");

                using var response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Paso 1: Enviar Código

        private async void OnEnviarCodigoClicked(object sender, EventArgs e)
        {
            string correo = CorreoEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(correo) || !Regex.IsMatch(correo, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                await DisplayAlert("Atención", "Por favor ingresa un correo electrónico válido.", "OK");
                return;
            }

            _correoUsuario = correo;

            // Bloquear UI opcionalmente mientras carga (si tienes el botón nombrado, puedes deshabilitarlo aquí)

            Random random = new Random();
            _codigoGenerado = random.Next(100000, 999999).ToString();

            // Llamada a la API backend para enviar el correo
            bool enviado = await SolicitarEnvioCorreoAsync(_correoUsuario, _codigoGenerado);

            if (!enviado)
            {
                await DisplayAlert("Error", "No se pudo enviar el código de recuperación al correo. Verifica tu conexión.", "OK");
                return;
            }

            // Cambiar de vista al Paso 2 si el envío fue exitoso
            PanelSolicitarCorreo.IsVisible = false;
            PanelVerificarCodigo.IsVisible = true;
            lblSubtituloHeader.Text = "Ingresa el código que enviamos a tu correo";
            lblInfoCodigoEnviado.Text = $"Hemos enviado un código de 6 dígitos a:\n{_correoUsuario}";

            await DisplayAlert("Código enviado", $"Hemos enviado las instrucciones a {_correoUsuario}.", "OK");
        }

        #endregion

        #region Paso 2: Verificar Código

        private async void OnVerificarCodigoClicked(object sender, EventArgs e)
        {
            string tokenIngresado = TokenResetEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(tokenIngresado) || tokenIngresado.Length < 6)
            {
                await DisplayAlert("Atención", "Ingresa el código de 6 dígitos enviado.", "OK");
                return;
            }

            // Verificación del token generado
            if (tokenIngresado == _codigoGenerado)
            {
                PanelVerificarCodigo.IsVisible = false;
                PanelNuevaContrasena.IsVisible = true;
                lblSubtituloHeader.Text = "Establece tu nueva contraseña";
            }
            else
            {
                await DisplayAlert("Error", "El código ingresado es incorrecto o ha expirado.", "OK");
            }
        }

        private async void OnReenviarCodigoClicked(object sender, EventArgs e)
        {
            Random random = new Random();
            _codigoGenerado = random.Next(100000, 999999).ToString();

            // Reenviar petición al API de correo
            bool enviado = await SolicitarEnvioCorreoAsync(_correoUsuario, _codigoGenerado);

            if (enviado)
            {
                await DisplayAlert("Reenviado", $"Se ha reexpedido un nuevo código a {_correoUsuario}.", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Ocurrió un problema al intentar reenviar el código.", "OK");
            }
        }

        #endregion

        #region Paso 3: Guardar Nueva Contraseña

        private async void OnCambiarClaveClicked(object sender, EventArgs e)
        {
            string clave1 = NuevaClaveEntry.Text;
            string clave2 = ConfirmarNuevaClaveEntry.Text;

            if (string.IsNullOrWhiteSpace(clave1) || clave1.Length < 6)
            {
                await DisplayAlert("Atención", "La contraseña debe tener al menos 6 caracteres.", "OK");
                return;
            }

            if (clave1 != clave2)
            {
                await DisplayAlert("Atención", "Las contraseñas no coinciden.", "OK");
                return;
            }

            // Esta versión todavía no puede persistir el cambio en el backend.
            // Se evita mostrar un falso éxito o cerrar la pantalla como si la clave hubiera cambiado.
            await DisplayAlert(
                "Restablecimiento no completado",
                "El código se comprueba en el dispositivo, pero el backend aún no tiene conectado el guardado seguro de la nueva contraseña. No se modificó tu clave.",
                "Entendido");
        }

        #endregion

        #region Navegación

        private void OnVolverLoginClicked(object sender, EventArgs e)
        {
            RedirigirLogin();
        }

        private void RedirigirLogin()
        {
            if (Shell.Current != null)
            {
                Shell.Current.GoToAsync("//LoginPage");
            }
            else
            {
                Application.Current.MainPage = new NavigationPage(new LoginPage());
            }
        }

        #endregion
    }
}