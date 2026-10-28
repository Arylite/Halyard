namespace Halyard.Models.Epic;

/// <summary>An app the account owns, as listed by Epic's launcher service.</summary>
public sealed record EpicAsset(string AppName, string Namespace, string CatalogItemId, string BuildVersion);
