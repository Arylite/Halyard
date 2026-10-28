namespace Halyard.Models.Installs;

/// <summary>Epic's GUID layout: four little-endian 32-bit words, printed as 32 hex digits.</summary>
public readonly record struct ChunkGuid(uint A, uint B, uint C, uint D)
{
    public override string ToString() => $"{A:X8}{B:X8}{C:X8}{D:X8}";
}
