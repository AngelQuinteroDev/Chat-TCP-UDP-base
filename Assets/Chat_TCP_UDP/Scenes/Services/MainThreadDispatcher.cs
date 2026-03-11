using System;
using System.Collections.Concurrent;
using UnityEngine;


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

    public static void Run(Action action)
    {
        _queue.Enqueue(action);
    }
}