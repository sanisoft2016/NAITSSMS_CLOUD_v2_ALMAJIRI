#!/usr/bin/env bash
# Restore one database from a nightly backup. The current data is saved first, the app's API is
# stopped while the database is replaced, and started again afterwards.
#
#   bash restore-db.sh <app>                 list the backups available for <app>
#   bash restore-db.sh <app> latest          restore the newest backup
#   bash restore-db.sh <app> <backup-file>   restore a specific backup (file name or full path)
#
# <app>: silkcoat | naitsasms | naitssmshills | naitssmsama
# Set YES=1 to skip the confirmation question, FORCE=1 to accept a suspiciously small backup.
set -euo pipefail

SAFETY_DIR=/root/restore-safety

say() { printf '\n=== %s\n' "$*"; }
die() { printf '\n!!! STOPPED: %s\n' "$*" >&2; exit 1; }

APP="${1:-}"
case "$APP" in
  silkcoat)      POD=postgres-deployment-0;    API=custrewardmgtsys-v3-api; DIR=/sani/tmp/database-backups;               ARGO_APP=custrewardmgtsys ;;
  naitsasms)     POD=postgres-naitsasms-0;     API=naitsasms-api;           DIR=/sani/tmp/naittsasms/database-backups;    ARGO_APP= ;;
  naitssmshills) POD=postgres-naitssmshills-0; API=naitssmshills-api;       DIR=/sani/tmp/naitssmshills/database-backups; ARGO_APP= ;;
  naitssmsama)   POD=postgres-naitssmsama-0;   API=naitssmsama-api;         DIR=/sani/tmp/naitssmsama/database-backups;   ARGO_APP= ;;
  *) sed -n '2,10p' "$0"; exit 2 ;;
esac

backups() { find "$DIR" -maxdepth 1 -type f \( -name '*.sql.gz' -o -name '*.dump' \) "$@"; }

