using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace ViandriveApp.Views;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Posiciones iniciales para la animación de entrada
        HeaderContainer.TranslationY = -20;
        HeaderContainer.Opacity = 0;

        CardsContainer.TranslationY = 40;
        CardsContainer.Opacity = 0;

        // Animación suave de aparición escalonada
        _ = BgLogoImage.ScaleTo(1.15, 2000, Easing.CubicOut);

        await Task.WhenAll(
            HeaderContainer.TranslateTo(0, 0, 700, Easing.CubicOut),
            HeaderContainer.FadeTo(1, 600, Easing.CubicOut)
        );

        await Task.WhenAll(
            CardsContainer.TranslateTo(0, 0, 700, Easing.CubicOut),
            CardsContainer.FadeTo(1, 600, Easing.CubicOut)
        );
    }

    private async void CardPasajero_Tapped(object sender, EventArgs e)
    {
        if (sender is Border card)
        {
            await card.ScaleTo(0.92, 90, Easing.SpringIn);
            await card.ScaleTo(1.0, 90, Easing.SpringOut);
        }

        await Shell.Current.GoToAsync("///LoginPage");
    }

    private async void CardConductor_Tapped(object sender, EventArgs e)
    {
        if (sender is Border card)
        {
            await card.ScaleTo(0.92, 90, Easing.SpringIn);
            await card.ScaleTo(1.0, 90, Easing.SpringOut);
        }

        await Shell.Current.GoToAsync("///LoginConductorPage");
    }
}