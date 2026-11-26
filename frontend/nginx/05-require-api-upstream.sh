#!/bin/sh
# Runs before the nginx image renders the templates: API_UPSTREAM is required (research R18).
set -eu

if [ -z "${API_UPSTREAM:-}" ]; then
    echo "easymenu-web: API_UPSTREAM is required (the API base URL nginx proxies /api/, /hubs/ and /media/ to, e.g. http://api:8080)." >&2
    exit 1
fi

case "$API_UPSTREAM" in
    http://*|https://*) ;;
    *)
        echo "easymenu-web: API_UPSTREAM must start with http:// or https:// (got \"$API_UPSTREAM\")." >&2
        exit 1
        ;;
esac