if [ $# -lt 2 ]; then
  say "Backups for $APP in $DIR (newest first)"
  backups -printf '%TY-%Tm-%Td %TH:%TM  %12s bytes  %f\n' | sort -r | head -40
  exit 0
fi

# kubectl exec on this cluster can hang after a large stream has been fully sent, so file
# transfers poll for the complete file and then stop the exec themselves.
pod_size() { timeout 60 kubectl exec "$POD" -c postgres -- stat -c %s "$1" 2>/dev/null || echo 0; }
pod_sha() { timeout 60 kubectl exec "$POD" -c postgres -- sha256sum "$1" | cut -d' ' -f1; }
stop_bg() { sleep 1; kill "$1" 2>/dev/null || true; wait "$1" 2>/dev/null || true; }

fetch_from_pod() {
  local src="$1" dst="$2" size pid
  size=$(pod_size "$src")
  [ "$size" -gt 0 ] || die "$src is missing or empty in the pod"
  rm -f "$dst"
  kubectl exec "$POD" -c postgres -- cat "$src" > "$dst" &
  pid=$!
  for _ in $(seq 1 300); do
    [ "$(stat -c %s "$dst" 2>/dev/null || echo 0)" = "$size" ] && break
    sleep 2
  done
  stop_bg "$pid"
  [ "$(pod_sha "$src")" = "$(sha256sum "$dst" | cut -d' ' -f1)" ] || die "copy of $src is incomplete"
}

push_to_pod() {
  local src="$1" dst="$2" size pid
  size=$(stat -c %s "$src")
  kubectl exec -i "$POD" -c postgres -- sh -c "cat > $dst" < "$src" &
  pid=$!
  for _ in $(seq 1 300); do
    [ "$(pod_size "$dst")" = "$size" ] && break
    sleep 2
  done
  stop_bg "$pid"
  [ "$(pod_sha "$dst")" = "$(sha256sum "$src" | cut -d' ' -f1)" ] || die "copy of $src into the pod is incomplete"
}

# SQL on stdin; $1 = "app" for the app database, or "postgres" for the maintenance database
psql_pod() {
  kubectl exec -i "$POD" -c postgres -- env TARGET="$1" sh -c \
    '[ "$TARGET" = app ] && TARGET="$POSTGRES_DB"; psql -X -v ON_ERROR_STOP=1 -At -U "$POSTGRES_USER" -d "$TARGET" -v db="$POSTGRES_DB" -v owner="$POSTGRES_USER"'
}

counts() {
  psql_pod app <<'SQL'
SELECT table_schema || '.' || table_name || ' ' ||
       (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text
FROM information_schema.tables
WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY 1;
SQL
}

argo_pause()  { [ -z "$ARGO_APP" ] || kubectl -n argocd patch application "$ARGO_APP" --type merge -p '{"spec":{"syncPolicy":{"automated":null}}}' >/dev/null; }
argo_resume() { [ -z "$ARGO_APP" ] || kubectl -n argocd patch application "$ARGO_APP" --type merge -p '{"spec":{"syncPolicy":{"automated":{"prune":true,"selfHeal":true}}}}' >/dev/null; }

# ---- pick and check the backup file
case "$2" in
  latest) FILE=$(backups -printf '%T@ %p\n' | sort -rn | head -1 | cut -d' ' -f2-) ;;
  /*)     FILE="$2" ;;
  *)      FILE="$DIR/$2" ;;
esac
[ -n "$FILE" ] && [ -f "$FILE" ] || die "backup not found: ${FILE:-none in $DIR}"

case "$FILE" in
  *.sql.gz)
    FORMAT=plain
    gzip -t "$FILE" || die "$FILE is not a valid gzip file"
    HEADER=$(gunzip -c "$FILE" 2>/dev/null | head -c 300 || true)
    grep -q 'PostgreSQL database dump' <<< "$HEADER" || die "$FILE does not contain a PostgreSQL dump" ;;
  *.dump)
    FORMAT=custom
    [ "$(head -c5 "$FILE")" = PGDMP ] || die "$FILE is not a pg_dump custom-format file" ;;
  *) die "unknown backup type: $FILE" ;;
esac
SIZE=$(stat -c %s "$FILE")
[ "$SIZE" -ge 51200 ] || [ "${FORCE:-}" = 1 ] || die "$FILE is only $SIZE bytes - probably an empty backup (FORCE=1 to use it anyway)"

say "Restore plan"
echo "app:         $APP"
echo "backup:      $FILE ($SIZE bytes, $(date -r "$FILE" '+%Y-%m-%d %H:%M'))"
echo "database in: pod $POD"
echo "API stopped: deploy/$API${ARGO_APP:+ (ArgoCD auto-sync paused meanwhile)}"
echo "The CURRENT contents of the database will be replaced (a safety copy is taken first)."
if [ "${YES:-}" != 1 ]; then
  read -r -p "Type the app name ($APP) to continue: " answer
  [ "$answer" = "$APP" ] || die "cancelled"
fi

STAGE=check
REPLICAS=1
SAFETY=""
on_exit() {
  local rc=$?
  [ "$rc" -eq 0 ] && return
  case "$STAGE" in
    stopped)
      echo "The database was NOT changed. Start the API again with:"
      echo "  kubectl scale deploy/$API --replicas=$REPLICAS"
      [ -z "$ARGO_APP" ] || echo "  kubectl -n argocd patch application $ARGO_APP --type merge -p '{\"spec\":{\"syncPolicy\":{\"automated\":{\"prune\":true,\"selfHeal\":true}}}}'" ;;
    replacing)
      echo "The database was being replaced when this stopped; the API is left stopped."
      echo "The data from before the restore is in: ${SAFETY:-(no safety copy - database did not exist)}"
      echo "Fix the problem and run this script again, or put the old data back with:"
      [ -z "$SAFETY" ] || echo "  bash $0 $APP $SAFETY" ;;
  esac
}
trap on_exit EXIT

say "1/6 Waiting for the database pod"
kubectl wait --for=condition=Ready "pod/$POD" --timeout=300s

say "2/6 Stopping the API"
STAGE=stopped
if kubectl get deploy "$API" >/dev/null 2>&1; then
  REPLICAS=$(kubectl get deploy "$API" -o jsonpath='{.spec.replicas}')
  [ "${REPLICAS:-0}" -gt 0 ] || REPLICAS=1
  argo_pause
  kubectl scale "deploy/$API" --replicas=0
  kubectl wait --for=delete pod -l "app=$API" --timeout=180s || true
else
  echo "deploy/$API does not exist yet (fresh server) - nothing to stop"
fi

say "3/6 Safety copy of the current database"
mkdir -p "$SAFETY_DIR"
if [ "$(printf "SELECT count(*) FROM pg_database WHERE datname = :'db';\n" | psql_pod postgres)" = 1 ]; then
  SAFETY="$SAFETY_DIR/$APP-before-restore-$(date +%Y%m%d-%H%M%S).dump"
  timeout 900 kubectl exec "$POD" -c postgres -- sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f /tmp/safety.dump' \
    || die "could not take a safety copy"
  fetch_from_pod /tmp/safety.dump "$SAFETY"
  echo "saved $SAFETY ($(stat -c %s "$SAFETY") bytes)"
else
  echo "database does not exist yet - no safety copy needed"
fi

say "4/6 Replacing the database"
STAGE=replacing
if [ "$FORMAT" = plain ]; then REMOTE=/tmp/restore.sql.gz; else REMOTE=/tmp/restore.dump; fi
push_to_pod "$FILE" "$REMOTE"
printf '%s\n' 'DROP DATABASE IF EXISTS :"db" WITH (FORCE);' 'CREATE DATABASE :"db" OWNER :"owner";' | psql_pod postgres
kubectl exec "$POD" -c postgres -- rm -f /tmp/restore.rc /tmp/restore.log
kubectl exec "$POD" -c postgres -- env FMT="$FORMAT" sh -c '
  if [ "$FMT" = plain ]; then
    gunzip -c /tmp/restore.sql.gz | psql -X -q -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" > /tmp/restore.log 2>&1
  else
    pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --no-acl --exit-on-error /tmp/restore.dump > /tmp/restore.log 2>&1
  fi
  echo $? > /tmp/restore.rc' &
pid=$!
for _ in $(seq 1 360); do
  timeout 60 kubectl exec "$POD" -c postgres -- test -f /tmp/restore.rc 2>/dev/null && break
  sleep 5
done
stop_bg "$pid"
RC=$(timeout 60 kubectl exec "$POD" -c postgres -- cat /tmp/restore.rc 2>/dev/null || echo "none")
LOG="$SAFETY_DIR/$APP-restore-$(date +%Y%m%d-%H%M%S).log"
timeout 60 kubectl exec "$POD" -c postgres -- cat /tmp/restore.log > "$LOG" 2>/dev/null || true
[ "$RC" = 0 ] || { tail -20 "$LOG"; die "restore failed (exit code $RC, full log: $LOG)"; }
kubectl exec "$POD" -c postgres -- rm -f "$REMOTE"

say "5/6 Checking the restored data"
COUNTS="$SAFETY_DIR/$APP-after-restore-$(date +%Y%m%d-%H%M%S).txt"
counts > "$COUNTS"
[ -s "$COUNTS" ] || die "the restored database has no tables"
awk '{n++; s+=$NF} END {print n " tables, " s " rows in total"}' "$COUNTS"
awk '{print $NF "\t" $0}' "$COUNTS" | sort -rn | head -8 | cut -f2- | sed 's/^/  /'

say "6/6 Starting the API"
STAGE=finished
if kubectl get deploy "$API" >/dev/null 2>&1; then
  kubectl scale "deploy/$API" --replicas="$REPLICAS"
  kubectl rollout status "deploy/$API" --timeout=300s
fi
argo_resume

say "DONE"
echo "Restored $APP from $(basename "$FILE")."
[ -z "$SAFETY" ] || echo "Data from before the restore: $SAFETY (delete it once you are happy)."
echo "Row counts: $COUNTS"
