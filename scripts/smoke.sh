#!/usr/bin/env bash
# End-to-end check without Unity: device-auth against Nakama, call the zanshin.* RPCs the sample uses.
set -euo pipefail
NAKAMA=${NAKAMA:-http://localhost:7350}
KEY=${NAKAMA_SERVER_KEY:-defaultkey}
DEVICE=${DEVICE:-smoke-$(date +%s)}

for i in $(seq 1 90); do curl -fs "$NAKAMA/healthcheck" >/dev/null && break; sleep 2; done
curl -fs "$NAKAMA/healthcheck" >/dev/null || { echo "smoke: Nakama is not answering at $NAKAMA"; exit 1; }
TOKEN=$(curl -fsS -u "$KEY:" -X POST "$NAKAMA/v2/account/authenticate/device?create=true" \
  -d "{\"id\":\"$DEVICE\",\"vars\":{\"lang\":\"en\"}}" | python3 -c 'import sys,json; print(json.load(sys.stdin)["token"])')
rpc() { curl -fs -X POST "$NAKAMA/v2/rpc/$1?unwrap" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "$2"; }

FLAGS=$(rpc zanshin.flags '{"names":["shop_v2","starter_bundle_price"]}')
echo "flags: $FLAGS"
echo "$FLAGS" | python3 -c '
import sys, json
f = {x["key"]: x for x in json.load(sys.stdin)["flags"]}
assert "shop_v2" in f and "starter_bundle_price" in f, f
assert f["starter_bundle_price"]["value"] == 2.99, f["starter_bundle_price"]   # device-auth players get the rule value
print("ok: flags evaluated for the Nakama user (starter_bundle_price =", f["starter_bundle_price"]["value"], ")")'

EVENTS=$(rpc zanshin.live_events '{}')
echo "live events: $EVENTS"
echo "$EVENTS" | python3 -c 'import sys,json; e=[x for x in json.load(sys.stdin)["live_events"] if x["key"]=="weekend_boost"]; assert e and e[0]["active"], e; print("ok: weekend_boost is live, payload", e[0]["value"])'

EXP=$(rpc zanshin.experiments '{"names":["starter_offer_copy"]}')
echo "experiments: $EXP"
echo "$EXP" | python3 -c 'import sys,json; e=json.load(sys.stdin)["experiments"]; assert e and e[0]["variant"] in ("a","b"), e; print("ok: sticky variant", e[0]["variant"])'

TRACK=$(rpc zanshin.track '{"events":[{"name":"purchase","value":"2.99","metadata":{"sku":"starter_bundle"}}]}')
echo "track: $TRACK"
echo "$TRACK" | python3 -c 'import sys,json; assert json.load(sys.stdin)["accepted"] == 1; print("ok: purchase tracked")'
echo "smoke: all good"
