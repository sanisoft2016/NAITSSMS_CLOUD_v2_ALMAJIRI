#!/usr/bin/env bash
# Rebuilds Silkcoat and the three NAITS schools on a fresh Ubuntu 24.04 VPS, from git, the secrets
# bundle (export-secrets.sh) and the Google Drive off-site copy (offsite-backup.yaml).
# Follow k8s/RECOVERY.md; it explains the order and what to check between stages.
#
#   bash rebuild-server.sh <stage>      stages, in order:
#     base      apt packages, firewall
#     k3s       Kubernetes (k3s, Traefik disabled - host nginx does HTTP/HTTPS)
#     folders   host data folders
#     secrets   apply the secrets bundle; set up rclone for root
#     files     pull uploads, websites and recent database backups from Google Drive
#     apps      Postgres, backups, NAITS APIs, dashboards, monitor, off-site job
#     argocd    ArgoCD + the Silkcoat Application (deploys the Silkcoat API and UI)
#     restore   restore the newest backup into each of the four databases
#     certs     Let's Encrypt certificates (DNS must already point at this server)
#     nginx     install the site configs and reload nginx
#     verify    show pods and check every site answers over HTTPS
#   bash rebuild-server.sh all         everything above, in order
#
# Every stage can be re-run safely.
set -euo pipefail

NAITS_DIR=${NAITS_DIR:-/root/src/naits}
SILKCOAT_DIR=${SILKCOAT_DIR:-/root/src/silkcoat}
SECRETS=${SECRETS:-/root/secrets-bundle.json}
SERVER_IP=${SERVER_IP:-$(hostname -I | awk '{print $1}')}
EMAIL=${EMAIL:-admin@ourschoolonline.ng}
K3S_VERSION=${K3S_VERSION:-v1.32.3+k3s1}
ARGOCD_VERSION=${ARGOCD_VERSION:-v3.0.0}
REMOTE=${REMOTE:-gdrive:vps-backups}

# cert name | domains  (the names match the ssl_certificate paths in the nginx configs)
CERTS=(
  "promo.silkcoatnigeria.com|promo.silkcoatnigeria.com silkcoat.ng www.silkcoat.ng"
  "silkcoatnigeria.com|silkcoatnigeria.com www.silkcoatnigeria.com"
  "ourschoolonline.ng|ourschoolonline.ng www.ourschoolonline.ng"
  "hills.ourschoolonline.ng|hills.ourschoolonline.ng www.hills.ourschoolonline.ng"
  "ama.ourschoolonline.ng|ama.ourschoolonline.ng"
)

# Google Drive folder | host folder | owner   (same layout as k8s/offsite-backup.yaml)
FILES=(
  "files/silkcoat-uploads|/sani/tmp/file-upload|1000"
  "files/naitsasms-wwwroot|/sani/tmp/naittsasms/wwwroot|1000"
  "files/naitssmshills-wwwroot|/sani/tmp/naitssmshills/wwwroot|1000"
  "files/naitssmsama-wwwroot|/sani/tmp/naitssmsama/wwwroot|1000"
  "www/silkcoatnigeria|/var/www/silkcoatnigeria|0"
  "www/ourschoolonline|/var/www/ourschoolonline|0"
  "www/ourschoolonline-hills|/var/www/ourschoolonline-hills|0"
  "www/ourschoolonline-ama|/var/www/ourschoolonline-ama|0"
  "db/silkcoat|/sani/tmp/database-backups|999"
  "db/naitsasms|/sani/tmp/naittsasms/database-backups|999"
  "db/naitssmshills|/sani/tmp/naitssmshills/database-backups|999"
  "db/naitssmsama|/sani/tmp/naitssmsama/database-backups|999"
)

say() { printf '\n=== %s\n' "$*"; }
die() { printf '\n!!! STOPPED: %s\n' "$*" >&2; exit 1; }
need() { [ -e "$1" ] || die "missing $1 - $2"; }

stage_base() {
  say "base: packages and firewall"
  apt-get update
  DEBIAN_FRONTEND=noninteractive apt-get install -y nginx certbot python3-certbot-nginx rclone ufw curl python3
  ufw allow 22/tcp
  ufw allow 80/tcp
  ufw allow 443/tcp
  ufw --force enable
  ufw status
}

stage_k3s() {
  say "k3s $K3S_VERSION on $SERVER_IP"
  # ufw does not filter NodePorts (k3s routes them before ufw sees them), so bind
  # NodePorts to localhost only; host nginx reaches the apps via 127.0.0.1.
  mkdir -p /etc/rancher/k3s
  if ! grep -qs 'nodeport-addresses' /etc/rancher/k3s/config.yaml; then
    printf 'kube-proxy-arg:\n  - nodeport-addresses=127.0.0.1/32\n' >> /etc/rancher/k3s/config.yaml
  fi
  if ! command -v k3s >/dev/null; then
    curl -sfL https://get.k3s.io | INSTALL_K3S_VERSION="$K3S_VERSION" \
      INSTALL_K3S_EXEC="server --disable traefik --node-ip=$SERVER_IP --node-external-ip=$SERVER_IP --tls-san $SERVER_IP" sh -
  fi
  for _ in $(seq 1 60); do
    kubectl get nodes 2>/dev/null | grep -q ' Ready' && break
    sleep 5
  done
  kubectl get nodes
}

stage_folders() {
  say "host folders"
  bash "$SILKCOAT_DIR/k8s/00-host-setup.sh"
  bash "$NAITS_DIR/k8s/00-host-setup.sh"
}

