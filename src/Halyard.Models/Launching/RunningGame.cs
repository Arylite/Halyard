using Halyard.Models.Library;

namespace Halyard.Models.Launching;

public sealed record RunningGame(Game Game, int ProcessId, DateTimeOffset StartedAt);
