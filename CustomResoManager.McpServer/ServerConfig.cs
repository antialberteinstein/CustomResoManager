using System.Net;
using System.Net.Sockets;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CustomResoManager.McpServer;

/// <summary>
/// Whole-app configuration loaded from <c>config.yaml</c> next to the executable.
///
/// This module (the WPF app) plays two network roles, so the file has two sections:
/// <list type="bullet">
///   <item><c>mcpServer</c> — where THIS app exposes its MCP tool server (the agent connects here).</item>
///   <item><c>agent</c> — where the Python chat backend runs (this app sends chat/health requests here).</item>
/// </list>
/// Missing file or parse error -> safe localhost defaults, never throws.
/// </summary>
public sealed class AppConfig
{
    public ServerConfig McpServer { get; set; } = new();
    public AgentConfig Agent { get; set; } = new();

    public static AppConfig Load(string baseDirectory)
    {
        foreach (var dir in new[] { baseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var file = System.IO.Path.Combine(dir, "config.yaml");
            if (!File.Exists(file)) continue;

            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                var cfg = deserializer.Deserialize<AppConfig>(File.ReadAllText(file));
                if (cfg is not null)
                {
                    cfg.McpServer ??= new ServerConfig();
                    cfg.Agent ??= new AgentConfig();
                    cfg.McpServer.Normalize();
                    cfg.Agent.Normalize();
                    return cfg;
                }
            }
            catch
            {
                // Bad YAML shouldn't take the app down — fall through to defaults.
            }
        }

        return new AppConfig();
    }
}

/// <summary>
/// Bind config for the MCP tool server. Lets it listen on the LAN instead of localhost so an
/// agent (or any MCP client) on another machine can reach it, without recompiling.
/// </summary>
public sealed class ServerConfig
{
    /// <summary>Requested host. Special values: <c>auto</c>/<c>lan</c>, <c>0.0.0.0</c>/<c>*</c>/<c>+</c>, <c>localhost</c>.</summary>
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 7777;

    /// <summary>Path the MCP endpoint is mapped at (must start with '/').</summary>
    public string Path { get; set; } = "/mcp";

    private static readonly string[] WildcardHosts = { "0.0.0.0", "*", "+", "any", "auto", "lan", "" };

    private bool IsWildcard =>
        WildcardHosts.Contains((Host ?? "").Trim().ToLowerInvariant());

    private bool IsLoopback
    {
        get
        {
            var h = (Host ?? "").Trim().ToLowerInvariant();
            return h == "localhost" || h == "127.0.0.1" || h == "::1";
        }
    }

    private string NormalizedPath
    {
        get
        {
            var p = string.IsNullOrWhiteSpace(Path) ? "/mcp" : Path.Trim();
            if (!p.StartsWith('/')) p = "/" + p;
            return p.TrimEnd('/');
        }
    }

    /// <summary>What Kestrel actually binds to. Wildcard/auto hosts bind all interfaces (0.0.0.0).</summary>
    public string BindUrl => $"http://{(IsWildcard ? "0.0.0.0" : Host.Trim())}:{Port}";

    /// <summary>The MCP path without a trailing slash, e.g. <c>/mcp</c>. Pass this to MapMcp.</summary>
    public string McpPath => NormalizedPath;

    /// <summary>
    /// A reachable URL to advertise/log. For wildcard/auto hosts this resolves to the machine's
    /// primary LAN IPv4 so you can tell remote clients where to connect.
    /// </summary>
    public string DisplayUrl
    {
        get
        {
            string host;
            if (IsLoopback) host = "127.0.0.1";
            else if (IsWildcard) host = DetectLanIPv4() ?? "127.0.0.1";
            else host = Host.Trim();
            return $"http://{host}:{Port}{NormalizedPath}/";
        }
    }

    internal void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Host)) Host = "127.0.0.1";
        if (Port <= 0) Port = 7777;
        if (string.IsNullOrWhiteSpace(Path)) Path = "/mcp";
    }

    /// <summary>
    /// Best-effort primary LAN IPv4. Uses a "connected" UDP socket (no packets sent) to learn
    /// which local address the OS would route through, then falls back to the first non-loopback
    /// IPv4 from DNS.
    /// </summary>
    private static string? DetectLanIPv4()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address))
                return ep.Address.ToString();
        }
        catch
        {
            // ignore and try DNS fallback
        }

        try
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                ?.ToString();
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Where the Python chat agent lives. The WPF app's AI Assistant panel POSTs to <see cref="ChatUrl"/>
/// and pings <see cref="HealthUrl"/>. Set <see cref="Host"/> to the agent machine's LAN IP if it runs
/// on a different box.
/// </summary>
public sealed class AgentConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8000;

    private string SafeHost => string.IsNullOrWhiteSpace(Host) ? "127.0.0.1" : Host.Trim();

    public string BaseUrl => $"http://{SafeHost}:{Port}";
    public string ChatUrl => $"{BaseUrl}/chat";
    public string HealthUrl => $"{BaseUrl}/health";

    internal void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Host)) Host = "127.0.0.1";
        if (Port <= 0) Port = 8000;
    }
}
