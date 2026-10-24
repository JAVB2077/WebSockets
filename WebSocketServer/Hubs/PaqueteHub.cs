using Microsoft.AspNetCore.SignalR;

namespace WebSocketServer.Hubs;

// Coordenada en el mapa
public record Punto(double Lat, double Lng);

// Paquete que se registra desde registro.html
public record PaqueteDto(string? Id, Punto Origen, Punto Destino, decimal Precio, string TipoPaquete, DateTime? Fecha);

// Cambio de estado de un paquete en una parada
public record ActualizacionDto(string PaqueteId, int Parada, Punto Ubicacion, string Estado, DateTime? Fecha);

// El hub guarda los paquetes en el almacén (memoria) y reenvía los cambios a todos los clientes
public class PaqueteHub(AlmacenPaquetes almacen) : Hub
{
    // Historial completo para la página que se acaba de conectar
    public List<HistorialPaqueteDto> ObtenerPaquetes() => almacen.Todos();

    // Asigna Id y fecha, lo guarda, avisa a todos y devuelve el paquete al que lo registró
    public async Task<PaqueteDto> RegistrarPaquete(PaqueteDto paquete)
    {
        var registrado = paquete with
        {
            Id = $"PKG-{DateTime.Now:yyMMdd-HHmmss}-{Random.Shared.Next(100, 1000)}",
            Fecha = DateTime.UtcNow
        };

        almacen.Agregar(registrado);
        await Clients.All.SendAsync("PaqueteRegistrado", registrado);
        return registrado;
    }

    // El servidor pone el número de parada y la fecha, lo guarda y lo reenvía a todos
    public async Task ActualizarEstado(ActualizacionDto actualizacion)
    {
        var (nueva, error) = almacen.AgregarParada(actualizacion);
        if (error is not null) throw new HubException(error);

        await Clients.All.SendAsync("EstadoActualizado", nueva);
    }

    // Borra todo el historial del servidor y avisa a todos para que limpien su pantalla
    public async Task ReiniciarHistorial()
    {
        almacen.Limpiar();
        await Clients.All.SendAsync("HistorialReiniciado");
    }
}
