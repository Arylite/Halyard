namespace Halyard.Models.Library;

/// <summary>
/// An installer a build declares must run once after install (Visual C++ runtime, DirectX,
/// anti-cheat service...). The official launcher runs it elevated, and never twice for the same ids.
/// </summary>
/// <param name="Ids">What the installer provides; once recorded, other games needing them skip it.</param>
/// <param name="Path">Executable, relative to the install folder.</param>
public sealed record GamePrerequisite(IReadOnlyList<string> Ids, string Name, string Path, string Arguments);
