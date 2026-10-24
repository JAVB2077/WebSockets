namespace WebSocketServer.Hubs;

// Una parada del recorrido (la 0 es el registro en el origen)
public record ParadaDto(int Parada, Punto Ubicacion, string Estado, DateTime Fecha);

// Paquete con todas sus paradas, como lo reciben las páginas al conectarse
public record HistorialPaqueteDto(string Id, Punto Origen, Punto Destino, decimal Precio, string TipoPaquete, DateTime Fecha, List<ParadaDto> Paradas);

// Guarda los paquetes en memoria para que un dispositivo que se conecta tarde vea todo.
// Es singleton (uno para todas las conexiones); se pierde al reiniciar servsocket.
public class AlmacenPaquetes
{
    private readonly Lock _candado = new();
    private readonly Dictionary<string, HistorialPaqueteDto> _paquetes = new();

    // Guarda un paquete nuevo con su parada 0 "Registrado" en el origen
    public void Agregar(PaqueteDto paquete)
    {
        var fecha = paquete.Fecha ?? DateTime.UtcNow;
        var registro = new ParadaDto(0, paquete.Origen, "Registrado", fecha);
        lock (_candado)
        {
            _paquetes[paquete.Id!] = new HistorialPaqueteDto(
                paquete.Id!, paquete.Origen, paquete.Destino, paquete.Precio, paquete.TipoPaquete, fecha, [registro]);
        }
    }

    // Agrega la siguiente parada; el número lo pone el servidor para que dos dispositivos no se encimen.
    // Devuelve un error si el paquete no existe o ya fue entregado.
    public (ActualizacionDto? Actualizacion, string? Error) AgregarParada(ActualizacionDto actualizacion)
    {
        lock (_candado)
        {
            if (!_paquetes.TryGetValue(actualizacion.PaqueteId, out var paquete))
                return (null, $"El paquete {actualizacion.PaqueteId} no existe en el servidor");

            var ultima = paquete.Paradas[^1];
            if (ultima.Estado == "Entregado")
                return (null, $"El paquete {actualizacion.PaqueteId} ya fue entregado");

            var nueva = actualizacion with { Parada = ultima.Parada + 1, Fecha = DateTime.UtcNow };
            paquete.Paradas.Add(new ParadaDto(nueva.Parada, nueva.Ubicacion, nueva.Estado, nueva.Fecha!.Value));
            return (nueva, null);
        }
    }

    // Borra todos los paquetes (botón "Reiniciar historial" de estados.html)
    public void Limpiar()
    {
        lock (_candado)
        {
            _paquetes.Clear();
        }
    }

    // Copia de todos los paquetes (copia para no leer la lista mientras otro la modifica)
    public List<HistorialPaqueteDto> Todos()
    {
        lock (_candado)
        {
            return _paquetes.Values
                .OrderBy(p => p.Fecha)
                .Select(p => p with { Paradas = [.. p.Paradas] })
                .ToList();
        }
    }
}
