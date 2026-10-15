#!/bin/sh
# Runs once inside the Zanshin image: waits for the server, mints API keys, applies ./config.
set -eu
until zanshinctl keys list >/dev/null 2>&1; do echo "provision: waiting for Zanshin at $ZANSHIN_URL"; sleep 2; done

mint() { # kind name file
  if [ ! -s "$3" ]; then
    zanshinctl keys create --kind "$1" "$2" | sed -n 's/.*"secret": *"\([^"]*\)".*/\1/p' | head -1 > "$3"
    [ -s "$3" ] || { echo "provision: could not mint $1 key"; exit 1; }
    echo "provision: created $1 key '$2'"
  fi
}
mint server nakama /zanshin/server.key
mint client unity  /zanshin/client.key

zanshinctl config apply /config | tee /tmp/apply.json
if grep -q '"error":' /tmp/apply.json; then echo 'provision: config apply reported errors'; exit 1; fi
echo
echo "provision: done."
echo "  console:            http://localhost:7550/console  (bootstrap key: \$ZANSHIN_ADMIN_KEY)"
echo "  Unity client key:   $(cat /zanshin/client.key)   (only needed for ZanshinDirect.cs)"
