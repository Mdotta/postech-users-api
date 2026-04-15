#!/bin/bash
set -euo pipefail

# =============================================================================
# deploy-ecr.sh
# Builds the Docker image, authenticates with AWS ECR, and pushes the latest
# image. Run from the repository root.
#
# Usage:
#   ./infra/deploy-ecr.sh
#
# Required environment variables (or set them in the script below):
#   AWS_ACCOUNT_ID  - Your AWS account ID (e.g. 123456789012)
#   AWS_REGION      - AWS region where ECR lives (e.g. us-east-1)
#   ECR_REPO        - ECR repository name (default: postech-users-api)
# =============================================================================

# --- Configuration -----------------------------------------------------------
AWS_ACCOUNT_ID="${AWS_ACCOUNT_ID:?❌ AWS_ACCOUNT_ID is not set}"
AWS_REGION="${AWS_REGION:-us-east-1}"
ECR_REPO="${ECR_REPO:-postech-users-api}"
IMAGE_TAG="${IMAGE_TAG:-latest}"

ECR_REGISTRY="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"
FULL_IMAGE="$ECR_REGISTRY/$ECR_REPO:$IMAGE_TAG"

# --- Helpers -----------------------------------------------------------------
log()  { echo "[$(date '+%H:%M:%S')] $*"; }
ok()   { echo "[$(date '+%H:%M:%S')] ✅ $*"; }
fail() { echo "[$(date '+%H:%M:%S')] ❌ $*" >&2; exit 1; }

# --- Checks ------------------------------------------------------------------
log "Checking dependencies..."
command -v docker &>/dev/null || fail "docker is not installed"
command -v aws    &>/dev/null || fail "aws CLI is not installed"

# Ensure we are running from the repo root (where Dockerfile lives)
[[ -f "Dockerfile" ]] || fail "Dockerfile not found. Run this script from the repository root."

# --- Step 1: Build -----------------------------------------------------------
log "Building Docker image..."
docker build \
  --target final \
  -t "$ECR_REPO:$IMAGE_TAG" \
  -t "$FULL_IMAGE" \
  -f Dockerfile \
  .
ok "Image built: $ECR_REPO:$IMAGE_TAG"

# --- Step 2: Authenticate with ECR -------------------------------------------
log "Authenticating Docker with ECR ($ECR_REGISTRY)..."
aws ecr get-login-password --region "$AWS_REGION" | \
  docker login --username AWS --password-stdin "$ECR_REGISTRY"
ok "Authenticated with ECR"

# --- Step 3: Push ------------------------------------------------------------
log "Pushing image to ECR..."
docker push "$FULL_IMAGE"
ok "Image pushed: $FULL_IMAGE"

# --- Done --------------------------------------------------------------------
echo ""
echo "🚀 Deploy complete!"
echo "   Image : $FULL_IMAGE"
echo "   Run 'aws ecr list-images --repository-name $ECR_REPO --region $AWS_REGION' to verify."