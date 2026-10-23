using Microsoft.AspNetCore.SignalR;

namespace WebSocketServer.Hubs;

// Coordenada en el mapa
public record Punto(double Lat, double Lng);

// Paquete que se registra desde registro.html
public record PaqueteDto(string? Id, Punto Origen, Punto Destino, decimal Precio, string TipoPaquete, DateTime? Fecha);

// Cambio de estado de un paquete en una parada
public record ActualizacionDto(string PaqueteId, int Parada, Punto Ubicacion, string Estado, DateTime? Fecha);

// El hub no guarda nada; solo reenvía los mensajes a todos los clientes
public class PaqueteHub : Hub
{
    // Asigna Id y fecha, avisa a todos y devuelve el paquete al que lo registró
    public async Task<PaqueteDto> RegistrarPaquete(PaqueteDto paquete)
    {
        var registrado = paquete with
        {
            Id = $"PKG-{DateTime.Now:yyMMdd-HHmmss}-{Random.Shared.Next(100, 1000)}",
            Fecha = DateTime.UtcNow
        };

        await Clients.All.SendAsync("PaqueteRegistrado", registrado);
        return registrado;
    }

    // Pone la fecha del servidor y reenvía la actualización a todos
    public async Task ActualizarEstado(ActualizacionDto actualizacion)
    {
        var conFecha = actualizacion with { Fecha = DateTime.UtcNow };
        await Clients.All.SendAsync("EstadoActualizado", conFecha);
    }
}
