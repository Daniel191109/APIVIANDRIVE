using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ViaDriveApp.Helpers;
using ViandriveApp.Helpers;
using ViandriveApp.Models;
using ViandriveApp.Services;
using VianDriveApp.Models;
using VianDriveApp.Services;

namespace ViandriveApp.Views
{
    public partial class SolicitudesPendientesPage : ContentPage
    {
        public ObservableCollection<SolicitudViajeDTO> Solicitudes { get; set; } = new();

        public SolicitudesPendientesPage()
        {
            InitializeComponent();
            SolicitudesCollectionView.ItemsSource = Solicitudes;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                var conductorId = PreferenciasUsuario.ObtenerConductorId();
                var solicitudes = await SolicitudService.ObtenerSolicitudesPendientes(conductorId);

                Solicitudes.Clear();
                foreach (var solicitud in solicitudes)
                {
                    solicitud.AceptarCommand = new Command(async () =>
                    {
                        var confirmado = await DisplayAlert("Confirmar", "¿Aceptar esta solicitud?", "Sí", "No");
                        if (confirmado)
                        {
                            var resultado = await SolicitudService.AceptarSolicitud(solicitud.Id, conductorId);
                            if (resultado)
                            {
                                await DisplayAlert("Éxito", "Solicitud aceptada correctamente.", "OK");
                                Solicitudes.Remove(solicitud);
                            }
                            else
                            {
                                await DisplayAlert("Error", "No se pudo aceptar la solicitud.", "OK");
                            }
                        }
                    });

                    Solicitudes.Add(solicitud);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudieron cargar las solicitudes: {ex.Message}", "OK");
            }
        }
    }
}