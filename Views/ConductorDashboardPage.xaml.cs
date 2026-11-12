using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System;

namespace ViandriveApp.Views
{
    public partial class ConductorDashboardPage : ContentPage
    {
        private bool conectado = false;

    public ConductorDashboardPage()
        {
            InitializeComponent();
            CargarDatosConductor();
            _ = ObtenerUbicacionActual();
        }

        private void CargarDatosConductor()
        {
            string nombre = Preferences.Get("ConductorNombre", "N/A");
            string placa = Preferences.Get("ConductorPlaca", "N/A");
            string modelo = Preferences.Get("ConductorModelo", "N/A");
            string color = Preferences.Get("ConductorColor", "N/A");
            int anio = Preferences.Get("ConductorAnio", 0);

            NombreLabel.Text = $"Bienvenido: {nombre}";
            PlacaLabel.Text = $"Placa: {placa}";
            ModeloLabel.Text = $"Modelo: {modelo}";
            ColorLabel.Text = $"Color: {color}";
            AnioLabel.Text = $"Año: {anio}";
        }

        private async void OnToggleEstadoClicked(object sender, EventArgs e)
        {
            conectado = !conectado;

            EstadoLabel.Text = conectado
                ? "Estado: Conectado"
                : "Estado: Desconectado";

            EstadoLabel.TextColor = conectado
                ? Colors.Green
                : Colors.Red;

            ToggleEstadoButton.Text = conectado
                ? "Desconectarse"
                : "Conectarse";

            if (conectado)
            {
                await Shell.Current.GoToAsync("///ModoConductorInicioPage");
            }
        }

        private async Task ObtenerUbicacionActual()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (status == PermissionStatus.Granted)
                {
                    var location = await Geolocation.GetLastKnownLocationAsync();

                    if (location != null)
                    {
                        UbicacionLabel.Text =
                            $"Ubicación actual:\n" +
                            $"Lat: {location.Latitude:F5}, " +
                            $"Lon: {location.Longitude:F5}";
                    }
                    else
                    {
                        UbicacionLabel.Text = "Ubicación no disponible.";
                    }
                }
                else
                {
                    UbicacionLabel.Text = "Permiso de ubicación denegado.";
                }
            }
            catch (Exception ex)
            {
                UbicacionLabel.Text = $"Error: {ex.Message}";
            }
        }
    }


}
