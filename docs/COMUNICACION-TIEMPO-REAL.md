# Comunicación en Tiempo Real en el Proyecto

En sistemas interactivos como este chat, el tiempo real es fundamental. En esta documentación explicamos cómo coordinamos el protocolo HTTP tradicional (REST) con los sockets en tiempo real (TCP y UDP) de manera armónica.

---

## 1. El Doble Enfoque: REST + Sockets

Nuestra arquitectura utiliza la combinación de dos vías de comunicación para optimizar la carga del servidor:

### A. API REST (Para el Handshake de Estado)
Actividades como **Crear una Sala** o **Verificar Existencia** no necesitan mantenerse abiertas en tiempo real, son consultas cortas.
En lugar de saturar nuestros puertos TCP/UDP con lógica de *matchmaking*, Unity usa `HttpClient` estándar apuntando al puerto `5000` del servidor GCP. 

*Ejemplo de flujo:*
1. Unity hace: `POST http://34.59.37.232:5000/rooms/create` -> `{"name": "Sala de Ana"}`
2. Servidor devuelve inmediatamente: `{"success": true, "room_id": "XY89Q"}`

### B. Sockets TCP / UDP (Para el Chat en Vivo)
Una vez que el cliente tiene en sus manos un `room_id` válido, entra la potencia de los sockets. Ya no usamos HTTP.
- **TCP (Puerto 9000):** Se usa para establecer una sesión larga (keep-alive). Garantiza que cada mensaje que se manda (especialmente los grandes paquetes de Base64 de las imágenes y PDFs) llegue forzosamente completo y en el orden en que fue enviado.
- **UDP (Puerto 9001):** Utilizado para mensajes que no pueden esperar la validación del protocolo. Es disparo y olvido (Fire-and-forget). Muy ligero, pero la pérdida de paquetes al intentar enviar un PDF inmenso por UDP puede corromper el archivo en redes inestables.

---

## 2. El Handshake a Nivel de Aplicación

Aunque TCP ya hace un handshake a nivel de red, nosotros necesitamos un "Apretón de manos" a nivel de la *lógica de nuestro Chat*. El servidor GCP necesita saber "quién" se acaba de conectar y "a qué sala" pertenece ese socket, de lo contrario no sabría cómo hacer broadcast de los mensajes.

El mecanismo implementado (visible en `Ui_tcpclient_gcp.cs`) es el siguiente:

1. El Socket de Unity se conecta al puerto 9000.
2. El servidor detecta la nueva conexión y por saluda primero enviando un paquete: `{"type": "HELLO"}`
3. Unity detecta el HELLO y usa ese disparador para enviar su tarjeta de presentación: 
   `{"type": "JOIN", "username": "Angel", "room_id": "XY89Q"}`
4. El servidor verifica que la sala exista, vincula internamente el `TcpClient` de "Angel" a la lista de sockets de "XY89Q", y responde al cliente: 
   `{"type": "WELCOME"}`
5. ¡Listo! Unity habilita el botón de "Enviar", sabiendo que está de lleno dentro del túnel de su sala.

---

## 3. ¿Por qué no usamos Polling ni WebSockets?

- **Contra el Polling (Consultar cada x segundos):** Es ineficiente. Preguntar a la API REST cada 1 segundo "¿tengo mensajes?" mataría rápidamente los recursos de la máquina virtual `e2-micro`, y causaría lag artificial a los usuarios. Los sockets (TCP/UDP) evitan el polling: el cliente simplemente se queda "escuchando" asíncronamente y reacciona de manera pasiva en el microsegundo exacto en el que el servidor decide retransmitirle un mensaje.
  
- **Contra los WebSockets:** Aunque los WebSockets son el estándar de oro en aplicaciones Web (React, Angular), en una aplicación nativa como Unity donde operamos directamente sobre C# Sockets, podemos envolver la lógica UDP pura de manera mucho más natural usando las clases `UdpClient` y `TcpListener`, dándonos un control de bajísimo nivel sobre la trama de bytes y cumpliendo perfectamente con el requerimiento de usar explícitamente los protocolos base.
