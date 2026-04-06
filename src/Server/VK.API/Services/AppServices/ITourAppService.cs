using Microsoft.AspNetCore.Mvc;
using VK.Shared.Constants;

namespace VK.API.Services.AppServices;

public interface ITourAppService
{
    Task<IActionResult> GetToursAsync(string languageCode = LanguageConstants.Vietnamese);
    Task<IActionResult> GetTourByIdAsync(int tourId, string languageCode = LanguageConstants.Vietnamese);
}
