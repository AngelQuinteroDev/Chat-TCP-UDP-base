# Arquitectura y Requisitos del Proyecto Chat TCP/UDP

Este documento establece los fundamentos arquitectónicos del proyecto de Chat, el cual fue diseñado específicamente para solucionar los problemas clásicos de las conexiones P2P (Peer-to-Peer) al introducir un modelo robusto de **Cliente-Servidor**.

---

## 1. Evolución de la Arquitectura

La versión base del repositorio utilizaba un enfoque *Peer-to-Peer*. Aunque útil para entender conceptos básicos de sockets, en la práctica obligaba a uno de los usuarios a actuar como anfitrión (host), lo cual requería configuraciones engorrosas como la apertura manual de puertos (Port Forwarding) en el router del usuario.

Para este proyecto, decidimos dar un salto hacia una **Arquitectura Cliente-Servidor Centralizada en la Nube (Google Cloud Platform)**. 

### ¿Por qué Cliente-Servidor?
- **Accesibilidad:** Los clientes en Unity solo necesitan conectarse a una única IP pública estática en la nube.
- **Aislamiento por Salas:** Permite que múltiples grupos de personas chateen simultáneamente usando "Códigos de Sala", sin interferir entre ellos.
- **Seguridad y Control:** El servidor actúa como intermediario (Relay) validando la existencia de las salas antes de permitir el tráfico de datos.

---

## 2. Componentes del Sistema

La solución está dividida en dos grandes bloques:

### A. El Cliente (Unity 6000.3+)
El ejecutable que los usuarios instalan. Tiene la lógica de UI y de red (Scripts TCP y UDP). 
El flujo de la aplicación cliente es:
1. Contactar a la **API REST** del servidor para crear o unirse a una Sala.
2. Seleccionar el protocolo deseado (**TCP** o **UDP**).
3. Establecer el socket persistente para el envío en tiempo real de Mensajes, Imágenes y Archivos (Pdfs/Audios).

### B. El Servidor Dedicado (C# .NET 8)
Alojado en una Máquina Virtual (Compute Engine) en GCP. Este servidor corre de manera independiente a Unity y se encarga de:
- Mantener una API REST en el puerto `5000` para generar y validar IDs de Salas.
- Escuchar conexiones persistentes TCP en el puerto `9000`.
- Recibir y redirigir datagramas UDP en el puerto `9001`.
- Funcionar como *Relay*: Recibe un paquete que va dirigido a la sala `ABC-1234` y lo retransmite exclusivamente a los demás clientes conectados a esa sala.

---

## 3. Cumplimiento de Requisitos Funcionales

El diseño responde en su totalidad a los requisitos propuestos para la entrega:

1. **Selector de Protocolo:** Implementado en la escena `MainMenu`, fuerza al usuario a elegir explícitamente TCP o UDP antes de cargar las escenas de chat.
2. **Funcionamiento Dual:** El núcleo de red permite comunicación tanto por TCP (confiable) como por UDP (rápido).
3. **UI Clara y Bidireccional:** Las salas de chat muestran los mensajes en burbujas adaptables para el emisor y los receptores.
4. **Envío de Archivos:** El chat no se limita a texto. Permite el envío de imágenes (decodificadas y renderizadas visualmente en Unity) y envío de documentos `.pdf` u otros archivos empaquetados en *Base64*, con opción de ser descargados al disco local por los destinatarios.
