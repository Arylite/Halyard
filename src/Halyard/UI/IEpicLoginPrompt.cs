using Halyard.Models.Auth;

namespace Halyard.UI;

/// <summary>Lets the user sign in to Epic in an embedded browser.</summary>
public interface IEpicLoginPrompt
{
    /// <returns>The completed sign-in, or <see langword="null"/> if the user gave up.</returns>
    Task<EpicWebLogin?> SignInAsync(EpicWebLoginFlow flow);

    /// <summary>Deletes the embedded browser's profile, so the next sign-in starts from scratch.</summary>
    void ForgetBrowserSession();
}
