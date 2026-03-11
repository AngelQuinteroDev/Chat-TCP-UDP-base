#!/bin/bash
# ─────────────────────────────────────────────────────────────────
#  deploy_gcp.sh
#  Ejecutar en la VM de GCP después de hacer SSH:
#    gcloud compute ssh chat-server --zone=us-central1-a
#    bash deploy_gcp.sh
# ─────────────────────────────────────────────────────────────────

set -e

echo "=== Instalando .NET 8 ==="
sudo apt-get update -y
sudo apt-get install -y dotnet-sdk-8.0

echo "=== Verificando instalación ==="
dotnet --version

echo "=== Creando directorio del servidor ==="
mkdir -p ~/chat-server
cd ~/chat-server

echo ""
echo ">>> Copia los archivos del servidor aquí con:"
echo "    gcloud compute scp --recurse ./ChatServer/ chat-server:~/chat-server/ --zone=us-central1-a"
echo ""

echo "=== Compilando el servidor ==="
# (Ejecutar esto después de copiar los archivos)
# dotnet build ChatServer.csproj -c Release -o ./publish

echo "=== Creando servicio systemd ==="
sudo tee /etc/systemd/system/chat-server.service > /dev/null <<'EOF'
[Unit]
Description=Chat Server TCP+UDP+REST
After=network.target

[Service]
User=ubuntu
WorkingDirectory=/home/ubuntu/chat-server/publish
ExecStart=/usr/bin/dotnet ChatServer.dll
Restart=always
RestartSec=5
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable chat-server

echo ""
echo "=== Para iniciar el servidor ==="
echo "    sudo systemctl start chat-server"
echo "    sudo systemctl status chat-server"
echo "    journalctl -u chat-server -f   # logs en tiempo real"
echo ""
echo "=== Abre los puertos en GCP Firewall ==="
echo "    gcloud compute firewall-rules create allow-chat-tcp  --allow tcp:9000 --target-tags=chat-server"
echo "    gcloud compute firewall-rules create allow-chat-udp  --allow udp:9001 --target-tags=chat-server"
echo "    gcloud compute firewall-rules create allow-chat-api  --allow tcp:5000 --target-tags=chat-server"
echo ""
echo "=== IP pública de tu VM ==="
echo "    gcloud compute instances describe chat-server --zone=us-central1-a --format='get(networkInterfaces[0].accessConfigs[0].natIP)'"