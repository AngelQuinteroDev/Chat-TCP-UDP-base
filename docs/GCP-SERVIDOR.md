# Montar el servidor de chat en Google Cloud (GCP)

Guía para tener un servidor intermediario en GCP al que se conecten los ejecutables de Unity (arquitectura cliente–servidor) usando TCP y UDP.

---

## 1. Opciones de cómputo en GCP

| Opción | Uso típico | Ventaja | Coste |
|--------|------------|---------|--------|
| **Compute Engine (VM)** | Servidor que escucha TCP/UDP (y opcionalmente HTTP) | Control total, puertos TCP/UDP abiertos | Por hora de VM (e2-micro suele estar en free tier) |
| **Cloud Run** | Solo HTTP/HTTPS (REST, WebSockets sobre HTTP) | Sin gestión de servidor, escalado a cero | Por request y tiempo de CPU |
| **App Engine** | Igual que Cloud Run, más antiguo | Similar | Similar |

Para un chat con **TCP y UDP puros** (como pide la rúbrica), necesitas un **proceso que abra sockets**. Eso implica **Compute Engine** (una máquina virtual) o un contenedor en GKE. La opción más directa es **Compute Engine**.

---

## 2. Pasos generales en GCP

### 2.1 Proyecto

- Ya tienes el proyecto creado en GCP. Anota el **ID del proyecto**.

### 2.2 Crear una VM (Compute Engine)

1. En la consola: **Compute Engine → VM instances → Create instance**.
2. Configuración sugerida para desarrollo/pruebas:
   - **Name**: `chat-server`.
   - **Region**: una cercana a tus usuarios (ej. `us-central1`).
   - **Machine type**: `e2-micro` (1 vCPU, 1 GB RAM; suele entrar en free tier).
   - **Boot disk**: Ubuntu 22.04 LTS.
   - **Firewall**: marcar **Allow HTTP traffic** y **Allow HTTPS traffic** si vas a exponer API REST; para solo TCP/UDP no es obligatorio.

3. Crear la VM. Anota la **IP externa** (ej. `34.x.x.x`).

### 2.3 Reglas de firewall para TCP y UDP

Por defecto, GCP no abre puertos arbitrarios. Hay que crear reglas:

1. **VPC network → Firewall → Create firewall rule**.
2. Regla 1 – TCP (ej. puerto 5555):
   - **Name**: `allow-chat-tcp`.
   - **Direction**: Ingress.
   - **Targets**: All instances (o la etiqueta de tu VM).
   - **Source IP ranges**: `0.0.0.0/0` (cualquier IP; en producción restringe si quieres).
   - **Protocols and ports**: TCP, `5555` (o el puerto que uses).
3. Regla 2 – UDP (ej. puerto 5556):
   - **Name**: `allow-chat-udp`.
   - Mismo criterio; **Protocols and ports**: UDP, `5556`.

Así el servidor en la VM podrá recibir conexiones TCP en 5555 y datagramas UDP en 5556.

---

## 3. Servidor en la VM: opciones de tecnología

El servidor debe:

- Escuchar en un puerto **TCP** (ej. 5555) y en un puerto **UDP** (ej. 5556).
- Gestionar salas (crear sala → código; unirse por código).
- Hacer handshake (primer mensaje con roomCode/protocol).
- Recibir mensajes de un cliente y reenviarlos a los demás de la misma sala (relay).

Puedes implementarlo en:

- **Node.js** (net + dgram) o **Python** (asyncio + sockets) o **C#** (.NET Core con TcpListener y UdpClient). C# tiene la ventaja de reutilizar lógica y formatos similares a Unity.

### 3.1 Ejemplo de diseño (sin código completo)

- **TCP**: un `TcpListener` acepta clientes; por cada cliente, un hilo o tarea que lee mensajes (por ejemplo líneas de texto o JSON por línea). Al recibir un mensaje, parseas `roomCode`/`userId` y reenvías a todos los clientes de esa sala (manteniendo una estructura `Dictionary<string, List<TcpClient>>` por roomCode).
- **UDP**: un `UdpClient` recibe datagramas; cada datagrama incluye en el payload (por ejemplo primer mensaje) el roomCode y un userId; guardas `IPEndPoint` por sala y usuario. Cuando llega un mensaje, reenvías a los demás endpoints de la misma sala.

Formato de mensaje sugerido (ejemplo):

```json
{ "type": "join", "roomCode": "ABC-1234", "userId": "user1" }
{ "type": "message", "text": "Hola", "messageId": "..." }
{ "type": "file", "filename": "doc.pdf", "contentBase64": "..." }
```

El servidor puede generar el `roomCode` al crear sala (endpoint HTTP opcional o comando especial en el primer mensaje TCP/UDP).

### 3.2 Desplegar el servidor en la VM

1. Conectar por SSH a la VM (desde la consola de GCP o `gcloud compute ssh chat-server --zone=...`).
2. Instalar runtime (Node, Python o .NET según tu servidor).
3. Subir el código (git clone, o subir por SCP/SFTP).
4. Ejecutar el servidor (por ejemplo `node server.js` o `python server.py` o `dotnet run`).
5. Para que siga corriendo al cerrar SSH: usar **systemd** o **screen**/ **tmux**:
   - systemd: crear un unit file que ejecute tu binario y hacer `systemctl enable --now chat-server`.

---

## 4. Cómo se conecta Unity al servidor

- **Dirección**: la **IP pública** de la VM (o un dominio apuntando a esa IP).
- **Puertos**: el mismo que abriste en el firewall (ej. TCP 5555, UDP 5556).
- En Unity:
  - Para **TCP**: igual que en tu base, pero en lugar de conectar a la IP del “otro jugador”, conectas a la IP de la VM y el puerto TCP. El primer mensaje que envías debe ser el handshake (join con roomCode).
  - Para **UDP**: igual: `remoteEndPoint` = IP de la VM + puerto UDP; primer datagrama = handshake con roomCode.

No hace falta “API REST” para el chat en tiempo real si usas TCP/UDP; la “API” es tu protocolo de mensajes (JSON por línea o por datagrama). Opcionalmente puedes exponer HTTP en otro puerto (ej. 8080) solo para “crear sala” (POST) y “unirse” (POST) y devolver el roomCode; el chat en sí seguiría por TCP/UDP.

---

## 5. Resumen de pasos

1. Crear VM en Compute Engine (Ubuntu).
2. Anotar IP pública.
3. Crear reglas de firewall: TCP 5555, UDP 5556 (y 80/443 si usas HTTP).
4. Implementar servidor que escuche en 5555 (TCP) y 5556 (UDP), gestione salas y relay.
5. Desplegar en la VM y dejarlo corriendo (systemd/tmux).
6. En Unity: configurar IP y puertos (por defecto o en UI) y conectar a esa IP/puerto con el protocolo elegido (TCP o UDP).

Para documentación del proyecto y README según la rúbrica, ver **README.md** en la raíz del repositorio.
