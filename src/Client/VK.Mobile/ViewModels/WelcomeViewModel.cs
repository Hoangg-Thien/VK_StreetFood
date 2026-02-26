using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VK.Mobile.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
    [RelayCommand]
    async Task GetStarted()
    {
        await Shell.Current.GoToAsync("///MainMap");
    }
}
