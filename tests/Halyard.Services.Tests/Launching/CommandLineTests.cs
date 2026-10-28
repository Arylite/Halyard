using Halyard.Services.Launching;

namespace Halyard.Services.Tests.Launching;

public sealed class CommandLineTests
{
    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("-dx12 -nosplash", new[] { "-dx12", "-nosplash" })]
    [InlineData("-log  \"-path=C:\\My Games\\x\"", new[] { "-log", "-path=C:\\My Games\\x" })]
    public void Split_HonoursQuotes(string? input, string[] expected) =>
        Assert.Equal(expected, CommandLine.Split(input));
}
