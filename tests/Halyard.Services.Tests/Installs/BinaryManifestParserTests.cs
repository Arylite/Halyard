using Halyard.Models.Library;
using Halyard.Services.Installs;
using Halyard.Services.Tests.Fakes;

namespace Halyard.Services.Tests.Installs;

public sealed class BinaryManifestParserTests
{
    [Fact]
    public void Parse_ReadsEverySection()
    {
        var expected = FakeBuild.Create(chunkCount: 20, fileCount: 30).Manifest;

        var parsed = new BinaryManifestParser().Parse(EpicFormatWriter.WriteManifest(expected));

        Assert.Equal(expected.AppName, parsed.AppName);
        Assert.Equal(expected.BuildVersion, parsed.BuildVersion);
        Assert.Equal(expected.LaunchExecutable, parsed.LaunchExecutable);
        Assert.Equal(expected.LaunchCommand, parsed.LaunchCommand);
        Assert.Equal("ChunksV4", parsed.ChunkDirectory);
        Assert.Equal(expected.Chunks.Count, parsed.Chunks.Count);

        foreach (var (guid, chunk) in expected.Chunks)
        {
            var actual = parsed.Chunks[guid];
            Assert.Equal(chunk.Hash, actual.Hash);
            Assert.Equal(chunk.Sha1, actual.Sha1);
            Assert.Equal(chunk.GroupNumber, actual.GroupNumber);
            Assert.Equal(chunk.WindowSize, actual.WindowSize);
            Assert.Equal(chunk.FileSize, actual.FileSize);
        }

        Assert.Equal(expected.Files.Count, parsed.Files.Count);
        for (var i = 0; i < expected.Files.Count; i++)
        {
            Assert.Equal(expected.Files[i].Path, parsed.Files[i].Path);
            Assert.Equal(expected.Files[i].Sha1, parsed.Files[i].Sha1);
            Assert.Equal(expected.Files[i].InstallTags, parsed.Files[i].InstallTags);
            Assert.Equal(expected.Files[i].Parts, parsed.Files[i].Parts);
            Assert.Equal(expected.Files[i].Size, parsed.Files[i].Size);
        }
    }

    [Fact]
    public void Parse_ReadsThePrerequisite()
    {
        var prerequisite = new GamePrerequisite(
            ["UE4PrereqSetup_x64"],
            "Unreal Engine prerequisites",
            Path.Combine("Engine", "Extras", "Redist", "UE4PrereqSetup_x64.exe"),
            "/quiet /norestart");
        var expected = FakeBuild.Create(chunkCount: 2, fileCount: 2).Manifest with { Prerequisite = prerequisite };

        var parsed = new BinaryManifestParser().Parse(EpicFormatWriter.WriteManifest(expected));

        Assert.NotNull(parsed.Prerequisite);
        Assert.Equal(prerequisite.Ids, parsed.Prerequisite.Ids);
        Assert.Equal(prerequisite.Name, parsed.Prerequisite.Name);
        Assert.Equal(prerequisite.Path, parsed.Prerequisite.Path);
        Assert.Equal(prerequisite.Arguments, parsed.Prerequisite.Arguments);
    }

    [Fact]
    public void Parse_HasNoPrerequisiteWhenTheBuildDeclaresNone() =>
        Assert.Null(new BinaryManifestParser().Parse(EpicFormatWriter.WriteManifest(FakeBuild.Create(2, 2).Manifest)).Prerequisite);

    [Fact]
    public void Parse_RejectsCorruptData()
    {
        var data = EpicFormatWriter.WriteManifest(FakeBuild.Create(chunkCount: 2, fileCount: 2).Manifest);
        data[^1] ^= 0xFF;

        Assert.ThrowsAny<Exception>(() => new BinaryManifestParser().Parse(data));
    }

    [Fact]
    public void Parse_ExplainsLegacyJsonManifests() =>
        Assert.Throws<NotSupportedException>(() => new BinaryManifestParser().Parse("{\"ManifestFileVersion\":\"013000000000\"}"u8.ToArray()));
}
