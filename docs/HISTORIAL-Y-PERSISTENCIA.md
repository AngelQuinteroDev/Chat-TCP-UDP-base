# Historial y Persistencia en el Proyecto

El manejo de persistencia de datos (guardar mensajes pasados o archivos compartidos) es uno de los temas más amplios en la arquitectura de un sistema de mensajería. En el marco de nuestro desarrollo, se ha priorizado el rendimiento en tiempo real por sobre un archivo muerto histórico.

---

## 1. El Estado Actual del Historial en el Servidor (En Memoria)

Actualmente, el servidor en C# (.NET) mantiene una persistencia **Efímera (En Memoria Ram)**. 
- Cuando la API REST crea un `room_id`, ese ID se deposita en una estructura tipo `ConcurrentDictionary` dentro de la memoria RAM del proceso .NET en nuestro servidor de GCP.
- El servidor solo actúa como un "Router inteligente" o *Relay*.
- Cuando recibe un JSON de mensaje tipo `"CHAT"`, determina de inmediato a qué sala pertenece y lo bombea cíclicamente a todo aquel cliente cuyo socket esté asociado administrativamente a ese ID de Sala.

**Por lo cual:** Nosotros no alojamos mensajes históricamente en una base de datos (Ej: MySQL o MongoDB) ni retransmitimos el historial cuando un cliente refresca. Si un cliente no estaba en la sala en el momento exacto donde la foto o texto fue transmitido, no lo recibe. La sala se considera "desechable" tras culminar la sesión.

---

## 2. Persistencia en el Cliente (La Evidencia de Uso)

Si bien el servidor no cuenta con registro cronológico para ahorrar costos en la infraestructura de la nube, **Unity se encarga de dejar evidencia local** del funcionamiento efectivo para el usuario final.

### Manejo de Archivos Pesados:
- **Codificación en Tránsito:** Cuando el usuario A envía una imagen o PDF, Unity pasa el contenido binario a texto (Codificación Base64) utilizando `Convert.ToBase64String` y embebe directamente el String gordo dentro de la llave `"file_data"` del Payload JSON de TCP.
- **Decodificación y Visualización (Imágenes):** Cuando el Usuario B recibe ese JSON en un microsegundo por el socket, si el `file_type` es `"image"`, extrae ese bloque Base64, lo devuelve a array de Bytes temporalmente con `Convert.FromBase64String`, y genera una textura visual asíncrona mostrándolo directamente sobre la vista (Scroll Rect), perdiéndose cuando se cierra el proceso, a menos que el usuario tome captura.
- **Descarga Física Local (Documentos/Audio):** Si el payload recibido viene marcado como tipo `"pdf"` o `"audio"`, la burbuja generada cuenta con un botón interactivo. Al darle click, Unity ejecuta sistemáticamente la decodificación del paquete persistiendo verdaderamente ese binario a disco dentro del `Application.persistentDataPath` de Windows/Mac para abrir luego externamente el documento o ejecutar el audio, sirviendo como evidencia final (Persistencia de Cliente) de la integridad del paquete del lado receptor.
