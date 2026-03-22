using FxSsh.Services;

namespace AlfredBr.SshServer.Core;

internal static class AuthenticationEvaluator
{
    public static AuthenticationEvaluation Evaluate(
        UserAuthArgs args,
        bool allowAnonymous,
        AuthorizedKeysStore authorizedKeys)
    {
        return args.AuthMethod switch
        {
            "none" => new AuthenticationEvaluation(
                IsAccepted: allowAnonymous,
                ConnectionAuthMethod: "none",
                KeyFingerprint: null),

            "publickey" when args.Key is not null
                && args.KeyAlgorithm is not null
                && authorizedKeys.IsAuthorized(args.KeyAlgorithm, args.Key) => new AuthenticationEvaluation(
                    IsAccepted: true,
                    ConnectionAuthMethod: "publickey",
                    KeyFingerprint: args.Fingerprint),

            "password" => new AuthenticationEvaluation(
                IsAccepted: false,
                ConnectionAuthMethod: "password",
                KeyFingerprint: null),

            _ => new AuthenticationEvaluation(
                IsAccepted: false,
                ConnectionAuthMethod: args.AuthMethod,
                KeyFingerprint: null),
        };
    }
}

internal sealed record AuthenticationEvaluation(
    bool IsAccepted,
    string ConnectionAuthMethod,
    string? KeyFingerprint);
