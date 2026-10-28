namespace Halyard.Models.Epic;

/// <summary>Where to fetch a build's manifest; several CDNs are usually offered.</summary>
public sealed record ManifestLocation(string BuildVersion, IReadOnlyList<Uri> ManifestUris);
