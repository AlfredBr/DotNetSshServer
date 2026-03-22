using FxSsh.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlfredBr.SshServer.Core.Tests;

public class AuthenticationEvaluatorTests
{
    [Fact]
    public void Evaluate_AcceptsAnonymousWhenEnabled()
    {
        var args = new UserAuthArgs(session: null!, username: "guest");
        var result = AuthenticationEvaluator.Evaluate(args, allowAnonymous: true, CreateAuthorizedKeysStore());

        Assert.True(result.IsAccepted);
        Assert.Equal("none", result.ConnectionAuthMethod);
    }

    [Fact]
    public void Evaluate_RejectsPasswordAuthentication()
    {
        var args = new UserAuthArgs(session: null!, username: "user", password: "secret");
        var result = AuthenticationEvaluator.Evaluate(args, allowAnonymous: true, CreateAuthorizedKeysStore());

        Assert.False(result.IsAccepted);
        Assert.Equal("password", result.ConnectionAuthMethod);
    }

    [Fact]
    public void Evaluate_RejectsUnknownPublicKey()
    {
        var args = new UserAuthArgs(
            session: null!,
            username: "user",
            keyAlgorithm: "ssh-ed25519",
            fingerprint: "SHA256:test",
            key: [1, 2, 3, 4]);

        var result = AuthenticationEvaluator.Evaluate(args, allowAnonymous: false, CreateAuthorizedKeysStore());

        Assert.False(result.IsAccepted);
        Assert.Equal("publickey", result.ConnectionAuthMethod);
        Assert.Null(result.KeyFingerprint);
    }

    private static AuthorizedKeysStore CreateAuthorizedKeysStore()
        => new(NullLogger.Instance);
}
