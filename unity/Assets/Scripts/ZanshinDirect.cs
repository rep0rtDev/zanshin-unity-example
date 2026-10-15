using System.Collections.Generic;
using UnityEngine;
using Zanshin;

/// <summary>
/// Optional: talk to Zanshin directly with the Unity SDK (client key) instead of through Nakama.
/// Useful for things Nakama does not proxy — the live change stream, push tokens, Identify().
/// Paste the client key printed by `docker compose logs provision` into ClientKey to enable it.
/// </summary>
public sealed class ZanshinDirect : MonoBehaviour
{
    public string Url = "http://127.0.0.1:7550";
    public string ClientKey = "";

    ZanshinClient _z;
    ZanshinStream _stream;
    readonly Queue<System.Action> _mainThread = new Queue<System.Action>();

    async void Start()
    {
        if (string.IsNullOrEmpty(ClientKey)) { enabled = false; return; }
        _z = new ZanshinClient(Url, ClientKey);
        await _z.Authenticate(SystemInfo.deviceUniqueIdentifier, new Dictionary<string, object>
        {
            ["platform"] = Application.platform.ToString(),
            ["version"] = Application.version,
        });
        // Same player as the Nakama-authenticated one once you call Identify(nakamaUserId).
        Debug.Log($"[zanshin] shop_v2 = {await _z.FlagBool("shop_v2")}");

        _stream = _z.Stream();
        _stream.FlagChanged += f => { lock (_mainThread) _mainThread.Enqueue(() => Debug.Log($"[zanshin] flag {f.Key} changed → {f.Value}")); };
        _stream.Connect();
    }

    void Update()
    {
        lock (_mainThread) while (_mainThread.Count > 0) _mainThread.Dequeue()();
    }

    void OnDestroy() => _stream?.Dispose();
}
