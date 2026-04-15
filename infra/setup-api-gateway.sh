#!/bin/bash
set -euo pipefail

# =============================================================================
# setup-api-gateway.sh
# Creates an HTTP API Gateway with Cognito JWT authorization in front of the
# EC2 instance running postech-users-api.
# Safe to re-run — skips already existing resources.
#
# Usage:
#   ./setup-api-gateway.sh
#
# Required environment variables:
#   AWS_ACCOUNT_ID  - Your AWS account ID
#   JWT_ISSUER      - Cognito issuer URL
#   JWT_AUDIENCE    - Cognito App Client ID
#
# Optional:
#   AWS_REGION      - Defaults to us-east-1
#   API_NAME        - Defaults to postech-users-gateway
#   STAGE_NAME      - Defaults to prod
# =============================================================================

AWS_ACCOUNT_ID="${AWS_ACCOUNT_ID:?❌ AWS_ACCOUNT_ID is not set}"
JWT_ISSUER="${JWT_ISSUER:?❌ JWT_ISSUER is not set}"
JWT_AUDIENCE="${JWT_AUDIENCE:?❌ JWT_AUDIENCE is not set}"

AWS_REGION="${AWS_REGION:-us-east-1}"
API_NAME="${API_NAME:-postech-users-gateway}"
STAGE_NAME="${STAGE_NAME:-prod}"

log()  { echo "[$(date '+%H:%M:%S')] $*"; }
ok()   { echo "[$(date '+%H:%M:%S')] ✅ $*"; }
fail() { echo "[$(date '+%H:%M:%S')] ❌ $*" >&2; exit 1; }

command -v aws &>/dev/null || fail "aws CLI is not installed"

# --- Resolve EC2 public IP ---------------------------------------------------
log "Resolving EC2 public IP..."
EC2_IP=$(aws ec2 describe-instances \
  --region "$AWS_REGION" \
  --filters "Name=tag:Name,Values=postech-users-api" \
            "Name=instance-state-name,Values=running" \
  --query 'Reservations[0].Instances[0].PublicIpAddress' \
  --output text)

[[ "$EC2_IP" == "None" || -z "$EC2_IP" ]] && fail "No running EC2 instance tagged 'postech-users-api' found."
ok "EC2 IP: $EC2_IP"

log "JWT Issuer  : $JWT_ISSUER"
log "JWT Audience: $JWT_AUDIENCE"

# --- Step 1: Create or reuse HTTP API ----------------------------------------
log "Checking for existing API Gateway '$API_NAME'..."
EXISTING_API_ID=$(aws apigatewayv2 get-apis \
  --region "$AWS_REGION" \
  --query "Items[?Name=='$API_NAME'].ApiId" \
  --output text)

if [[ -n "$EXISTING_API_ID" && "$EXISTING_API_ID" != "None" ]]; then
  log "API '$API_NAME' already exists (ID: $EXISTING_API_ID), reusing it."
  API_ID="$EXISTING_API_ID"
else
  API_ID=$(aws apigatewayv2 create-api \
    --region "$AWS_REGION" \
    --name "$API_NAME" \
    --protocol-type HTTP \
    --query 'ApiId' --output text)
  ok "API created: $API_ID"
  sleep 5
fi

# --- Step 2: Create or reuse JWT Authorizer ----------------------------------
log "Checking for existing JWT Authorizer..."
EXISTING_AUTHORIZER_ID=$(aws apigatewayv2 get-authorizers \
  --api-id "$API_ID" \
  --region "$AWS_REGION" \
  --query "Items[?Name=='JwtAuthorizer'].AuthorizerId" \
  --output text)

if [[ -n "$EXISTING_AUTHORIZER_ID" && "$EXISTING_AUTHORIZER_ID" != "None" ]]; then
  log "Authorizer already exists (ID: $EXISTING_AUTHORIZER_ID), reusing it."
  AUTHORIZER_ID="$EXISTING_AUTHORIZER_ID"
else
  MAX_RETRIES=5
  RETRY_DELAY=10
  ATTEMPT=1
  while [[ $ATTEMPT -le $MAX_RETRIES ]]; do
    log "Attempt $ATTEMPT/$MAX_RETRIES to create authorizer..."
    AUTHORIZER_ID=$(aws apigatewayv2 create-authorizer \
      --region "$AWS_REGION" \
      --api-id "$API_ID" \
      --authorizer-type JWT \
      --name JwtAuthorizer \
      --identity-source '$request.header.Authorization' \
      --jwt-configuration "{\"Audience\":[\"$JWT_AUDIENCE\"],\"Issuer\":\"$JWT_ISSUER\"}" \
      --query 'AuthorizerId' --output text 2>&1) && break
    sleep $RETRY_DELAY
    ATTEMPT=$((ATTEMPT + 1))
  done
  [[ $ATTEMPT -gt $MAX_RETRIES ]] && fail "Failed to create authorizer after $MAX_RETRIES attempts."
  ok "Authorizer created: $AUTHORIZER_ID"
fi

# --- Step 3: Create or update integration ------------------------------------
# Always updates to the current EC2 IP
log "Setting up HTTP integration → http://$EC2_IP..."
EXISTING_INTEGRATION_ID=$(aws apigatewayv2 get-integrations \
  --api-id "$API_ID" \
  --region "$AWS_REGION" \
  --query 'Items[0].IntegrationId' \
  --output text)

