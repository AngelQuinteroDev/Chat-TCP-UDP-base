# Chat TCP/UDP - Aplicación de chat multiplataforma

[![Unity](https://img.shields.io/badge/Unity-2022+-black?style=flat-square&logo=unity)](https://unity.com)
[![TCP](https://img.shields.io/badge/Protocol-TCP-blue?style=flat-square)](https://es.wikipedia.org/wiki/Protocolo_de_control_de_transmisi%C3%B3n)
[![UDP](https://img.shields.io/badge/Protocol-UDP-green?style=flat-square)](https://es.wikipedia.org/wiki/Protocolo_de_datagramas_de_usuario)

Aplicación de chat desarrollada a partir del [fork de la base Chat-TCP-UDP](https://github.com/memin2522/Chat-TCP-UDP-base). Permite comunicación bidireccional mediante **TCP** o **UDP**, con soporte para mensajes de texto, imágenes y otros tipos de archivo.

---

## Tabla de contenidos

- [Acerca del proyecto](#acerca-del-proyecto)
- [Requisitos funcionales (rúbrica)](#requisitos-funcionales-rúbrica)
- [Construido con](#construido-con)
- [Empezar](#empezar)
  - [Prerrequisitos](#prerrequisitos)
  - [Instalación](#instalación)
  - [Uso](#uso)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Documentación adicional](#documentación-adicional)
- [Entregables y criterios de evaluación](#entregables-y-criterios-de-evaluación)
- [Roadmap](#roadmap)
- [Contacto](#contacto)
- [Agradecimientos](#agradecimientos)

---

## Acerca del proyecto

Este proyecto extiende la base de chat TCP/UDP para cumplir con la actividad de Servicios Multimedia (Primer Corte). La aplicación permite:

- **Seleccionar el protocolo** (TCP o UDP) **antes** de establecer la conexión.
- Comunicación **bidireccional** entre dos usuarios.
- Envío y recepción de:
  - **Mensajes de texto**
  - **Imágenes** (con visualización dentro del chat)
  - **Al menos un tipo adicional de archivo** (.pdf, audio o ejecutable)

La interfaz muestra de forma clara los mensajes enviados y recibidos, así como la recepción de archivos.

*(Incluir aquí capturas de pantalla en formato .png de las interfaces, por ejemplo en una carpeta `images/`.)*

<!-- Ejemplo:
![Pantalla principal](images/main.png)
![Chat TCP](images/chat-tcp.png)
![Chat UDP](images/chat-udp.png)
-->

---

## Requisitos funcionales (rúbrica)

| Requisito | Estado |
|-----------|--------|
| Selección explícita de protocolo (TCP/UDP) antes de conectar | Pendiente / Implementado |
| Funcionamiento correcto con TCP | Pendiente / Implementado |
| Funcionamiento correcto con UDP | Pendiente / Implementado |
| UI: visualización clara de mensajes enviados y recibidos | Pendiente / Implementado |
| Comunicación bidireccional | Pendiente / Implementado |
| Envío y recepción de texto | Pendiente / Implementado |
| Envío y recepción de imágenes (visualización en chat) | Pendiente / Implementado |
| Envío y recepción de al menos un tipo extra (.pdf, audio o ejecutable) | Pendiente / Implementado |
| Evidencia en interfaz de llegada de mensajes y recepción de archivos | Pendiente / Implementado |

---

## Construido con

- [Unity](https://unity.com) (2022 o superior)
- [TextMesh Pro](https://unity.com/unity/features/2d/fonts-and-text/textmesh-pro) (texto en UI)
- C# / .NET (sockets TCP y UDP)
- *(Opcional: servidor en GCP para arquitectura cliente–servidor con salas)*

---

## Empezar

### Prerrequisitos

- Unity Hub con Unity 2022 LTS o superior.
- *(Si usas servidor en GCP: IP y puertos del servidor configurados en el cliente.)*

### Instalación

1. Clonar el repositorio (debe ser **fork** de la base entregada):
   ```bash
   git clone https://github.com/TU_USUARIO/Chat-TCP-UDP.git
   cd Chat-TCP-UDP
   ```
2. Abrir el proyecto con Unity Hub (Add → seleccionar esta carpeta).
3. Abrir la escena correspondiente:
   - **TCP**: `Assets/Chat_TCP_UDP/Scenes/TCP/Tcp_Server.unity` (servidor) y `Tcp_Client.unity` (cliente).
   - **UDP**: `Assets/Chat_TCP_UDP/Scenes/UDP/Udp_Server.unity` y `Udp_Client.unity`.

### Uso

- **Modo servidor**: ejecutar la escena de servidor (TCP o UDP), pulsar el botón para iniciar servidor y anotar la IP y puerto (ej. mostrados en UI o consola).
- **Modo cliente**: ejecutar la escena de cliente, introducir la IP y puerto del servidor (o del otro ejecutable si es P2P) y conectar.
- *(Cuando esté implementado: seleccionar primero TCP o UDP en la UI y luego crear sala / unirse con código.)*

---

## Estructura del proyecto

```
Assets/
  Chat_TCP_UDP/
    Scenes/          # Escenas TCP, UDP y Video
    Scripts/         # Lógica de red e interfaces
      Interface/     # IChatConnection, IServer, IClient
      TCP/           # TCPServer, TCPClient, UI
      UDP/           # UDPServer, UDPClient, UI
      VIdeo/         # Video por UDP (sender/receiver)
docs/                # Documentación en Markdown
  ARQUITECTURA-Y-REQUISITOS.md
  COMUNICACION-TIEMPO-REAL.md   # Polling, handshake, WebSockets, TCP/UDP
  HISTORIAL-Y-PERSISTENCIA.md   # Cómo guardar mensajes e historial
  GCP-SERVIDOR.md               # Montar servidor en GCP
```

---

## Documentación adicional

Toda la documentación en formato Markdown está en la carpeta `docs/`:

| Documento | Contenido |
|-----------|-----------|
| [ARQUITECTURA-Y-REQUISITOS.md](docs/ARQUITECTURA-Y-REQUISITOS.md) | Rúbrica, idea de salas con código, arquitecturas (P2P vs cliente–servidor). |
| [COMUNICACION-TIEMPO-REAL.md](docs/COMUNICACION-TIEMPO-REAL.md) | Handshake, polling, long polling, WebSockets, y cómo encajan con TCP/UDP. |
| [HISTORIAL-Y-PERSISTENCIA.md](docs/HISTORIAL-Y-PERSISTENCIA.md) | Dónde y cómo guardar mensajes e historial (cliente, servidor, DB). |
| [GCP-SERVIDOR.md](docs/GCP-SERVIDOR.md) | Pasos para montar el servidor intermediario en Google Cloud (VM, firewall, TCP/UDP). |

---

## Entregables y criterios de evaluación

- **Repositorio en GitHub**: fork de la base, con código fuente y documentación en `.md`.
- **Video en YouTube** mostrando:
  - Funcionamiento con TCP.
  - Funcionamiento con UDP.
  - Envío de imagen.
  - Envío de otro tipo de archivo (.pdf, audio o ejecutable).
- **Imágenes de las interfaces** en formato `.png` (incluir en el repo o en la documentación).

*(Enlace al video y a las capturas se añadirán aquí al entregar.)*

---

## Roadmap

- [ ] UI única con selector TCP/UDP antes de conectar.
- [ ] Visualización en chat de mensajes enviados/recibidos (lista/scroll).
- [ ] Envío y visualización de imágenes en el chat.
- [ ] Envío y recepción de al menos un tipo extra (.pdf, audio o ejecutable).
- [ ] *(Opcional)* Servidor en GCP con salas y código de sala.
- [ ] *(Opcional)* Persistencia de historial (servidor + DB).

---

## Contacto

**Tu nombre / equipo** – [@tu_twitter](https://twitter.com) – tu_email@ejemplo.com  

Link del proyecto: [https://github.com/TU_USUARIO/Chat-TCP-UDP](https://github.com/TU_USUARIO/Chat-TCP-UDP)

---

## Agradecimientos

- [Chat-TCP-UDP-base](https://github.com/memin2522/Chat-TCP-UDP-base) – Base del proyecto.
- [Best-README-Template](https://github.com/othneildrew/Best-README-Template) – Plantilla de README.
- Unity Documentation – TextMesh Pro y Networking.
