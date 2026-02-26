using CommunityToolkit.Mvvm.Messaging.Messages;

namespace VK.Mobile.ViewModels;

/// <summary>Fired when user changes app language so views can re-bind.</summary>
public class LanguageChangedMessage : ValueChangedMessage<string>
{
    public LanguageChangedMessage(string languageCode) : base(languageCode) { }
}
