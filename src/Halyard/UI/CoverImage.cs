using Microsoft.UI.Xaml.Media.Imaging;

namespace Halyard.UI;

/// <summary>Cover art sized for a tile, not for a poster.</summary>
public static class CoverImage
{
    /// <summary>Wide enough for the largest tile at 150% scaling.</summary>
    private const int RequestWidth = 480;

    private const int RequestHeight = 640;

    /// <summary>
    /// Epic's image CDN resizes on request: a 1200x1600 cover drops from ~700 KB to ~30 KB, which is
    /// what keeps the library from stuttering while dozens of covers download and decode at once.
    /// </summary>
    public static BitmapImage? Create(string? url, int decodeWidth)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Host.EndsWith(".epicgames.com", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(uri.Query))
        {
            uri = new Uri($"{uri.AbsoluteUri}?w={RequestWidth}&h={RequestHeight}&quality=medium&resize=1");
        }

        return new BitmapImage { UriSource = uri, DecodePixelWidth = decodeWidth, DecodePixelType = DecodePixelType.Logical };
    }
}
