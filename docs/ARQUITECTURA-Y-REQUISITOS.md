# Arquitectura y requisitos del proyecto Chat TCP/UDP

## 1. Resumen de la rúbrica (checklist)

| Requisito | Descripción |
|-----------|-------------|
| **Protocolo** | UI que permita **seleccionar explícitamente** TCP o UDP **antes** de iniciar la conexión. |
| **Dual protocol** | El chat debe funcionar **completamente** con TCP y con UDP. |
| **UI** | Selector de protocolo, visualización clara de mensajes enviados/recibidos, comunicación bidireccional. |
| **Contenido** | Texto, imágenes (visualización en chat), + al menos un tipo extra: .pdf, audio o ejecutable. |
| **Evidencia** | Interfaz debe mostrar claramente llegada de mensajes y recepción de archivos. |
| **Entregables** | Fork del repo base, código + docs .md, video YouTube (TCP, UDP, imagen, otro archivo), capturas .png. |

---

## 2. Tu idea: salas + código de sala + ejecutable

### Flujo propuesto

1. Usuario abre el **ejecutable** de Unity.
2. Pantalla inicial:
   - **Crear sala** → el servidor genera un **código de sala** (ej. `ABC-1234`) y el usuario queda como “host”.
   - **Unirse a sala** → el usuario escribe el código y se conecta a esa sala.
3. Dentro de la sala: elegir **protocolo (TCP o UDP)** y luego conectar; chatear (texto, imágenes, otro tipo de archivo).

Para “crear sala” / “unirse con código” necesitas un **servidor central** (por ejemplo en GCP) que:

- Genere y almacene salas (código → sala).
- Haga de intermediario entre clientes (relay de mensajes por sala).
- Opcionalmente guarde historial de mensajes.

La base actual es **P2P** (un cliente conecta directo a otro que hace de “servidor”). Para salas con código hay que pasar a **arquitectura cliente–servidor** con un servidor en la nube.

---

## 3. Arquitecturas posibles

### 3.1 Mantener la base P2P (sin GCP para el chat)

- Un ejecutable hace de **servidor** (abre puerto), el otro de **cliente** (IP + puerto).
- No hay “código de sala”: el cliente debe conocer la IP del host (o usar red local/broadcast).
- **Ventaja**: cumple rúbrica TCP/UDP sin montar servidor.  
- **Desventaja**: no hay salas con código ni historial centralizado.

### 3.2 Cliente–servidor con servidor en GCP (recomendado para tu idea)

```
[Unity Client A]  ←→  [Servidor en GCP]  ←→  [Unity Client B]
       TCP/UDP              (relay)                 TCP/UDP
```

- El **servidor en GCP**:
  - Gestiona **salas** (crear sala → código, unirse por código).
  - Recibe mensajes de un cliente y los reenvía a los demás de la misma sala (relay).
  - Puede guardar historial en base de datos.
- Los **clientes Unity** se conectan siempre al mismo servidor (IP/hostname de GCP); el protocolo (TCP o UDP) lo eliges en la UI y lo implementas en cliente y servidor.

Para “objeto de polling”, handshake y tiempo real, más abajo se explican las opciones (polling, WebSockets, TCP/UDP).

---

## 4. Requisitos funcionales mapeados a la base actual

| Requisito rúbrica | En la base actual | Qué falta |
|-------------------|-------------------|-----------|
| Selección de protocolo antes de conectar | No existe; cada escena es TCP o UDP | Una sola escena/lobby con **selector TCP/UDP** y luego iniciar servidor o cliente según lo elegido. |
| Mensajes visibles enviados/recibidos | Solo Debug.Log | **UI**: lista o scroll de mensajes (TextMeshPro), diferenciando “enviado” vs “recibido”. |
| Bidireccional | Sí (base ya lo hace) | Mantener y asegurar que se vea en UI. |
| Texto | Sí | Mejorar presentación en UI. |
| Imágenes en el chat | Hay Video (UDP); no “imagen como archivo en chat” | Añadir **envío de imagen como archivo** (ej. JPG/PNG) y mostrarla en la lista de mensajes. |
| Otro tipo de archivo (.pdf, audio, ejecutable) | No | Implementar envío/recepción y **indicador en UI** (icono + nombre; para ejecutable solo descarga, no ejecutar por seguridad). |

Siguiente documento: **COMUNICACION-TIEMPO-REAL.md** (polling, handshake, WebSockets, TCP/UDP).
