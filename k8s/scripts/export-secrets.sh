#!/usr/bin/env bash
# Writes every Secret the apps need (database passwords, JWT keys, SMTP, Docker Hub pull secret,
# Google Drive rclone config, ArgoCD's GitHub access) into ONE file you keep OFF the server,
# e.g. as an attachment in your password manager. rebuild-server.sh applies that file.
#
#   bash export-secrets.sh            -> /root/secrets-bundle-<date>.json (readable by root only)
#
# Download it, store it safely, then delete it from the server:  shred -u /root/secrets-bundle-*.json
set -euo pipefail

OUT=/root/secrets-bundle-$(date +%Y%m%d).json
SECRETS=(
  default/postgres-secret
  default/custrewardmgtsys-v3-api-secrets
  default/postgres-secret-naitsasms
  default/postgres-secret-naitssmshills
  default/postgres-secret-naitssmsama
  default/naitsasms-api-secrets
  default/naitssmshills-api-secrets
  default/naitssmsama-api-secrets
  default/dockerhub-sanibesiru
  default/rclone-config-secret
)
# ArgoCD's GitHub credential has a generated name; find it by its label.
mapfile -t ARGO_REPOS < <(kubectl -n argocd get secret -l argocd.argoproj.io/secret-type=repository -o name 2>/dev/null | sed 's|^secret/|argocd/|')

umask 077
{
  for s in "${SECRETS[@]}" "${ARGO_REPOS[@]}"; do
    kubectl -n "${s%%/*}" get secret "${s#*/}" -o json
  done
} | python3 -c '
import json, sys
dec = json.JSONDecoder()
text, items, i = sys.stdin.read(), [], 0
while i < len(text):
    while i < len(text) and text[i].isspace():
        i += 1
    if i >= len(text):
        break
    obj, i = dec.raw_decode(text, i)
    md = obj["metadata"]
    keep = {"name": md["name"], "namespace": md["namespace"]}
    labels = {k: v for k, v in (md.get("labels") or {}).items() if k.startswith("argocd.argoproj.io/")}
    if labels:
        keep["labels"] = labels
    items.append({"apiVersion": "v1", "kind": "Secret", "metadata": keep, "type": obj.get("type", "Opaque"), "data": obj.get("data", {})})
json.dump({"apiVersion": "v1", "kind": "List", "items": items}, sys.stdout, indent=2)
print()
print(f"{len(items)} secrets exported", file=sys.stderr)
' > "$OUT"

echo "Written: $OUT"
echo "Download it (laptop PowerShell):  scp root@<VPS-IP>:$OUT ."
echo "Then remove it from the server:   shred -u $OUT"
