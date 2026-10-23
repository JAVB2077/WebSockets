using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using WebSocketServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

// SignalR para los WebSockets
builder.Services.AddSignalR();

var app = builder.Build();

// Caddy hace de proxy inverso con HTTPS; respetamos sus cabeceras
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Carpeta del cliente: se toma de "ClientPath" o de ../WebSocketClient
var rutaCliente = builder.Configuration["ClientPath"]
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "WebSocketClient"));
var archivosCliente = new PhysicalFileProvider(rutaCliente);

// "/" abre index.html y se sirven los archivos del cliente
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = archivosCliente });
app.UseStaticFiles(new StaticFileOptions { FileProvider = archivosCliente });

// Hub de paquetes (WebSocket)
app.MapHub<PaqueteHub>("/hubs/paquetes");

app.Run();
