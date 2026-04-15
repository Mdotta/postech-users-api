#!/bin/bash
set -euo pipefail

# =============================================================================
# setup-cognito.sh
# Creates a Cognito User Pool and App Client for postech-users-api.
# Outputs the UserPoolId and ClientId needed for appsettings and API Gateway.
#
# Usage:
#   ./infra/setup-cognito.sh
#
# Optional environment variables:
#   AWS_REGION      - Defaults to us-east-1
#   POOL_NAME       - Defaults to postech-users-pool
#   CLIENT_NAME     - Defaults to postech-api-client
# =============================================================================

AWS_REGION="${AWS_REGION:-us-east-1}"
POOL_NAME="${POOL_NAME:-postech-users-pool}"
CLIENT_NAME="${CLIENT_NAME:-postech-api-client}"

# --- Helpers -----------------------------------------------------------------
log()  { echo "[$(date '+%H:%M:%S')] $*"; }
ok()   { echo "[$(date '+%H:%M:%S')] ✅ $*"; }
fail() { echo "[$(date '+%H:%M:%S')] ❌ $*" >&2; exit 1; }

# --- Checks ------------------------------------------------------------------
command -v aws &>/dev/null || fail "aws CLI is not installed"
command -v jq  &>/dev/null || fail "jq is not installed"

# --- Step 1: Check if pool already exists ------------------------------------
log "Checking for existing User Pool '$POOL_NAME'..."

EXISTING_POOL_ID=$(aws cognito-idp list-user-pools \
  --max-results 60 \
  --region "$AWS_REGION" \
  --query "UserPools[?Name=='$POOL_NAME'].Id" \
  --output text)

if [[ -n "$EXISTING_POOL_ID" && "$EXISTING_POOL_ID" != "None" ]]; then
  log "User Pool '$POOL_NAME' already exists (ID: $EXISTING_POOL_ID), reusing it."
  USER_POOL_ID="$EXISTING_POOL_ID"
else
  # --- Step 2: Create User Pool ----------------------------------------------
  log "Creating Cognito User Pool '$POOL_NAME'..."

  USER_POOL_ID=$(aws cognito-idp create-user-pool \
    --region "$AWS_REGION" \
    --pool-name "$POOL_NAME" \
    --policies '{
      "PasswordPolicy": {
        "MinimumLength": 8,
        "RequireUppercase": true,
        "RequireLowercase": true,
        "RequireNumbers": true,
        "RequireSymbols": false,
        "TemporaryPasswordValidityDays": 7
      }
    }' \
    --auto-verified-attributes email \
    --username-attributes email \
    --username-configuration '{"CaseSensitive": false}' \
    --account-recovery-setting '{
      "RecoveryMechanisms": [
        {"Priority": 1, "Name": "verified_email"}
      ]
    }' \
    --query 'UserPool.Id' --output text)

  ok "User Pool created: $USER_POOL_ID"
fi

# --- Step 3: Check if App Client already exists ------------------------------
log "Checking for existing App Client '$CLIENT_NAME'..."

EXISTING_CLIENT_ID=$(aws cognito-idp list-user-pool-clients \
  --user-pool-id "$USER_POOL_ID" \
  --region "$AWS_REGION" \
  --query "UserPoolClients[?ClientName=='$CLIENT_NAME'].ClientId" \
  --output text)

if [[ -n "$EXISTING_CLIENT_ID" && "$EXISTING_CLIENT_ID" != "None" ]]; then
  log "App Client '$CLIENT_NAME' already exists (ID: $EXISTING_CLIENT_ID), reusing it."
  CLIENT_ID="$EXISTING_CLIENT_ID"
else
  # --- Step 4: Create App Client ---------------------------------------------
  log "Creating App Client '$CLIENT_NAME'..."

  CLIENT_ID=$(aws cognito-idp create-user-pool-client \
    --region "$AWS_REGION" \
    --user-pool-id "$USER_POOL_ID" \
    --client-name "$CLIENT_NAME" \
    --no-generate-secret \
    --explicit-auth-flows \
      ALLOW_USER_PASSWORD_AUTH \
      ALLOW_REFRESH_TOKEN_AUTH \
      ALLOW_USER_SRP_AUTH \
    --token-validity-units '{"AccessToken":"hours","IdToken":"hours","RefreshToken":"days"}' \
    --access-token-validity 1 \
    --id-token-validity 1 \
    --refresh-token-validity 30 \
    --query 'UserPoolClient.ClientId' --output text)

  ok "App Client created: $CLIENT_ID"
fi

# --- Step 5: Create Cognito Groups for roles ---------------------------------
log "Creating Cognito groups for role-based authorization..."

create_group_if_not_exists() {
  local GROUP_NAME="$1"
  local DESCRIPTION="$2"

  EXISTING=$(aws cognito-idp get-group \
    --user-pool-id "$USER_POOL_ID" \
    --group-name "$GROUP_NAME" \
    --region "$AWS_REGION" \
    --query 'Group.GroupName' \
    --output text 2>/dev/null) || EXISTING=""

  if [[ -n "$EXISTING" && "$EXISTING" != "None" ]]; then
    log "Group '$GROUP_NAME' already exists, skipping."
  else
    aws cognito-idp create-group \
      --user-pool-id "$USER_POOL_ID" \
      --group-name "$GROUP_NAME" \
      --description "$DESCRIPTION" \
      --region "$AWS_REGION" > /dev/null
    ok "Group '$GROUP_NAME' created"
  fi
}
