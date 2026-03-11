using System;
using System.Collections.Generic;
using UnityEngine;

public class UdpChunkReceiver
{

    public event Action<ChunkMeta, byte[]> OnFileComplete;

    public class ChunkMeta
    {
        public string TransferId;
        public string Username;
        public string RoomId;
        public string FileName;
        public string FileType;   
        public int    TotalChunks;
    }

    private class Transfer
    {
        public ChunkMeta      Meta;
        public string[]       Segments;   
        public int            Received;   
        public DateTime       LastUpdate; 
    }

    private readonly Dictionary<string, Transfer> _transfers = new();

    private const int TIMEOUT_SECONDS = 30;


    public void HandleChunk(string json)
    {
        try
        {
            ChunkJson c = JsonUtility.FromJson<ChunkJson>(json);

            if (string.IsNullOrEmpty(c.transfer_id) || c.total_chunks <= 0)
            {
                Debug.LogWarning("[UdpChunkReceiver] Chunk con datos inválidos, ignorando");
                return;
            }


            PurgeTimedOut();


            if (!_transfers.TryGetValue(c.transfer_id, out Transfer t))
            {
                t = new Transfer
                {
                    Meta = new ChunkMeta
                    {
                        TransferId  = c.transfer_id,
                        Username    = c.username,
                        RoomId      = c.room_id,
                        FileName    = c.file_name,
                        FileType    = c.file_type,
                        TotalChunks = c.total_chunks
                    },
                    Segments   = new string[c.total_chunks],
                    Received   = 0,
                    LastUpdate = DateTime.UtcNow
                };
                _transfers[c.transfer_id] = t;
                Debug.Log($"[UdpChunkReceiver] Nueva transferencia '{c.file_name}' — {c.total_chunks} chunks (id={c.transfer_id})");
            }

            if (t.Segments[c.chunk_index] == null)
            {
                t.Segments[c.chunk_index] = c.data;
                t.Received++;
                t.LastUpdate = DateTime.UtcNow;

                Debug.Log($"[UdpChunkReceiver] Chunk {c.chunk_index + 1}/{c.total_chunks} recibido (id={c.transfer_id})");
            }
            else
            {
                Debug.Log($"[UdpChunkReceiver] Chunk {c.chunk_index} duplicado ignorado (id={c.transfer_id})");
                return;
            }

            if (t.Received == t.Meta.TotalChunks)
            {
                Debug.Log($"[UdpChunkReceiver] Archivo completo: '{t.Meta.FileName}' — reensamblando...");
                Reassemble(t);
                _transfers.Remove(c.transfer_id);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UdpChunkReceiver] Error procesando chunk: {ex.Message}");
        }
    }


    private void Reassemble(Transfer t)
    {
        try
        {
            string fullBase64 = string.Concat(t.Segments);
            byte[] bytes      = Convert.FromBase64String(fullBase64);

            Debug.Log($"[UdpChunkReceiver] Reensamblado: {bytes.Length} bytes — {t.Meta.FileName}");
            OnFileComplete?.Invoke(t.Meta, bytes);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UdpChunkReceiver] Error al reensamblar '{t.Meta.FileName}': {ex.Message}");
        }
    }

    private void PurgeTimedOut()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-TIMEOUT_SECONDS);
        var expired = new List<string>();

        foreach (var kv in _transfers)
            if (kv.Value.LastUpdate < cutoff)
                expired.Add(kv.Key);

        foreach (var id in expired)
        {
            Debug.LogWarning($"[UdpChunkReceiver] Transfer {id} expirado y descartado");
            _transfers.Remove(id);
        }
    }

    [Serializable]
    private class ChunkJson
    {
        public string type;
        public string transfer_id;
        public int    chunk_index;
        public int    total_chunks;
        public string username;
        public string room_id;
        public string file_name;
        public string file_type;
        public string data;
    }
}