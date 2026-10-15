# Nakama + Zanshin + Unity

A complete, runnable example of a Unity game using [Nakama](https://heroiclabs.com/nakama/) as its
game server and [Zanshin](https://github.com/rep0rtDev/zanshin) as its LiveOps backend — feature flags,
A/B experiments, live events, in-game inbox and analytics — with **zero Zanshin code in the game client**.

```
Unity (unity/)  ──device auth, zanshin.* RPCs──▶  Nakama :7350  ──server key──▶  Zanshin :7550
                                                  + Zanshin module                 console /console
```

The Zanshin runtime module inside Nakama turns every authenticated Nakama user into a Zanshin
identity (with `default.auth_provider`, `lang`, `location`, `timezone`, … ready for targeting),
exposes `zanshin.flags`, `zanshin.experiments`, `zanshin.live_events`, `zanshin.inbox`,
`zanshin.track`, … as RPCs, and mirrors Nakama purchases, leaderboard writes and friend invites
into Zanshin's core events. The client only ever talks to Nakama.

## Run it

```sh
docker compose up -d           # Postgres ×2, Redis, Zanshin, provisioning, Nakama
docker compose logs -f provision
```

`provision` waits for Zanshin, mints a **server key** for Nakama and a **client key** for the optional
direct-SDK script, then applies [`config/`](config) (config-as-code, `zanshinctl config apply`):

| file | what it shows |
|---|---|
| `flags/shop_v2.yaml` | bool flag: beta testers always on, everyone else in a 50 % rollout |
| `flags/starter_bundle_price.yaml` | number flag targeting `default.auth_provider = device` — a prop the Nakama module fills in |
| `experiments/starter_offer_copy.yaml` | two headlines for the starter offer, sticky per player, purchases attributed automatically |
| `live_events/weekend_boost.yaml` | an active live event with a payload (`xp_multiplier: 2`) |
| `audiences/beta_testers.yaml` | dynamic audience over `custom.beta` |

Then:

* Zanshin console: <http://localhost:7550/console> — sign in with the admin API key
  `example-admin-key-change-me` (see `docker-compose.yml`).
* Nakama console: <http://localhost:7351> — `admin` / `password`.

Without Unity you can exercise the same path from the shell: `./scripts/smoke.sh` device-auths against
Nakama and calls the RPCs the sample uses (this is what CI runs).

## The Unity side

Open [`unity/`](unity) in Unity 2022.3 or newer. The Package Manager pulls
[Nakama Unity](https://github.com/heroiclabs/nakama-unity) and the
[Zanshin Unity SDK](https://github.com/rep0rtDev/zanshin/tree/main/sdk/unity) from git (see
`Packages/manifest.json`). Press Play in any scene — [`LiveOpsDemo.cs`](unity/Assets/Scripts/LiveOpsDemo.cs)
spawns itself and walks through:

1. `AuthenticateDeviceAsync` → the Nakama module upserts the player in Zanshin.
2. `zanshin.flags` → `shop_v2` and `starter_bundle_price` evaluated for *this* player, with the reason.
3. `zanshin.experiments` + `zanshin.expose` → sticky headline variant for the offer button.
4. `zanshin.live_events` → active events and their payload.
5. `zanshin.inbox` → campaign messages targeted at the player.
6. Pressing the offer button → `zanshin.track` a `purchase` event (attributed to the variant server-side).

Things to try while it runs:

* Console → Feature flags → `shop_v2` → disable, press Play again.
* Console → Players → your player → Properties → set `custom.beta = true` → `shop_v2` is now `true` with reason `rule`.
* Console → Messages → new campaign for "All players" → it shows up in the inbox on the next run.
* Console → Experiments → `starter_offer_copy` → Results after a few purchases from different devices.

[`ZanshinDirect.cs`](unity/Assets/Scripts/ZanshinDirect.cs) is the optional second route — the Zanshin
Unity SDK talking to Zanshin directly with the client key printed by `provision` (live flag change
stream, push tokens, `Identify()`); leave `ClientKey` empty to keep it off.

## Production notes

* Change `ZANSHIN_ADMIN_KEY`, `ZANSHIN_JWT_SECRET`, `NAKAMA_SERVER_KEY` and `NAKAMA_CONSOLE_PASSWORD`
  (all overridable through the environment / `.env`).
* The server key is minted once and kept in the `keys` volume; rotate it from the console
  (API keys → Rotate) and restart Nakama with the new value.
* `ZANSHIN_VERSION` pins both images (`ghcr.io/rep0rtdev/zanshin`, `ghcr.io/rep0rtdev/zanshin-nakama`);
  the Nakama image is Nakama 3.22 with the module preinstalled.
* Zanshin's own deployment guide: <https://github.com/rep0rtDev/zanshin/blob/main/docs/production.md>.

## License

Apache-2.0.