stage_secrets() {
  say "secrets"
  need "$SECRETS" "copy the secrets bundle from your password manager to this path"
  kubectl create namespace argocd --dry-run=client -o yaml | kubectl apply -f -
  kubectl apply -f "$SECRETS"
  install -d -m 700 /root/.config/rclone
  kubectl get secret rclone-config-secret -o jsonpath='{.data.rclone\.conf}' | base64 -d > /root/.config/rclone/rclone.conf
  chmod 600 /root/.config/rclone/rclone.conf
  rclone lsd "${REMOTE%%:*}:" >/dev/null || die "rclone cannot reach Google Drive (token expired? run: rclone config reconnect ${REMOTE%%:*}:)"
  echo "Google Drive reachable."
}

stage_files() {
  say "files from $REMOTE"
  local entry src dst owner
  for entry in "${FILES[@]}"; do
    IFS='|' read -r src dst owner <<< "$entry"
    echo "--- $src -> $dst"
    install -d "$dst"
    case "$src" in
      db/*) rclone copy "$REMOTE/$src" "$dst" --max-age 14d ;;
      *)    rclone copy "$REMOTE/$src" "$dst" ;;
    esac
    [ "$owner" = 0 ] || chown -R "$owner:$owner" "$dst"
  done
}

stage_apps() {
  say "Kubernetes apps"
  kubectl apply -f "$SILKCOAT_DIR/k8s/SilkcoatManifest.yaml"
  kubectl apply -f "$NAITS_DIR/k8s/NaitsAsmsManifest.yaml" -f "$NAITS_DIR/k8s/NaitsSmsHillsManifest.yaml" \
                -f "$NAITS_DIR/k8s/NaitsSmsAmaManifest.yaml"
  kubectl apply -f "$NAITS_DIR/k8s/backup-monitor.yaml" -f "$NAITS_DIR/k8s/offsite-backup.yaml"
  for pod in postgres-deployment-0 postgres-naitsasms-0 postgres-naitssmshills-0 postgres-naitssmsama-0; do
    for _ in $(seq 1 60); do kubectl get pod "$pod" >/dev/null 2>&1 && break; sleep 5; done
    kubectl wait --for=condition=Ready "pod/$pod" --timeout=600s
  done
}

stage_argocd() {
  say "ArgoCD $ARGOCD_VERSION"
  kubectl create namespace argocd --dry-run=client -o yaml | kubectl apply -f -
  kubectl apply -n argocd --server-side --force-conflicts \
    -f "https://raw.githubusercontent.com/argoproj/argo-cd/$ARGOCD_VERSION/manifests/install.yaml"
  kubectl -n argocd rollout status deploy/argocd-server --timeout=600s
  kubectl -n argocd rollout status deploy/argocd-repo-server --timeout=600s
  kubectl apply -f "$SILKCOAT_DIR/k8s/argocd-app.yaml"
  echo "Waiting for ArgoCD to deploy the Silkcoat API and UI..."
  for _ in $(seq 1 60); do
    kubectl get deploy custrewardmgtsys-v3-api custrewardmgtsys-v3-ui >/dev/null 2>&1 && break
    sleep 10
  done
  kubectl rollout status deploy/custrewardmgtsys-v3-ui --timeout=600s
  kubectl rollout status deploy/custrewardmgtsys-v3-api --timeout=600s
}

stage_restore() {
  say "restore databases"
  for app in silkcoat naitsasms naitssmshills naitssmsama; do
    YES=1 bash "$NAITS_DIR/k8s/scripts/restore-db.sh" "$app" latest
  done
}

stage_certs() {
  say "certificates"
  local entry name domains d ip bad=""
  for entry in "${CERTS[@]}"; do
    for d in ${entry#*|}; do
      ip=$(getent ahostsv4 "$d" | awk 'NR==1 {print $1}')
      [ "$ip" = "$SERVER_IP" ] || bad="$bad $d(${ip:-no record})"
    done
  done
  if [ -n "$bad" ] && [ "${FORCE:-}" != 1 ]; then
    die "these names do not point at $SERVER_IP yet:$bad - update DNS, wait, re-run (FORCE=1 to skip names that fail)"
  fi
  systemctl start nginx
  for entry in "${CERTS[@]}"; do
    name=${entry%%|*}
    domains=()
    for d in ${entry#*|}; do domains+=(-d "$d"); done
    certbot certonly --nginx --non-interactive --agree-tos -m "$EMAIL" --keep-until-expiring \
      --cert-name "$name" "${domains[@]}" || [ "${FORCE:-}" = 1 ]
  done
}

stage_nginx() {
  say "nginx sites"
  install -m 644 "$NAITS_DIR/k8s/nginx/nginx.conf" /etc/nginx/nginx.conf
  local f
  for f in "$NAITS_DIR"/k8s/nginx/*ourschoolonline.ng "$SILKCOAT_DIR"/k8s/nginx/*; do
    install -m 644 "$f" "/etc/nginx/sites-available/$(basename "$f")"
    ln -sf "/etc/nginx/sites-available/$(basename "$f")" "/etc/nginx/sites-enabled/$(basename "$f")"
  done
  nginx -t
  systemctl reload nginx
}

stage_verify() {
  say "pods"
  kubectl get pods -o wide
  say "sites"
  local entry d code
  for entry in "${CERTS[@]}"; do
    for d in ${entry#*|}; do
      code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "https://$d/" || echo "---")
      printf '%-32s %s\n' "$d" "$code"
    done
  done
}

STAGES=(base k3s folders secrets files apps argocd restore certs nginx verify)
case "${1:-}" in
  all) for s in "${STAGES[@]}"; do "stage_$s"; done ;;
  base|k3s|folders|secrets|files|apps|argocd|restore|certs|nginx|verify) "stage_$1" ;;
  *) sed -n '2,20p' "$0"; exit 2 ;;
esac
