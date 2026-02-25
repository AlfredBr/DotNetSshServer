namespace AlfredBr.SshServer.Core;

/// <summary>
/// Configuration for the application selection menu.
/// </summary>
public class AppMenuConfiguration
{
    internal List<AppRegistration> Apps { get; } = [];
    internal string? DefaultAppName { get; private set; }
    internal bool ReturnToMenu { get; private set; } = true;
    internal string MenuTitle { get; private set; } = "Select an application:";

    /// <summary>
    /// Register an application in the menu.
    /// </summary>
    /// <typeparam name="TApp">Application type.</typeparam>
    /// <param name="name">Display name in the menu.</param>
    /// <param name="description">Description shown in the menu.</param>
    public AppMenuConfiguration Add<TApp>(string name, string description = "")
        where TApp : SshShellApplication, new()
    {
        Apps.Add(new AppRegistration(name, description, () => new TApp()));
        return this;
    }

    /// <summary>
    /// Register an application with a factory in the menu.
    /// </summary>
    /// <param name="name">Display name in the menu.</param>
    /// <param name="description">Description shown in the menu.</param>
    /// <param name="factory">Factory function to create the app.</param>
    public AppMenuConfiguration Add(string name, string description, Func<SshShellApplication> factory)
    {
        Apps.Add(new AppRegistration(name, description, factory));
        return this;
    }

    /// <summary>
    /// Set the default application for exec commands when no prefix is specified.
    /// </summary>
    /// <param name="name">Name of the registered app to use as default.</param>
    public AppMenuConfiguration SetDefaultForExec(string name)
    {
        DefaultAppName = name;
        return this;
    }

    /// <summary>
    /// Whether to return to the menu when an app exits, or disconnect.
    /// Default: true.
    /// </summary>
    public AppMenuConfiguration ReturnToMenuOnExit(bool returnToMenu)
    {
        ReturnToMenu = returnToMenu;
        return this;
    }

    /// <summary>
    /// Set the menu title.
    /// </summary>
    public AppMenuConfiguration WithTitle(string title)
    {
        MenuTitle = title;
        return this;
    }

    internal record AppRegistration(string Name, string Description, Func<SshShellApplication> Factory);
}
