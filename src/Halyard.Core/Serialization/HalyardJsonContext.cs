using System.Text.Json.Serialization;
using Halyard.Models.Auth;
using Halyard.Models.Downloads;
using Halyard.Models.Library;
using Halyard.Models.Settings;

namespace Halyard.Core.Serialization;

/// <summary>Source-generated serializers for the files Halyard owns.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(LauncherSettings))]
[JsonSerializable(typeof(List<Game>))]
[JsonSerializable(typeof(List<InstallRequest>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(EpicSession))]
public sealed partial class HalyardJsonContext : JsonSerializerContext;
