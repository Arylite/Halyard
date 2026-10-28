using System.Globalization;
using Halyard.Core.Auth;
using Halyard.Core.Launching;
using Halyard.Models.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Launching;

/// <summary>Appends the arguments Epic titles read to authenticate (exchange code, account, locale).</summary>
public sealed class EpicLaunchArgumentsBuilder(IAuthService auth, ILogger<EpicLaunchArgumentsBuilder> logger) : ILaunchArgumentsBuilder
{
    public async Task<IReadOnlyList<string>> BuildAsync(Game game, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> baseArguments = [.. game.Installation?.Arguments ?? [], .. CommandLine.Split(game.CustomArguments)];
        if (game.EpicAppName is null || game.LaunchOffline)
        {
            return baseArguments;
        }

        if (auth.Session is not { } session)
        {
            logger.LogWarning("{Game} expects an Epic account but none is signed in; launching offline", game.Title);
            return baseArguments;
        }

        var exchangeCode = await auth.CreateExchangeCodeAsync(cancellationToken);
        return
        [
            .. baseArguments,
            "-AUTH_LOGIN=unused",
            $"-AUTH_PASSWORD={exchangeCode}",
            "-AUTH_TYPE=exchangecode",
            $"-epicapp={game.EpicAppName}",
            "-epicenv=Prod",
            "-EpicPortal",
            $"-epicusername={session.DisplayName}",
            $"-epicuserid={session.AccountId}",
            $"-epiclocale={CultureInfo.CurrentUICulture.Name}",
        ];
    }
}
