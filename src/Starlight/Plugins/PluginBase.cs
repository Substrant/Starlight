﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Starlight.Bootstrap;
using Starlight.Launch;

namespace Starlight.Plugins;

/// <summary>
///     A base class for Starlight plugins.
/// </summary>
public abstract class PluginBase
{
    /// <summary>
    ///     The software development kit for this plugin.
    /// </summary>
    public readonly PluginSdk Sdk;

    private bool _enabled = true;

    protected PluginBase()
    {
        Sdk = new PluginSdk(Assembly.GetCallingAssembly());
    }

    /// <summary>
    ///     This plugin's name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    ///     The author or organization that wrote this plugin.
    /// </summary>
    public abstract string Author { get; }

    /// <summary>
    ///     The description of this plugin.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    ///     Gets or sets whether this plugin is enabled.<br />
    ///     If this is set to false, the plugin will be unloaded, and vice versa.
    /// </summary>
#pragma warning disable CS0618
    public bool Enabled
    {
        get => _enabled;
        internal set
        {
            _enabled = value;
            if (_enabled)
            {
                PluginArbiter.Plugins.Add(this);
                Load();
            }
            else
            {
                Unload();
                PluginArbiter.Plugins.Remove(this);
            }
        }
    }
#pragma warning restore CS0618

    /// <summary>
    ///     Overload the configuration of this plugin.
    /// </summary>
    public void OverloadConfig(IReadOnlyDictionary<string, dynamic> obj)
    {
        Sdk.MergeConfig(JObject.FromObject(obj));
    }

    /// <summary>
    ///     The method that is called when this plugin is loaded.
    /// </summary>
    [Obsolete("This is not intended for external use. Use PluginBase.Loaded instead.", false)]
    public virtual void Load()
    {
    }

    /// <summary>
    ///     The method that is called when this plugin is unloaded.
    /// </summary>
    [Obsolete("This is not intended for external use. Use PluginBase.Loaded instead.", false)]
    public virtual void Unload()
    {
    }

    /// <summary>
    ///     The method that is called before Roblox launches.
    /// </summary>
    /// <param name="info">The parameters used to launch Roblox.</param>
    /// <param name="client">The client to launch.</param>
    /// <param name="token">The cancellation token used to cancel the task.</param>
    /// <exception cref="TaskCanceledException" />
    public virtual Task<Client> PreLaunch(Client client, LaunchParams info, CancellationToken token = default)
    {
        return Task.FromResult<Client>(null);
    }

    /// <summary>
    ///     The method that is called after Roblox launches.
    /// </summary>
    /// <param name="inst">The running instance of Roblox.</param>
    /// <param name="token">The cancellation token used to cancel the task.</param>
    /// <exception cref="TaskCanceledException" />
    public virtual Task PostLaunch(ClientInstance inst, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     The method that is called when Roblox's window opens.
    /// </summary>
    /// <param name="hwnd">Roblox's main window handle.</param>
    /// <param name="token">The cancellation token used to cancel the task.</param>
    /// <exception cref="TaskCanceledException" />
    public virtual Task PostWindow(IntPtr hwnd, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }
}