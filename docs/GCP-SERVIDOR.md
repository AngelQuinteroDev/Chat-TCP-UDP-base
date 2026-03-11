# Servidor Centralizado en Google Cloud Platform (GCP)

Para este proyecto, se requería una intermediación confiable entre los clientes de Unity para resolver las clásicas limitaciones de red en los entornos P2P modernos (CGNAT, Firewalls, Port Forwarding). La solución consistió en desarrollar un servidor independiente en C# (.NET 8) y desplegarlo en una Máquina Virtual de GCP. 

---

## 1. La Elección de la Infraestructura en la Nube

Optamos por utilizar **Google Compute Engine (VM)** sobre opciones "Serverless" como Cloud Run (pensado solo para HTTP).
La razón principal es el requisito del proyecto de soportar tanto el protocolo **TCP** como **UDP** de forma cruda, manteniendo Sockets abiertos permanentemente para la mensajería en tiempo real. Esto requiere control total sobre el sistema operativo base para abrir los puertos en el Firewall y atar (bind) nuestros Listeners a ellos.

- **Tipo de Máquina:** Utilizamos una instancia `e2-micro` alojada en `us-central1` (aprovechando la capa gratuita de GCP), que provee recursos más que suficientes para enrutar texto ligero e imágenes comprimidas entre las salas interactivas.
- **Sistema Operativo:** Ubuntu 22.04 LTS (Facilita correr cargas de trabajo generadas por el CLI de dotnet).

---

## 2. Puertos Expuestos y Firewalls (Reglas Ingress)

Para que el servidor C# (`Server/Program.cs`) intercepte correctamente a Unity, la consola de VPC Network de GCP fue configurada para permitir tráfico externo hacia tres puertos clave:

1. **REST API (Puerto 5000):** Protocolo `HTTP/TCP`. Este puerto es el primer punto de contacto de Unity para solicitar el alta de una sala nueva, o verificar que una cadena de `room_id` concuerde con una sala guardada en el diccionario de RAM de la máquina virtual.
2. **Servidor TCP (Puerto 9000):** Protocolo `TCP`. Unity abre un socket persistente contra la IP pública usando `TcpClient`. Este canal está restringido exclusivamente a las comunicaciones de chat ordenadas.
3. **Servidor UDP (Puerto 9001):** Protocolo `UDP`. Unity envía al instante datagramas en bloque a través de este puerto usando `UdpClient`, y el servidor los dispara a sus oyentes al instante simulando una comunicación ultra-rápida.

---

## 3. Despliegue (Deploy) de la Solución

El repositorio incluye un proyecto C# completo apartado de Unity en el directorio `/Server`.

El comando base que arranca este servicio de escucha se encapsula típicamente en `dotnet run`, pero en un servidor Cloud de producción necesitamos que este aplicativo ".NET" se reinicie solo si falla o si la VM se reinicia. 

Para lograrlo, el proyecto incluyó el script `deploy_gcp.sh`:
- Este archivo compilado (Release) registra nuestro ejecutable de Chat Server como un servicio oficial del Sistema (Daemon de `systemd`) de Linux.
- Por ende, actualmente en la nube basta con encender la VM de GCP asignada, y el servicio de chat arranca pasivamente en segundo plano, esperando los Handshakes enviados desde el Menú Principal y los Sockets de nuestra interfaz de Unity.
