using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using ViandriveApp.Helpers;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ViandriveApp.Views;

public partial class LoginPage : ContentPage
{
    private bool isPasswordHidden = true;
    private readonly HttpClient _httpClient;

    // Variables para el manejo del token y credenciales temporales
    private string tokenGenerado = string.Empty;
    private string correoTemporal = string.Empty;
    private string claveTemporal = string.Empty;

    public LoginPage()
    {
        InitializeComponent();

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Estado inicial de los botones
        PasswordEntry.IsPassword = true;
        TogglePasswordButton.Source = "eye_closed.png";
    }

    // =========================================================
    // ANIMACIÓN DE TRANSICIÓN AL ABRIR LA PANTALLA
    // =========================================================
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // 1. Estado inicial (Efecto Splash Screen)
            HeaderContainer.TranslationY = 200; // Logo/Header posicionado en el centro
            HeaderContainer.Opacity = 1;

            FormContainer.Opacity = 0;
            FormContainer.TranslationY = 120; // Formulario oculto abajo
            FormContainer.Scale = 0.95;       // Ligero escalado para suavizar la subida

            FooterLabel.Opacity = 0;
            TitleLabel.Opacity = 0;
            SubtitleLabel.Opacity = 0;

            // 2. Pausa estética para simular la pantalla de presentación (Splash Screen)
            await Task.Delay(800);

            // 3. Fase 1: El Header sube a su lugar natural de forma fluida (1.1s)
            var headerTask = HeaderContainer.TranslateTo(0, 0, 1100, Easing.CubicOut);

            // 4. Fase 2: Entrada escalonada de los títulos
            await Task.Delay(250);
            var titleTask = Task.WhenAll(
                TitleLabel.FadeTo(1, 700, Easing.CubicOut),
                SubtitleLabel.FadeTo(1, 700, Easing.CubicOut)
            );

            // 5. Fase 3: Subida con opacidad y escalado del Formulario (1.2s)
            await Task.Delay(200);
            var formTask = Task.WhenAll(
                FormContainer.TranslateTo(0, 0, 1200, Easing.CubicOut),
                FormContainer.FadeTo(1, 1000, Easing.CubicOut),
                FormContainer.ScaleTo(1.0, 1200, Easing.CubicOut)
            );

            // 6. Fase 4: Aparición discreta del Footer
            var footerTask = FooterLabel.FadeTo(1, 900, Easing.Linear);

