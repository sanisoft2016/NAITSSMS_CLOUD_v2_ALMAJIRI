# NAITS schools and whole-server recovery

This folder holds everything needed to run the three NAITS schools on the VPS, plus the jobs and
scripts that cover the whole server (off-site copies, backup alerts, full rebuild). Silkcoat runs on
the same server; its own files are in the Silkcoat repository under `k8s/`.

## What runs where

| School | Site | API NodePort | Host data folder | Backup |
|---|---|---|---|---|
| ASMS | ourschoolonline.ng | 30092 | `/sani/tmp/naittsasms/` | 02:00 |
| Hills | hills.ourschoolonline.ng | 30093 | `/sani/tmp/naitssmshills/` | 02:30 |
| Ama | ama.ourschoolonline.ng | 30094 | `/sani/tmp/naitssmsama/` | 03:00 |

Each data folder has `postgres/` (database), `wwwroot/` (passports, signatures, logos) and
`database-backups/` (nightly `.dump` files). The ASMS folder really is spelled `naittsasms`.

The Angular UI is not in Kubernetes: host nginx serves it from `/var/www/ourschoolonline`,
`/var/www/ourschoolonline-hills` and `/var/www/ourschoolonline-ama`, and proxies `/api/`, `/home/`
and the upload folders to the school's API NodePort.

| File | Purpose |
|---|---|
| `00-host-setup.sh` | creates the host folders; never touches existing ones |
| `secrets/*.secrets.example.yaml` | templates for each school's secrets (real copies are `*.local.yaml`, gitignored) |
| `NaitsAsmsManifest.yaml`, `NaitsSmsHillsManifest.yaml`, `NaitsSmsAmaManifest.yaml` | database, API, dashboard and nightly backup for each school |
| `backup-monitor.yaml` | 04:30 check of all four databases' backups (NAITS and Silkcoat); emails info@naits.com.ng on problems, and every Monday |
| `offsite-backup.yaml` | 03:45 copy of backups, uploads, websites and nginx config to Google Drive (`gdrive:vps-backups/`) |
| `nginx/` | `nginx.conf` and the three school sites |
| `scripts/restore-db.sh` | restore a backup into any of the four databases |
| `scripts/export-secrets.sh` | bundle every Secret into one file to keep off the server |
| `scripts/rebuild-server.sh` | rebuild the whole server on a new VPS |

Preview any change to a running server first: `kubectl diff -f k8s/<file>.yaml`.

## Keep these off the server (do it now, and again after changing any password)

1. **Secrets bundle.** On the VPS: `bash k8s/scripts/export-secrets.sh`. Download the file it
   prints, store it in your password manager, then `shred -u` it on the server. Without it, a
   rebuilt server cannot open the restored databases or pull the NAITS image.
2. **Off-site copy.** Check it ran: `kubectl get jobs | grep offsite`, then
   `kubectl logs job/<newest offsite-backup job>`; the log ends with `Off-site copy OK.`

## Everyday recovery

**Restore a school's database** (saves the current data first, stops that school's API, restores,
compares row counts, starts the API):

```bash
bash k8s/scripts/restore-db.sh naitsasms                  # list backups
bash k8s/scripts/restore-db.sh naitsasms latest
bash k8s/scripts/restore-db.sh naitssmshills db_a9f78f_hill2nd-20260930-104452.dump
```

**Uploaded files missing** (passports, signatures, logos):
`rclone copy gdrive:vps-backups/files/naitsasms-wwwroot /sani/tmp/naittsasms/wwwroot` then
`chown -R 1000:1000 /sani/tmp/naittsasms/wwwroot` (use the school's folder names).

**New API version.** All three schools run `sanibesiru/naitsasms-api:latest` with
`imagePullPolicy: Always`, so after pushing a new image:
`kubectl rollout restart deploy/naitsasms-api deploy/naitssmshills-api deploy/naitssmsama-api`.
Because `:latest` is overwritten, a bad release cannot be rolled back by tag; push the previous
build again.

**New UI version.** One build serves all three schools (it calls `/api` on whatever domain it is
opened on):

```powershell
cd NaitsSmsUI; npm run build:prod
scp -r dist/ng-matero/browser/* root@<VPS-IP>:/var/www/ourschoolonline/
```

Repeat the `scp` for `ourschoolonline-hills` and `ourschoolonline-ama`.

## Whole server lost: rebuild on a new VPS

You need: a new Ubuntu 24.04 VPS (2 vCPU / 8 GB / 100 GB like the current one), the secrets bundle,
access to the DNS of all the domains, and about two hours.

1. On the new VPS, get both repositories:
   ```bash
   apt-get update && apt-get install -y git
   git clone -b cloud-native https://github.com/sanisoft2016/NAITSSMS_CLOUD_v2.git /root/src/naits
   git clone https://github.com/sanisoft2016/CustRewardMgtSys.git /root/src/silkcoat
   ```
2. From your laptop, copy the secrets bundle up:
   `scp secrets-bundle-<date>.json root@<NEW-IP>:/root/secrets-bundle.json`
3. Run the stages one at a time and read each one's output before going on:
   ```bash
   cd /root/src/naits
   bash k8s/scripts/rebuild-server.sh base
   bash k8s/scripts/rebuild-server.sh k3s
   bash k8s/scripts/rebuild-server.sh folders
   bash k8s/scripts/rebuild-server.sh secrets    # also checks Google Drive is reachable
   bash k8s/scripts/rebuild-server.sh files      # uploads, websites, last 14 days of backups
   bash k8s/scripts/rebuild-server.sh apps
   bash k8s/scripts/rebuild-server.sh argocd
   bash k8s/scripts/rebuild-server.sh restore    # newest backup into all four databases
   ```
4. Point DNS at the new IP: `ourschoolonline.ng`, `hills.`, `ama.` (with `www.` variants),
   `promo.silkcoatnigeria.com`, `silkcoatnigeria.com`, `www.silkcoatnigeria.com`, `silkcoat.ng`,
   `www.silkcoat.ng`. Wait until `getent hosts ourschoolonline.ng` shows the new IP.
5. Finish:
   ```bash
   bash k8s/scripts/rebuild-server.sh certs
   bash k8s/scripts/rebuild-server.sh nginx
   bash k8s/scripts/rebuild-server.sh verify     # every site should answer 200 or 301
   ```

Not covered by the rebuild (they run on the current server outside Kubernetes and have no copy
here): musiadnigeria.org (musiad-backend and musiadnigeria services, pm2), iconapaints.com,
platinumpaint.com.ng, teejayschool.com, MariaDB, PHP-FPM, and the postfix/opendkim mail setup.

`rebuild-server.sh` has not yet been run end to end. Rehearse it on a cheap throwaway VPS (skip
the DNS step; check the sites with `curl --resolve <domain>:443:<NEW-IP> https://<domain>/`).
