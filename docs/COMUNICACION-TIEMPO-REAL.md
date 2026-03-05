# Comunicación en tiempo real: Polling, Handshake, WebSockets, TCP y UDP

Esta guía explica cómo encajan **object polling**, **handshake** y otras técnicas con TCP, UDP y WebSockets cuando el cliente Unity se conecta a un servidor intermediario (por ejemplo en GCP).

---

## 1. Handshake (apretón de manos)

### ¿Qué es?

Es el **intercambio inicial** de mensajes para acordar que la conexión está viva y, si aplica, parámetros (protocolo, sala, autenticación).

### En tu base actual

- **TCP**: no hay handshake explícito de aplicación; TCP ya hace su propio handshake a nivel de red (SYN, SYN-ACK, ACK). A nivel aplicación, el cliente se conecta y empieza a enviar/recibir.
- **UDP (chat)**: el cliente envía `"CONNECT"`, el servidor responde `"CONNECTED"` y guarda el `remoteEndPoint` para saber a quién enviar mensajes. Eso **es** un handshake de aplicación.
- **UDP (video)**: el cliente envía `"Hi"`, el servidor recibe y guarda el endpoint. Handshake mínimo para “registrar” al receptor.

### En un servidor central (GCP) con salas

Ejemplo de handshake al unirse a una sala:

```
Cliente → Servidor:  { "type": "join", "roomCode": "ABC-1234", "protocol": "tcp" }
Servidor → Cliente:  { "type": "joined", "roomId": "...", "userId": "..." }
```

Así el servidor sabe: sala, usuario y (si lo envías) protocolo. Útil para logging y para decidir a quién reenviar mensajes.

---

## 2. Polling (consulta periódica)

### ¿Qué es?

El cliente **pregunta cada X segundos** al servidor: “¿hay mensajes nuevos para mí?”. El servidor responde con la lista de mensajes pendientes (o vacía).

### Flujo típico

```
Cliente                    Servidor
   |                           |
   |  GET /api/messages?since=...  |
   | -------------------------->  |
   |   [ { id, from, text }, ... ] |
   | <--------------------------  |
   |                           |
   |  (espera 2 s)              |
   |  GET /api/messages?since=...  |
   | -------------------------->  |
   |   [ ]                       |
   | <--------------------------  |
```

### Ventajas y desventajas

| Ventaja | Desventaja |
|---------|------------|
| Muy fácil de implementar (HTTP + REST) | Más latencia (depende del intervalo). |
| Funciona detrás de firewalls/proxies | Más peticiones y carga en el servidor. |
| No requiere conexión persistente | No es “tiempo real” estricto. |

### “Object polling” en este contexto

Suele referirse a **polling de recursos/objetos**: el cliente consulta periódicamente un endpoint que devuelve el “estado” (mensajes nuevos, lista de usuarios en la sala, etc.). Cada petición es una “consulta al objeto estado” en el servidor.

Ejemplo conceptual en C# (Unity con HTTP):

```csharp
// Cada 1-2 segundos
IEnumerator PollMessages()
{
    while (isConnected)
    {
        var request = UnityWebRequest.Get($"{serverUrl}/rooms/{roomId}/messages?since={lastMessageId}");
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            var newMessages = JsonUtility.FromJson<MessageList>(request.downloadHandler.text);
            foreach (var msg in newMessages.items)
                OnMessageReceived?.Invoke(msg.text);
        }
        yield return new WaitForSeconds(1.5f);
    }
}
```

Para un chat con sensación de tiempo real, suele usarse **long polling** o **WebSockets** en lugar de polling corto cada 1–2 s.

---

## 3. Long polling

El cliente hace una petición HTTP y el **servidor la mantiene abierta** hasta que tenga datos (mensajes nuevos) o hasta un timeout; entonces responde. El cliente recibe la respuesta y vuelve a abrir otra petición.

- Menos peticiones que polling corto.
- Implementación en servidor más compleja (colas por cliente, timeouts).
- Sigue siendo HTTP; algunos proxies pueden cerrar conexiones largas.

---

## 4. WebSockets

### ¿Qué es?

Conexión **full-duplex y persistente** sobre un único socket. Primero se hace un handshake HTTP especial (“Upgrade: websocket”); después el canal queda abierto y ambos pueden enviar frames cuando quieran.

### Flujo resumido

1. Cliente: `GET /chat` con headers `Upgrade: websocket`, `Connection: Upgrade`, clave.
2. Servidor: responde `101 Switching Protocols` y acepta el WebSocket.
3. A partir de ahí: frames de texto o binario (mensajes de chat, imágenes en base64 o binario, etc.).

