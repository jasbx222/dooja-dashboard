#!/usr/bin/env bash
# Publish the Flutter web app into this server's wwwroot/app.
# From the monorepo, the Flutter project is the parent of dashboard/.
# From the standalone dashboard repo, set FLUTTER_APP_DIR or place dooja-app next to this repo.
set -euo pipefail
DASH_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
if [[ -n "${FLUTTER_APP_DIR:-}" ]]; then
  APP_ROOT="$(cd "$FLUTTER_APP_DIR" && pwd)"
elif [[ -f "$DASH_ROOT/../pubspec.yaml" ]]; then
  APP_ROOT="$(cd "$DASH_ROOT/.." && pwd)"
elif [[ -f "$DASH_ROOT/../dooja-app/pubspec.yaml" ]]; then
  APP_ROOT="$(cd "$DASH_ROOT/../dooja-app" && pwd)"
else
  echo "Set FLUTTER_APP_DIR to a checkout of https://github.com/jasbx222/dooja-app" >&2
  exit 1
fi
cd "$APP_ROOT"
API_BASE_URL="${API_BASE_URL:-https://dooja.cloudapps.trade}"
flutter build web --release --base-href /app/ --dart-define="API_BASE_URL=$API_BASE_URL"
DEST="$DASH_ROOT/src/Dooja.Web/wwwroot/app"
rm -rf "$DEST"
mkdir -p "$DEST"
cp -a build/web/. "$DEST/"
echo "Published Flutter web → $DEST"
