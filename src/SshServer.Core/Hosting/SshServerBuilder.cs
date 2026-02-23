using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SshServer;

/// <summary>
/// Fluent builder for configuring and creating an SSH server host.
/// </summary>
public class SshServerBuilder
{
    private readonly SshServerOptions _options = new();
    private Action<ILoggingBuilder>? _loggingConfiguration;
    private Func<SshShellApplication>? _applicationFactory;
    private IConfiguration? _configuration;

    /// <summary>
    /// Set the port to listen on.
    /// </summary>
    public SshServerBuilder UsePort(int port)
    {
        _options.Port = port;
        return this;
    }

    /// <summary>
    /// Set the SSH protocol banner.
    /// </summary>
    public SshServerBuilder UseBanner(string banner)
    {
        _options.Banner = banner;
        return this;
    }

    /// <summary>
    /// Set the path to the host key file.
    /// </summary>
    public SshServerBuilder UseHostKeyPath(string path)
    {
        _options.HostKeyPath = path;
        return this;
    }

    /// <summary>
    /// Enable or disable anonymous authentication.
    /// </summary>
    public SshServerBuilder AllowAnonymous(bool allow = true)
    {
        _options.AllowAnonymous = allow;
        return this;
    }

    /// <summary>
    /// Set the path to the authorized_keys file for public key authentication.
    /// </summary>
    public SshServerBuilder UseAuthorizedKeysFile(string path)
    {
        _options.AuthorizedKeysPath = path;
        return this;
    }

    /// <summary>
    /// Set the session idle timeout.
    /// </summary>
    public SshServerBuilder UseSessionTimeout(TimeSpan timeout)
    {
        _options.SessionTimeoutMinutes = (int)timeout.TotalMinutes;
        return this;
    }

    /// <summary>
    /// Set the maximum number of concurrent connections.
    /// </summary>
    public SshServerBuilder UseMaxConnections(int maxConnections)
    {
        _options.MaxConnections = maxConnections;
        return this;
    }

    /// <summary>
    /// Set the minimum log level.
    /// </summary>
    public SshServerBuilder UseLogLevel(LogLevel level)
    {
        _options.LogLevel = level.ToString();
        return this;
    }

    /// <summary>
    /// Register the application type to use for SSH sessions.
    /// </summary>
    /// <typeparam name="TApp">The application type that inherits from SshShellApplication.</typeparam>
    public SshServerBuilder UseApplication<TApp>() where TApp : SshShellApplication, new()
    {
        _applicationFactory = () => new TApp();
        return this;
    }

    /// <summary>
    /// Register the application factory to use for SSH sessions.
    /// </summary>
    /// <param name="factory">Factory function that creates application instances.</param>
    public SshServerBuilder UseApplication(Func<SshShellApplication> factory)
    {
        _applicationFactory = factory;
        return this;
    }

    /// <summary>
    /// Configure logging for the SSH server.
    /// </summary>
    public SshServerBuilder ConfigureLogging(Action<ILoggingBuilder> configure)
    {
        _loggingConfiguration = configure;
        return this;
    }

    /// <summary>
    /// Use configuration from an IConfiguration instance.
    /// Settings will be loaded from the "SshServer" section.
    /// </summary>
    public SshServerBuilder UseConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
        return this;
    }

    /// <summary>
    /// Load configuration from appsettings.json and environment variables.
    /// </summary>
    /// <param name="args">Command-line arguments to include in configuration.</param>
    public SshServerBuilder UseDefaultConfiguration(string[]? args = null)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("SSHSERVER_");

        if (args != null)
            builder.AddCommandLine(args);

        _configuration = builder.Build();
        return this;
    }

    /// <summary>
    /// Build the SSH server host.
    /// </summary>
    public SshServerHost Build()
    {
        // Apply configuration if provided
        if (_configuration != null)
        {
            _configuration.GetSection(SshServerOptions.SectionName).Bind(_options);
        }

        // Validate
        if (_applicationFactory == null)
        {
            throw new InvalidOperationException(
                "No application registered. Call UseApplication<TApp>() before Build().");
        }

        return new SshServerHost(_options, _applicationFactory, _loggingConfiguration);
    }
}