### Comparado con TCP/UDP

| Aspecto | TCP | UDP | WebSocket |
|--------|-----|-----|-----------|
| Capa | Transporte | Transporte | Aplicación (sobre TCP) |
| Orden y fiabilidad | Sí | No | Sí (usa TCP) |
| Persistencia | Sí (conexión larga) | No (datagramas) | Sí |
| En navegador | No expuesto directo | No | Sí (API nativa) |
| En Unity | Sí (TcpClient) | Sí (UdpClient) | Librerías de terceros o WebSocket sobre TCP propio |

Para **Unity como cliente** hacia un servidor en GCP:

- Si el servidor ofrece **REST + polling**: Unity usa `UnityWebRequest` (HTTP) y polling.
- Si el servidor ofrece **WebSockets**: Unity usa una librería WebSocket (ej. NativeWebSocket, WebSocketSharp) y se conecta a `wss://tu-servidor.com/chat`.
- Si quieres **TCP o UDP puro** (como pide la rúbrica), el servidor en GCP debe abrir **puertos TCP y/o UDP** y hablar el mismo protocolo que el cliente Unity (no HTTP en ese canal).

La rúbrica pide que el chat funcione con **TCP** y con **UDP**; WebSockets es un extra opcional para una versión web o para comparar patrones.

---

## 5. Cómo encaja todo en tu arquitectura

### Opción A: Solo TCP y UDP (cumple rúbrica, sin HTTP)

- **Servidor en GCP**: proceso que escucha en dos puertos (ej. 5555 TCP y 5556 UDP).
- **Handshake de aplicación** (igual que en tu base):
  - TCP: tras `Accept`, primer mensaje puede ser `{"type":"join","roomCode":"ABC-1234"}`; servidor responde `{"type":"joined"}`.
  - UDP: cliente envía mismo JSON; servidor responde y guarda `IPEndPoint` para ese usuario/sala.
- **Sin polling**: mensajes en **tiempo real** por el mismo socket (TCP) o por datagramas (UDP). El servidor, al recibir un mensaje, lo reenvía a los demás clientes de la sala.

### Opción B: Servidor híbrido (HTTP para salas + TCP/UDP para chat)

- **HTTP** (REST):
  - `POST /rooms` → crear sala, devolver código.
  - `POST /rooms/{code}/join` → unirse, devolver token o sessionId.
- **TCP/UDP** (mismo proceso o otro puerto):
  - Cliente conecta con su token o roomCode en el primer mensaje (handshake).
  - Servidor asocia la conexión a una sala y hace relay de mensajes.
- Aquí **no** hace falta polling para el chat; el “objeto” que se actualiza es el **estado en el servidor** (mensajes por sala), y el cliente lo recibe por eventos de lectura (TCP) o recepción (UDP), no consultando cada X segundos.

### Opción C: API REST + polling (sin TCP/UDP para mensajes)

- Crear sala y unirse por HTTP.
- Envío de mensajes: `POST /rooms/{id}/messages` con cuerpo `{ "text": "..." }`.
- Recepción: **polling** a `GET /rooms/{id}/messages?since=...`.
- Cumple “servidor intermediario” y “salas con código”, pero **no** cumple “chat con TCP y UDP”. Para la rúbrica necesitas al menos Opción A (o B con TCP y UDP para el chat).

---

## 6. Resumen práctico

| Objetivo | Qué usar |
|----------|----------|
| Cumplir rúbrica (TCP + UDP) | Servidor en GCP que escuche TCP y UDP; handshake de aplicación (join sala); relay de mensajes por el mismo socket/datagrama. |
| Salas con código | Servidor central que gestione salas; handshake incluye `roomCode` (y opcionalmente `protocol`). |
| “Object polling” | Si usas HTTP para mensajes: cliente consulta periódicamente un endpoint de mensajes; el “objeto” es el recurso “mensajes de la sala”. |
| Handshake | Siempre: primer mensaje (o primeros bytes) con tipo de operación (join, leave) y datos de sala/usuario; servidor responde OK o error. |
| WebSockets | Opcional; útil si añades cliente web o quieres un solo canal persistente; en Unity requiere librería; no sustituye la obligación de TCP y UDP para la rúbrica. |

En el siguiente documento se trata **persistencia e historial de chat** (**HISTORIAL-Y-PERSISTENCIA.md**).
