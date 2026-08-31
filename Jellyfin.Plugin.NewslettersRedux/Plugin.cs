using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.NewslettersRedux.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.NewslettersRedux;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        SetConfigPaths(applicationPaths);
        return;

        void SetConfigPaths(IApplicationPaths dataPaths)
        {
            // custom code
            // IApplication Paths
            var config = Instance!.Configuration;
            config.DataPath = dataPaths.DataPath;
            config.TempDirectory = dataPaths.TempDirectory;
            config.PluginsPath = dataPaths.PluginsPath;
            config.ProgramDataPath = dataPaths.ProgramDataPath;
            config.ProgramSystemPath = dataPaths.ProgramSystemPath;
            config.SystemConfigurationFilePath = dataPaths.SystemConfigurationFilePath;
            config.LogDirectoryPath = dataPaths.LogDirectoryPath;

            // Custom Paths
            config.NewsletterDir = $"{config.TempDirectory}/NewslettersRedux/";
        }
    }

    /// <inheritdoc />
    public override string Name => "NewslettersRedux";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("47f7a2a6-0569-40ca-9548-0d4d1bd986d8");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
            
        ];
    }
}
