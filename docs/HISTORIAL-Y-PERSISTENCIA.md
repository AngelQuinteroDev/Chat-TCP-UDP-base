# Cómo guardar mensajes e historial de chat

En aplicaciones de chat, el historial se puede guardar en el **cliente**, en el **servidor** o en **ambos**. Aquí se resumen opciones y cuándo usarlas.

---

## 1. Dónde guardar los mensajes

| Lugar | Qué se guarda | Ventaja | Limitación |
|-------|----------------|---------|------------|
| **Solo en memoria (servidor)** | Mensajes mientras la sala está activa | Simple, sin base de datos | Se pierde al reiniciar o al cerrar la sala |
| **Servidor + base de datos** | Todos los mensajes por sala (y opcionalmente por usuario) | Historial persistente, recuperable al reabrir sala | Necesitas DB en GCP (Firestore, Cloud SQL, etc.) |
| **Solo en el cliente (Unity)** | Mensajes que ese cliente ha visto/recibido | Funciona sin servidor persistente; útil para P2P | No hay historial compartido; si cambias de dispositivo, no hay historial |
| **Servidor + cliente** | Servidor como fuente de verdad; cliente cachea lo que ve | Reconexión: el cliente pide “mensajes desde último id” y rellena la UI | Diseño un poco más complejo |

---

## 2. Opciones según tu arquitectura

### 2.1 P2P (base actual, sin servidor central)

- **En memoria en el cliente**: cada Unity guarda en una lista los mensajes que envía y recibe durante la sesión; al cerrar, se pierde.
- **Persistencia local en Unity**:
  - `PlayerPrefs`: solo para pocos datos (ej. último mensaje); no recomendable para muchos mensajes.
  - **Archivo en disco** (Application.persistentDataPath): al recibir/enviar, append a un JSON o texto; al abrir la app, cargas ese archivo y muestras las últimas N líneas. Es “historial local” de ese dispositivo.

Ejemplo conceptual (guardar en archivo local):

```csharp
// Al recibir o enviar un mensaje
void AppendToHistory(string sender, string text, bool isMine)
{
    var entry = new ChatEntry { sender = sender, text = text, isMine = isMine, time = DateTime.UtcNow };
    string path = Path.Combine(Application.persistentDataPath, "chat_history.json");
    // Leer lista existente, añadir entry, escribir de nuevo (o usar append con formato línea a línea)
    File.AppendAllText(path, JsonUtility.ToJson(entry) + "\n");
}
```

---

### 2.2 Cliente–servidor con GCP (salas con código)

- **Servidor en memoria**: por cada sala, una lista `List<Message>`. Cuando llega un mensaje, se añade y se reenvía a los demás. Al cerrar la sala o reiniciar el servidor, se pierde.
- **Servidor + base de datos** (recomendado si quieres historial “real”):
  - Al recibir un mensaje, el servidor:
    1. Lo guarda en la DB (Firestore, Cloud SQL, etc.) con `roomId`, `userId`, `text`, `timestamp`, tipo (texto/imagen/archivo), referencia al archivo si aplica.
    2. Hace relay a los clientes conectados de esa sala.
  - Cuando un cliente se une a una sala (o reconecta), puede pedir “últimos N mensajes” o “mensajes desde timestamp X” y el servidor los lee de la DB y los envía. Así el historial se “recupera” al entrar.

Modelo de datos mínimo (ejemplo):

```
Room: { id, code, createdAt }
Message: { id, roomId, senderId, senderName, type: "text"|"image"|"file", content (texto o URL/id de archivo), timestamp }
File: { id, roomId, messageId, filename, contentType, storagePath }  // si guardas archivos en Cloud Storage
```

---

## 3. Flujo típico con historial en servidor

1. **Usuario crea sala** → Servidor crea `Room`, devuelve código.
2. **Usuario se une** → Servidor registra cliente en la sala.
3. **Al unirse (o reconectar)**:
   - Cliente envía: “dame mensajes de esta sala desde el inicio” (o “desde último id”).
   - Servidor consulta DB y envía los últimos mensajes (ej. 50).
   - Cliente los muestra en la UI en orden.
4. **Cuando llega un mensaje nuevo** (por TCP/UDP en tiempo real):
   - Servidor guarda en DB y reenvía a los clientes de la sala.
   - Cliente añade el mensaje a la lista en pantalla.

Así “guardar mensajes” y “historial” se resuelven en el servidor; el cliente solo muestra lo que recibe (en tiempo real + carga inicial).

---

## 4. Archivos (imágenes, PDF, etc.)

- **Envío**: el cliente no envía el archivo crudo en cada mensaje de chat; suele subir el archivo a un almacenamiento (ej. GCP Cloud Storage) y envía un **mensaje** con la URL o el id del archivo.
- **Persistencia**:
  - **Storage**: el archivo queda en un bucket (Cloud Storage); el registro en DB tiene la referencia (path o URL).
  - **Historial**: al cargar “mensajes de la sala”, cada mensaje de tipo “image” o “file” incluye la URL; el cliente descarga y muestra (imagen en el chat) o ofrece descarga (PDF, audio, ejecutable).

Para la rúbrica, en una primera versión puedes enviar la imagen/archivo **en línea** por TCP/UDP (base64 o binario) sin Storage; la “evidencia” es que se recibe y se muestra. Para salas con historial y muchos usuarios, conviene pasar a “subir a Storage + mensaje con referencia”.

---

## 5. Resumen

| Pregunta | Respuesta corta |
|----------|------------------|
| ¿Dónde se guardan los mensajes? | En memoria del servidor (solo sesión) o en base de datos en GCP (historial persistente). Opcionalmente también en el cliente (archivo local) como copia. |
| ¿Cómo se “guarda” el historial? | Servidor escribe cada mensaje en DB; al unirse/reconectar, el cliente pide “últimos mensajes” y el servidor los lee de la DB y los envía. |
| ¿Y los archivos? | Mejor subirlos a Cloud Storage y guardar en DB solo la referencia; el historial muestra la referencia y el cliente descarga para ver o guardar. |

Siguiente documento: **GCP-SERVIDOR.md** para montar el servidor en GCP.
