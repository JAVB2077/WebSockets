// Conexión compartida por index, registro, estados y mapa.
// Requiere cargar signalr.js antes que este archivo.
// URL relativa: en local usa ws:// y en servcasa (Caddy) usa wss:// automáticamente.
// Ojo: los campos llegan en camelCase (origen.lat, tipoPaquete, paqueteId).
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/paquetes", {
    skipNegotiation: true,
    transport: signalR.HttpTransportType.WebSockets
  })
  .withAutomaticReconnect()
  .build();

//#region Estado de la conexión
// Actualiza el indicador #estadoConexion (si la página lo tiene) y avisa a la página
// (escuchar con window.addEventListener("conexion", ...))
function avisarEstado() {
  const conectado = connection.state === signalR.HubConnectionState.Connected;
  const indicador = document.getElementById("estadoConexion");
  if (indicador) {
    indicador.textContent = conectado ? "Conectado" : "Desconectado";
    indicador.classList.toggle("conectado", conectado);
  }
  window.dispatchEvent(new CustomEvent("conexion", { detail: conectado }));
}

// Inicia la conexión; si falla, reintenta cada 3 segundos.
// La promesa se resuelve cuando ya está conectado.
// Registrar los connection.on(...) ANTES de llamarla.
async function iniciarConexion() {
  while (connection.state === signalR.HubConnectionState.Disconnected) {
    try {
      await connection.start();
      console.log("Conectado al hub de paquetes");
    } catch (err) {
      console.error("No se pudo conectar, reintentando en 3 s...", err);
      await new Promise(r => setTimeout(r, 3000));
    }
  }
  avisarEstado();
  await sincronizar();
}

// Pide al servidor todos los paquetes y reemplaza el historial local,
// así un dispositivo que se conecta tarde ve lo que hicieron los demás.
// Avisa a la página con el evento "historial" para que redibuje.
async function sincronizar() {
  try {
    const paquetes = await connection.invoke("ObtenerPaquetes");
    guardarHistorial(Object.fromEntries(paquetes.map(p => [p.id, p])));
    window.dispatchEvent(new CustomEvent("historial"));
  } catch (err) {
    console.error("No se pudo obtener el historial del servidor", err);
  }
}

// Alguien reinició el historial: todas las páginas se vacían
connection.on("HistorialReiniciado", () => {
  guardarHistorial({});
  window.dispatchEvent(new CustomEvent("historial"));
});

connection.onreconnecting(avisarEstado);
connection.onreconnected(() => {
  avisarEstado();
  sincronizar();
});

// Si se cierra del todo (se agotaron los reintentos automáticos), volvemos a empezar
connection.onclose(() => {
  avisarEstado();
  setTimeout(iniciarConexion, 3000);
});
//#endregion

//#region Historial en localStorage
// Copia local del historial del servidor: se muestra al recargar mientras conecta
// y se reemplaza con lo del servidor en cuanto hay conexión (sincronizar).
// Forma: { "PKG-...": { id, origen, destino, precio, tipoPaquete, fecha,
//                       paradas: [{ parada, ubicacion, estado, fecha }] } }
// La parada 0 es el registro en el origen.
const CLAVE_HISTORIAL = "examen.paquetes";

function leerHistorial() {
  try {
    return JSON.parse(localStorage.getItem(CLAVE_HISTORIAL)) ?? {};
  } catch {
    return {};
  }
}

function guardarHistorial(historial) {
  try {
    localStorage.setItem(CLAVE_HISTORIAL, JSON.stringify(historial));
  } catch (err) {
    console.error("No se pudo guardar en localStorage", err);
  }
}

// Guarda un paquete recién registrado (si ya existe no pierde sus paradas)
function guardarPaquete(paquete) {
  const historial = leerHistorial();
  const anterior = historial[paquete.id];
  const paradas = anterior?.paradas ?? [];
  if (!paradas.some(p => p.parada === 0)) {
    paradas.unshift({ parada: 0, ubicacion: paquete.origen, estado: "Registrado", fecha: paquete.fecha });
  }
  historial[paquete.id] = { ...paquete, paradas };
  guardarHistorial(historial);
  return historial;
}

// Guarda una actualización de estado; si llega dos veces la misma parada, se reemplaza
function guardarActualizacion(act) {
  const historial = leerHistorial();
  // Por si llega antes de sincronizar: guardamos lo que viene
  const paquete = historial[act.paqueteId] ??= {
    id: act.paqueteId, origen: null, destino: null, precio: null, tipoPaquete: "", fecha: act.fecha, paradas: []
  };
  paquete.paradas = paquete.paradas.filter(p => p.parada !== act.parada);
  paquete.paradas.push({ parada: act.parada, ubicacion: act.ubicacion, estado: act.estado, fecha: act.fecha });
  paquete.paradas.sort((a, b) => a.parada - b.parada);
  guardarHistorial(historial);
  return historial;
}

// Paquetes ordenados del más antiguo al más nuevo
function listaPaquetes(historial = leerHistorial()) {
  return Object.values(historial).sort((a, b) => new Date(a.fecha) - new Date(b.fecha));
}
//#endregion

//#region Utilidades
// Texto "lat, lng" con 5 decimales
function textoPunto(punto) {
  return punto ? `${punto.lat.toFixed(5)}, ${punto.lng.toFixed(5)}` : "—";
}

// Escapa texto para meterlo en HTML (popups y tooltips de Leaflet)
function escaparHtml(texto) {
  const div = document.createElement("div");
  div.textContent = texto ?? "";
  return div.innerHTML;
}

// Capa de OpenStreetMap para cualquier mapa de Leaflet
function capaOsm() {
  return L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
  });
}
//#endregion