            // Sincronización completa de todas las tareas
            await Task.WhenAll(headerTask, titleTask, formTask, footerTask);
        }
        catch
        {
            // Fallback en caso de fallo de renderizado
            HeaderContainer.TranslationY = 0;
            HeaderContainer.Opacity = 1;
            FormContainer.TranslationY = 0;
            FormContainer.Opacity = 1;
            FormContainer.Scale = 1;
            TitleLabel.Opacity = 1;
            SubtitleLabel.Opacity = 1;
            FooterLabel.Opacity = 1;
        }
    }

    // =========================================================
    // LÓGICA DE NEGOCIO Y API
    // =========================================================

    private string GenerarToken()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    private async Task<bool> SolicitarEnvioCorreoAsync(string correo, string token)
    {
        try
        {
            var requestData = new { Email = correo, Token = token, Tipo = "LoginPage" };
            var json = JsonSerializer.Serialize(requestData);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = ApiConfig.BuildUrl("api/EmailApi/enviar-token");

            using var response = await _httpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        correoTemporal = UsernameEntry.Text?.Trim() ?? string.Empty;
        claveTemporal = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(correoTemporal))
        {
            await DisplayAlert("Correo requerido", "Ingresa tu correo electrónico.", "Aceptar");
            UsernameEntry.Focus();
            return;
        }

        if (!EsCorreoValido(correoTemporal))
        {
            await DisplayAlert("Correo inválido", "Ingresa una dirección de correo válida.", "Aceptar");
            UsernameEntry.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(claveTemporal))
        {
            await DisplayAlert("Contraseña requerida", "Ingresa tu contraseña.", "Aceptar");
            PasswordEntry.Focus();
            return;
        }

        LoginButton.IsEnabled = false;
        LoginButton.Text = "Enviando código...";

        try
        {
            tokenGenerado = GenerarToken();
            bool enviado = await SolicitarEnvioCorreoAsync(correoTemporal, tokenGenerado);

            if (!enviado)
            {
                await DisplayAlert("Error", "No se pudo enviar el código de verificación al correo. Verifica la conexión con la API.", "Aceptar");
                return;
            }

            PanelCredenciales.IsVisible = false;
            PanelTokenVerification.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Ocurrió un problema: {ex.Message}", "Aceptar");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "Continuar →";
        }
    }

    private async void OnVerificarTokenClicked(object sender, EventArgs e)
    {
        string codigoIngresado = TokenEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(codigoIngresado) || codigoIngresado.Length != 6)
        {
            await DisplayAlert("Código incompleto", "Ingresa el código de 6 dígitos que se envió a tu correo.", "Aceptar");
            return;
        }

        if (codigoIngresado != tokenGenerado)
        {
            await DisplayAlert("Código incorrecto", "El código de verificación no coincide. Inténtalo de nuevo.", "Aceptar");
            return;
        }

        VerificarTokenButton.IsEnabled = false;
        VerificarTokenButton.Text = "Verificando...";

        try
        {
            var loginData = new { correo = correoTemporal, clave = claveTemporal };
            string json = JsonSerializer.Serialize(loginData);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            string url = ApiConfig.BuildUrl("api/ClienteApi/login");

            using HttpResponseMessage response = await _httpClient.PostAsync(url, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var loginResult = JsonSerializer.Deserialize<LoginResponse>(
                    responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (loginResult == null || string.IsNullOrWhiteSpace(loginResult.token))
                {
                    await DisplayAlert("Error", "El servidor no devolvió un token válido.", "Aceptar");
                    return;
                }

                Preferences.Set("IsLoggedIn", true);
                Preferences.Set("Token", loginResult.token);
                Preferences.Set("ClienteId", loginResult.clienteId);
                Preferences.Set("Nombre", loginResult.nombre ?? string.Empty);

                string nombre = string.IsNullOrWhiteSpace(loginResult.nombre) ? "Usuario" : loginResult.nombre;
                await DisplayAlert("¡Bienvenido!", $"Hola {nombre}, acceso concedido.", "Continuar");

                // Crear un diccionario con los datos que quieres pasar
                var navigationParameter = new Dictionary<string, object>
                {
                    { "ClienteId", loginResult.clienteId },
                    { "Nombre", loginResult.nombre ?? string.Empty }
                };

                // Navegar a MapaPage pasando los parámetros
                await Shell.Current.GoToAsync("/MapaPage", navigationParameter);
            }
            else
            {
                string mensaje = ObtenerMensajeError(responseBody);
                await DisplayAlert("Error de inicio de sesión", mensaje, "Aceptar");

                PanelTokenVerification.IsVisible = false;
                PanelCredenciales.IsVisible = true;
                TokenEntry.Text = string.Empty;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de red", $"No se pudo completar la solicitud: {ex.Message}", "Aceptar");
        }
        finally
        {
            VerificarTokenButton.IsEnabled = true;
            VerificarTokenButton.Text = "Verificar e Ingresar";
        }
    }

    private void OnVolverCredencialesClicked(object sender, EventArgs e)
    {
        PanelTokenVerification.IsVisible = false;
        PanelCredenciales.IsVisible = true;
        TokenEntry.Text = string.Empty;
    }

    private void OnTogglePasswordVisibility(object sender, EventArgs e)
    {
        isPasswordHidden = !isPasswordHidden;
        PasswordEntry.IsPassword = isPasswordHidden;
        TogglePasswordButton.Source = isPasswordHidden ? "eye_closed.png" : "eye_open.png";
    }

    private bool EsCorreoValido(string correo)
    {
        try
        {
            var mailAddress = new System.Net.Mail.MailAddress(correo);
            return mailAddress.Address.Equals(correo, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string ObtenerMensajeError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return "Credenciales inválidas.";
        try
        {
            var errorResult = JsonSerializer.Deserialize<LoginResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (!string.IsNullOrWhiteSpace(errorResult?.mensaje)) return errorResult.mensaje;
        }
        catch { }

        if (responseBody.Length < 300)
        {
            string mensaje = responseBody.Replace("\"", "").Trim();
            if (!string.IsNullOrWhiteSpace(mensaje)) return mensaje;
        }

        return "Correo o contraseña incorrectos.";
    }

    private async void OnRegisterTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("MainPage");
            }
            else
            {
                await Navigation.PushAsync(new MainPage());
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de navegación", $"No se pudo abrir el registro: {ex.Message}", "Aceptar");
        }
    }

    private async void OnForgotPasswordTapped(object sender, TappedEventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync(nameof(ForgotPasswordPage));
        }
        else
        {
            await Navigation.PushAsync(new ForgotPasswordPage());
        }
    }

    public class LoginResponse
    {
        public string? mensaje { get; set; }
        public int clienteId { get; set; }
        public string? nombre { get; set; }
        public string? token { get; set; }
    }
}