if [[ -n "$EXISTING_INTEGRATION_ID" && "$EXISTING_INTEGRATION_ID" != "None" ]]; then
  log "Integration exists ($EXISTING_INTEGRATION_ID), updating URI to current EC2 IP..."
  aws apigatewayv2 update-integration \
    --api-id "$API_ID" \
    --integration-id "$EXISTING_INTEGRATION_ID" \
    --integration-uri "http://$EC2_IP/{proxy}" \
    --region "$AWS_REGION" > /dev/null
  INTEGRATION_ID="$EXISTING_INTEGRATION_ID"
  ok "Integration updated → http://$EC2_IP/{proxy}"
else
  INTEGRATION_ID=$(aws apigatewayv2 create-integration \
    --region "$AWS_REGION" \
    --api-id "$API_ID" \
    --integration-type HTTP_PROXY \
    --integration-uri "http://$EC2_IP/{proxy}" \
    --integration-method ANY \
    --payload-format-version "1.0" \
    --query 'IntegrationId' --output text)
  ok "Integration created: $INTEGRATION_ID"
fi

# --- Step 4: Create routes ---------------------------------------------------
log "Creating routes..."

create_route_if_not_exists() {
  local ROUTE_KEY="$1"
  local AUTH_TYPE="$2"

  EXISTING=$(aws apigatewayv2 get-routes \
    --api-id "$API_ID" \
    --region "$AWS_REGION" \
    --query "Items[?RouteKey=='$ROUTE_KEY'].RouteId" \
    --output text)

  if [[ -n "$EXISTING" && "$EXISTING" != "None" ]]; then
    log "Route '$ROUTE_KEY' already exists, skipping."
    return
  fi

  if [[ "$AUTH_TYPE" == "JWT" ]]; then
    aws apigatewayv2 create-route \
      --region "$AWS_REGION" \
      --api-id "$API_ID" \
      --route-key "$ROUTE_KEY" \
      --authorization-type JWT \
      --authorizer-id "$AUTHORIZER_ID" \
      --target "integrations/$INTEGRATION_ID" > /dev/null
  else
    aws apigatewayv2 create-route \
      --region "$AWS_REGION" \
      --api-id "$API_ID" \
      --route-key "$ROUTE_KEY" \
      --target "integrations/$INTEGRATION_ID" > /dev/null
  fi
  ok "Route created: $ROUTE_KEY ($AUTH_TYPE)"
}

# Public routes (no auth)
create_route_if_not_exists "POST /api/auth/{proxy+}"   "NONE"
create_route_if_not_exists "GET /health"               "NONE"

# Protected routes (JWT required)
create_route_if_not_exists "GET /api/users/{proxy+}"    "JWT"
create_route_if_not_exists "PATCH /api/users/{proxy+}"  "JWT"  # UpdateUserStatus
create_route_if_not_exists "PUT /api/users/{proxy+}"    "JWT"
create_route_if_not_exists "DELETE /api/users/{proxy+}" "JWT"

# --- Step 5: Create stage ----------------------------------------------------
log "Checking stage '$STAGE_NAME'..."
EXISTING_STAGE=$(aws apigatewayv2 get-stages \
  --api-id "$API_ID" \
  --region "$AWS_REGION" \
  --query "Items[?StageName=='$STAGE_NAME'].StageName" \
  --output text)

if [[ -n "$EXISTING_STAGE" && "$EXISTING_STAGE" != "None" ]]; then
  log "Stage '$STAGE_NAME' already exists, skipping."
else
  aws apigatewayv2 create-stage \
    --region "$AWS_REGION" \
    --api-id "$API_ID" \
    --stage-name "$STAGE_NAME" \
    --auto-deploy > /dev/null
  ok "Stage '$STAGE_NAME' created with auto-deploy"
fi

# --- Done --------------------------------------------------------------------
API_ENDPOINT=$(aws apigatewayv2 get-api \
  --api-id "$API_ID" \
  --region "$AWS_REGION" \
  --query 'ApiEndpoint' --output text)

INVOKE_URL="$API_ENDPOINT/$STAGE_NAME"

echo ""
echo "🚀 API Gateway setup complete!"
echo ""
echo "   API ID      : $API_ID"
echo "   Invoke URL  : $INVOKE_URL"
echo "   JWT Issuer  : $JWT_ISSUER"
echo "   JWT Audience: $JWT_AUDIENCE"
echo "   EC2 IP      : $EC2_IP"
echo ""
echo "📋 Test commands:"
echo ""
echo "  # Health check (public)"
echo "  curl $INVOKE_URL/health"
echo ""
echo "  # Register (public)"
echo "  curl -X POST $INVOKE_URL/api/auth/register \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -d '{\"email\":\"test@test.com\",\"password\":\"Test@1234\",\"name\":\"Test\"}'"
echo ""
echo "  # Login"
echo "  TOKEN=\$(curl -s -X POST $INVOKE_URL/api/auth/login \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -d '{\"email\":\"test@test.com\",\"password\":\"Test@1234\"}' | jq -r '.token')"
echo ""
echo "  # Get current user (protected)"
echo "  curl $INVOKE_URL/api/users/me \\"
echo "    -H \"Authorization: Bearer \$TOKEN\""