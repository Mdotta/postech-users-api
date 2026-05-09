#!/bin/bash
set -euo pipefail

# =============================================================================
# deploy-ecr.sh
# Builds the Docker image for linux/amd64, authenticates with AWS ECR,
# and pushes the latest image. Run from the repository root.
#
# Usage:
#   ./deploy-ecr.sh
#
# Required environment variables:
#   AWS_ACCOUNT_ID  - Your AWS account ID (e.g. 123456789012)
#
# Optional environment variables:
#   AWS_REGION      - AWS region where ECR lives (default: us-east-1)
#   ECR_REPO        - ECR repository name (default: tf-postech-postech-users-api)
#   IMAGE_TAG       - Image tag (default: latest)
# =============================================================================

AWS_ACCOUNT_ID="${AWS_ACCOUNT_ID:?❌ AWS_ACCOUNT_ID is not set}"
AWS_REGION="${AWS_REGION:-us-east-1}"
ECR_REPO="${ECR_REPO:-tf-postech-postech-users-api}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
PLATFORM="linux/amd64"

ECR_REGISTRY="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"
FULL_IMAGE="$ECR_REGISTRY/$ECR_REPO:$IMAGE_TAG"

log()  { echo "[$(date '+%H:%M:%S')] $*"; }
ok()   { echo "[$(date '+%H:%M:%S')] ✅ $*"; }
fail() { echo "[$(date '+%H:%M:%S')] ❌ $*" >&2; exit 1; }

log "Checking dependencies..."
command -v docker &>/dev/null || fail "docker is not installed"
command -v aws    &>/dev/null || fail "aws CLI is not installed"
[[ -f "Dockerfile" ]] || fail "Dockerfile not found. Run this script from the repository root."

# --- Step 1: Ensure buildx builder is available ------------------------------
log "Setting up Docker buildx for $PLATFORM..."
if ! docker buildx inspect postech-builder &>/dev/null; then
  docker buildx create --name postech-builder --use
  ok "buildx builder 'postech-builder' created"
else
  docker buildx use postech-builder
  log "Reusing existing buildx builder 'postech-builder'"
fi

# --- Step 2: Authenticate with ECR -------------------------------------------
log "Authenticating Docker with ECR ($ECR_REGISTRY)..."
aws ecr get-login-password --region "$AWS_REGION" | \
  docker login --username AWS --password-stdin "$ECR_REGISTRY"
ok "Authenticated with ECR"

# --- Step 3: Verify ECR repository exists (created by Terraform) ---------------
log "Verifying ECR repository '$ECR_REPO' exists..."
aws ecr describe-repositories --repository-names "$ECR_REPO" --region "$AWS_REGION" &>/dev/null || \
  fail "ECR repository '$ECR_REPO' not found. Run 'terraform apply' first to create it."
ok "ECR repository ready: $ECR_REPO"

# --- Step 4: Build and push --------------------------------------------------
log "Building and pushing image for $PLATFORM..."
docker buildx build \
  --platform "$PLATFORM" \
  --target final \
  -t "$FULL_IMAGE" \
  -f Dockerfile \
  --push \
  .
ok "Image built and pushed: $FULL_IMAGE"

echo ""
echo "🚀 ECR deploy complete!"
echo "   Image    : $FULL_IMAGE"
echo "   Platform : $PLATFORM"
echo ""
echo "   Verify: aws ecr list-images --repository-name $ECR_REPO --region $AWS_REGION"
echo ""
echo "📋 Redeploy EC2 next:"
echo "   AWS_ACCOUNT_ID=\"$AWS_ACCOUNT_ID\" \\"
echo "   DB_CONNECTION_STRING=\"<your-db-connection-string>\" \\"
echo "   SNS_TOPIC_ARN=\"<your-sns-topic-arn>\" \\"
echo "   COGNITO_USER_POOL_ID=\"<your-user-pool-id>\" \\"
echo "   COGNITO_CLIENT_ID=\"<your-client-id>\" \\"
echo "   ./deploy-ec2.sh"