#!/usr/bin/env bash
# Creates the VPS host folders the three NAITS schools keep their data in. Safe to re-run:
# folders that already exist are left exactly as they are.
set -euo pipefail

make_dir() {  # path owner mode
  if [ -d "$1" ]; then echo "exists:  $1"; else install -d -o "$2" -g "$2" -m "$3" "$1"; echo "created: $1"; fi
}

# The ASMS folder really is spelled "naittsasms" on the server; the manifests use that path.
for school in naittsasms naitssmshills naitssmsama; do
  make_dir "/sani/tmp/$school"                   0    755
  make_dir "/sani/tmp/$school/postgres"          999  700   # PostgreSQL data (uid 999 = postgres)
  make_dir "/sani/tmp/$school/database-backups"  999  755   # nightly .dump backups
  make_dir "/sani/tmp/$school/wwwroot"           1000 2775  # passports, signatures, logos (API uid 1000)
done

# Angular UI, served by host nginx (see k8s/nginx/)
for site in ourschoolonline ourschoolonline-hills ourschoolonline-ama; do
  make_dir "/var/www/$site" 0 755
done
