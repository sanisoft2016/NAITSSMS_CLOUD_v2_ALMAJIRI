# K8s secret overlays for NAITS SMS instances (ASMS / Hills / Ama).
#
# Why: GitGuardian (correctly) flags DB passwords and JWT secrets committed in
# Naits*Manifest.yaml. Base64 in `data:` is not encryption.
#
# Workflow:
#   1. cp k8s/secrets/<Instance>.secrets.example.yaml k8s/secrets/<Instance>.secrets.local.yaml
#   2. Replace every REPLACE_ME with the real value (keep *.local.yaml off git).
#   3. kubectl apply -f k8s/secrets/<Instance>.secrets.local.yaml
#   4. kubectl apply -f k8s/<Instance>Manifest.yaml
#
# If these secrets were ever pushed to a remote, rotate Postgres passwords and
# JWT secrets on the VPS — removing them from the latest commit does not erase
# git history.
