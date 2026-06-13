using Clipwell.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Clipwell MCP server (stdio). Exposes clipboard history to MCP clients such as
// Claude Desktop / Claude Code, which spawn this process and talk JSON-RPC over
// stdin/stdout. Tools proxy to the running daemon's REST API — this process holds
// no state of its own.
var builder = Host.CreateApplicationBuilder(args);

// stdio transport owns stdout, so all logging MUST go to stderr or it corrupts
// the JSON-RPC stream.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<DaemonClient>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
