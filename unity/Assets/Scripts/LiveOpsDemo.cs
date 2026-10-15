using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using Nakama.TinyJson;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The whole loop in one component: device-auth against Nakama, then read LiveOps state through the
/// zanshin.* RPCs the Zanshin Nakama module registers. No Zanshin credentials live in the client —
/// Nakama holds the server key, the player is identified by their Nakama user id.
/// Spawns itself at start-up, so any scene works; drop it on a GameObject to tweak the fields.
/// </summary>
public sealed class LiveOpsDemo : MonoBehaviour
{
    [Header("Nakama")]
    public string Scheme = "http";
    public string Host = "127.0.0.1";
    public int Port = 7350;
    public string ServerKey = "defaultkey";

    IClient _client;
    ISession _session;
    Text _log;
    Button _buy;
    double _price = 4.99;
    string _offerCopy = "Starter bundle";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindObjectOfType<LiveOpsDemo>() == null) new GameObject("LiveOpsDemo").AddComponent<LiveOpsDemo>();
    }

    async void Start()
    {
        BuildUi();
        try { await Run(); }
        catch (Exception e) { Log("<color=#c33>error:</color> " + e.Message); Debug.LogException(e); }
    }

    async Task Run()
    {
        _client = new Client(Scheme, Host, Port, ServerKey, UnityWebRequestAdapter.Instance);
        var vars = new Dictionary<string, string> { ["lang"] = Application.systemLanguage.ToString() };
        _session = await _client.AuthenticateDeviceAsync(SystemInfo.deviceUniqueIdentifier, null, true, vars);
        Log($"Nakama session for user <b>{_session.UserId}</b> — the module just upserted this player in Zanshin.");

        // 1. Feature flags: evaluated for this player (rules see default.* props filled in by the module).
        var flags = await Rpc<Dictionary<string, object>>("zanshin.flags", new { names = new[] { "shop_v2", "starter_bundle_price" } });
        foreach (Dictionary<string, object> f in (List<object>)flags["flags"])
        {
            Log($"flag <b>{f["key"]}</b> = {f["value"]}  <i>({Reason(f)})</i>");
            if ((string)f["key"] == "starter_bundle_price") _price = Convert.ToDouble(f["value"]);
            if ((string)f["key"] == "shop_v2") Log(Convert.ToBoolean(f["value"]) ? "→ showing the new shop" : "→ showing the old shop");
        }

        // 2. Experiment: sticky variant per player; purchases below are attributed to it automatically.
        var exps = await Rpc<Dictionary<string, object>>("zanshin.experiments", new { names = new[] { "starter_offer_copy" } });
        foreach (Dictionary<string, object> e in (List<object>)exps["experiments"])
        {
            _offerCopy = e["value"] as string ?? _offerCopy;
            Log($"experiment <b>{e["key"]}</b> → variant {e["variant"]}: \"{_offerCopy}\"");
            await Rpc<Dictionary<string, object>>("zanshin.expose", new { experiment = e["key"] });
        }

        // 3. Live events: what is active right now, with its payload.
        var events = await Rpc<Dictionary<string, object>>("zanshin.live_events", new { });
        foreach (Dictionary<string, object> ev in (List<object>)events["live_events"])
        {
            if (!(ev["active"] is bool a && a)) continue;
            var payload = ev["value"] as Dictionary<string, object>;
            Log($"live event <b>{ev["name"]}</b> is on — payload {payload?.ToJson()}");
        }

        // 4. Inbox: campaign messages targeted at this player.
        var inbox = await Rpc<Dictionary<string, object>>("zanshin.inbox", new { limit = 10 });
        var messages = (List<object>)inbox["messages"];
        Log(messages.Count == 0 ? "inbox: empty (send a campaign from the console — it lands here)" : $"inbox: {messages.Count} message(s)");
        foreach (Dictionary<string, object> m in messages) Log($"  ✉ <b>{m["title"]}</b> — {m["body"]}");

        _buy.GetComponentInChildren<Text>().text = $"{_offerCopy} — ${_price:0.00}";
        _buy.interactable = true;
    }

    async void Buy()
    {
        _buy.interactable = false;
        // 5. Events: tracked through Nakama, tagged with the player's experiment variants server-side.
        var r = await Rpc<Dictionary<string, object>>("zanshin.track", new
        {
            events = new[] { new { name = "purchase", value = _price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), metadata = new { sku = "starter_bundle" } } },
        });
        Log($"tracked purchase ${_price:0.00} (accepted {r["accepted"]}) — see Analytics and the experiment results in the console");
    }

    async Task<T> Rpc<T>(string id, object payload)
    {
        var r = await _client.RpcAsync(_session, id, payload.ToJson());
        return r.Payload.FromJson<T>();
    }

    static string Reason(Dictionary<string, object> f)
    {
        var reason = f.TryGetValue("reason", out var r) ? r as Dictionary<string, object> : null;
        var kind = reason != null && reason.TryGetValue("kind", out var k) ? k as string : "default";
        return f.TryGetValue("variant", out var v) && v is string s && s.Length > 0 ? $"{kind}, variant {s}" : kind;
    }

    void Log(string line)
    {
        Debug.Log(line.Replace("<b>", "").Replace("</b>", "").Replace("<i>", "").Replace("</i>", ""));
        if (_log != null) _log.text += line + "\n";
    }

    void BuildUi()
    {
        var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _log = Make<Text>(canvas.transform, new Vector2(0, 0.2f), new Vector2(1, 1), new Vector2(24, 12), new Vector2(-24, -12));
        _log.font = font; _log.fontSize = 20; _log.color = Color.white; _log.supportRichText = true; _log.alignment = TextAnchor.UpperLeft;
        _log.text = "Connecting to Nakama…\n";

        var btn = Make<Image>(canvas.transform, new Vector2(0.3f, 0.06f), new Vector2(0.7f, 0.16f), Vector2.zero, Vector2.zero);
        btn.color = new Color(0.15f, 0.55f, 0.25f);
        _buy = btn.gameObject.AddComponent<Button>();
        _buy.interactable = false;
        _buy.onClick.AddListener(Buy);
        var label = Make<Text>(btn.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        label.font = font; label.fontSize = 24; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.text = "…";
        Camera.main?.gameObject.SetActive(true);
        if (Camera.main == null) new GameObject("Camera", typeof(Camera)).GetComponent<Camera>().backgroundColor = new Color(0.09f, 0.09f, 0.1f);
    }

    static T Make<T>(Transform parent, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax) where T : Component
    {
        var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(T));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        return go.GetComponent<T>();
    }
}
