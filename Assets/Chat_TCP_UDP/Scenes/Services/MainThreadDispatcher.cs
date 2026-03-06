using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Los callbacks de red llegan en hilos de background.
/// Unity solo permite modificar objetos (UI, Transforms, etc.)
/// desde el hilo principal. Este componente actúa como puente:
/// encola acciones y las ejecuta en el Update del hilo principal.
///
/// Se crea automáticamente al iniciar la aplicación.
/// Uso: MainThreadDispatcher.Run(() => miTexto.text = "hola");
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher _instance;
    private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        var go = new GameObject("[MainThreadDispatcher]");
        _instance = go.AddComponent<MainThreadDispatcher>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        while (_queue.TryDequeue(out Action action))
        {
            try { action.Invoke(); }
            catch (Exception ex) { Debug.LogError("[MainThread] " + ex.Message); }
        }
    }

    /// <summary>
    /// Encola una acción para ejecutarse en el próximo Update del hilo principal.
    /// Seguro llamarlo desde cualquier hilo de red o background.
    /// </summary>
    public static void Run(Action action)
    {
        _queue.Enqueue(action);
    }
}