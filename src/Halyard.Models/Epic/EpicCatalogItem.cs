namespace Halyard.Models.Epic;

/// <param name="IsAddOn">DLC or other content that belongs to another game.</param>
public sealed record EpicCatalogItem(string Id, string Title, bool IsAddOn, string? CoverImageUrl);
