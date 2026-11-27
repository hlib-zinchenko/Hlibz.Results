using System.IO.Pipelines;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ModelContextProtocol.Client;

namespace Hlibz.Results.Mcp.Tests;

/// <summary>An MCP server with <see cref="CityTools"/>, in memory, and a client connected to it.</summary>
internal sealed class McpTestServer : IAsyncDisposable
{
    private readonly IHost _host;

    private McpTestServer(IHost host, McpClient client)
    {
        _host = host;
        Client = client;
    }

    public static Action<ToolErrorOptions> Guidance { get; } = errors => errors
        .Guide<CityNotFound>("No city has id {cityId}. Call list_cities to find its id.")
        .Guide<CityInUse>("{userCount} users live in city {cityId}. Ask the user whether to move them first.")
        .Guide<CityClosed>("The city is closed. Tell the user; retrying won't help.")
        .Guide<ValidationFailed>("Fix these arguments and call the tool again: {errors}");

    public McpClient Client { get; }

    public static IHost Build(Action<IMcpServerBuilder> configure, Pipe? toServer = null, Pipe? toClient = null)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        IMcpServerBuilder mcp = builder.Services
            .AddMcpServer()
            .WithStreamServerTransport((toServer ?? new()).Reader.AsStream(), (toClient ?? new()).Writer.AsStream());
        configure(mcp);
        return builder.Build();
    }

    public static async Task<McpTestServer> StartAsync(Action<IMcpServerBuilder>? configure = null)
    {
        Pipe toServer = new();
        Pipe toClient = new();
        IHost host = Build(
            configure ?? (mcp => mcp.WithToolsFromAssembly(typeof(CityTools).Assembly).WithToolErrors(Guidance)),
            toServer,
            toClient);
        await host.StartAsync(TestContext.Current.CancellationToken);
        McpClient client = await McpClient.CreateAsync(
            new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()),
            cancellationToken: TestContext.Current.CancellationToken);
        return new McpTestServer(host, client);
    }

    public async Task<JsonObject?> MetaOfAsync(string tool)
    {
        IList<McpClientTool> tools = await Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        return tools.Single(candidate => candidate.ProtocolTool.Name == tool).ProtocolTool.Meta;
    }

    public Task<CallToolResult> CallAsync(string tool, Dictionary<string, object?>? arguments = null) =>
        Client.CallToolAsync(tool, arguments ?? [], cancellationToken: TestContext.Current.CancellationToken).AsTask();

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _host.StopAsync(TestContext.Current.CancellationToken);
        _host.Dispose();
    }
}
