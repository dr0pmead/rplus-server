#!/bin/bash
# ═══════════════════════════════════════════════════════════════════════════════
# RPlus k8s — Apply all manifests to k3s
# Usage: sudo k3s kubectl apply -k k8s/ OR bash k8s/apply.sh
# ═══════════════════════════════════════════════════════════════════════════════
set -e

KUBECTL="sudo k3s kubectl"
DIR="$(cd "$(dirname "$0")" && pwd)"

echo "═══ RPlus k8s Deploy ═══"

# 1. Namespace + shared resources
echo "→ Namespace & shared config..."
$KUBECTL apply -f "$DIR/namespace.yaml"

# 2. Infrastructure (order matters)
echo "→ Infrastructure..."
$KUBECTL apply -f "$DIR/infra/vault.yaml"
$KUBECTL apply -f "$DIR/infra/kafka.yaml"
$KUBECTL apply -f "$DIR/infra/minio.yaml"
$KUBECTL apply -f "$DIR/infra/otel.yaml"
$KUBECTL apply -f "$DIR/infra/traefik.yaml"

# 3. Wait for Vault to be ready
echo "→ Waiting for Vault..."
$KUBECTL rollout status deployment/kernel-vault -n rplus --timeout=120s || true

# 4. Services (apply all at once)
echo "→ Services..."
for f in "$DIR"/services/*.yaml; do
  echo "  applying $(basename $f)..."
  $KUBECTL apply -f "$f"
done

echo ""
echo "═══ Done! Check status: ═══"
echo "  sudo k3s kubectl get pods -n rplus"
echo "  sudo k3s kubectl get svc -n rplus"
