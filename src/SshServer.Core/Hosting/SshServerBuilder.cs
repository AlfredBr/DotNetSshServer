using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlfredBr.SshServer.Core;

/// <summary>
/// Fluent builder for configuring and creating an SSH server host.
/// </summary>
public class SshServerBuilder
{
    private readonly SshServerOptions _options = new();
    private readonly Dictionary<string, Func<SshShellApplication>> _userMappings = new(StringComparer.OrdinalIgnoreCase);
    private Action<ILoggingBuilder>? _loggingConfiguration;
    private Func<SshShellApplication>? _applicationFactory;
    private AppMenuConfiguration? _menuConfiguration;
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
    /// Map a username to a specific application.
    /// When a user connects with this username, they go directly to this app (no menu).
    /// </summary>
    /// <typeparam name="TApp">The application type.</typeparam>
    /// <param name="username">The username to map.</param>
    public SshServerBuilder MapUser<TApp>(string username) where TApp : SshShellApplication, new()
    {
        _userMappings[username] = () => new TApp();
        return this;
    }

    /// <summary>
    /// Map a username to a specific application factory.
    /// When a user connects with this username, they go directly to this app (no menu).
    /// </summary>
    /// <param name="username">The username to map.</param>
    /// <param name="factory">Factory function that creates the application.</param>
    public SshServerBuilder MapUser(string username, Func<SshShellApplication> factory)
    {
        _userMappings[username] = factory;
        return this;
    }

    /// <summary>
    /// Configure an application selection menu for unmapped usernames.
    /// Users who connect with an unmapped username will see a menu to choose an app.
    /// </summary>
    /// <param name="configure">Action to configure the menu.</param>
    public SshServerBuilder UseApplicationMenu(Action<AppMenuConfiguration> configure)
    {
        _menuConfiguration = new AppMenuConfiguration();
        configure(_menuConfiguration);
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

        // Validate - need at least one of: application, user mappings, or menu
        if (_applicationFactory == null && _userMappings.Count == 0 && _menuConfiguration == null)
        {
            throw new InvalidOperationException(
                "No application registered. Call UseApplication<TApp>(), MapUser(), or UseApplicationMenu() before Build().");
        }

        // If menu is configured but no default app, use the menu as the default
        if (_applicationFactory == null && _menuConfiguration != null)
        {
            _applicationFactory = () => new AppLauncherApplication(_menuConfiguration);
        }

        // If only user mappings are configured (no default), we need a fallback
        if (_applicationFactory == null && _userMappings.Count > 0)
        {
            // Use the first mapped app as default
            var firstMapping = _userMappings.First();
            _applicationFactory = firstMapping.Value;
        }

        return new SshServerHost(_options, _applicationFactory!, _userMappings, _menuConfiguration, _loggingConfiguration);
    }
}
