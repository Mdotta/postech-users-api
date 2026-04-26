#!/bin/bash
set -euo pipefail

# =============================================================================
# deploy-ec2.sh
# Launches an EC2 instance running the postech-users-api container.
# After launch, automatically updates the RDS security group and API Gateway
# integration with the new EC2 IP.
#
# Usage:
#   ./deploy-ec2.sh
#
# Required environment variables:
#   AWS_ACCOUNT_ID        - Your AWS account ID
#   DB_CONNECTION_STRING  - Full PostgreSQL connection string
#                           (set automatically by setup-rds.sh via deployment.env)
#   SNS_TOPIC_ARN         - ARN of the SNS user-created topic
#                           (or set USERS_SNS_TOPIC_ARN from deployment.env)
#   COGNITO_USER_POOL_ID  - Cognito User Pool ID
#                           (set automatically by setup-cognito.sh via deployment.env)
#   COGNITO_CLIENT_ID     - Cognito App Client ID
#                           (set automatically by setup-cognito.sh via deployment.env)
#
# Optional:
#   AWS_REGION            - Defaults to us-east-1
#   INSTANCE_TYPE         - Defaults to t3.micro
#   KEY_NAME              - Defaults to postech-key
#   API_NAME              - API Gateway name (default: postech-gateway)
#   RDS_INSTANCE_ID       - RDS instance identifier (default: postech-db)
#   EC2_SG_NAME           - EC2 security group name (default: postech-api-sg)
# =============================================================================

AWS_ACCOUNT_ID="${AWS_ACCOUNT_ID:?❌ AWS_ACCOUNT_ID is not set}"
DB_CONNECTION_STRING="${DB_CONNECTION_STRING:?❌ DB_CONNECTION_STRING is not set}"
SNS_TOPIC_ARN="${SNS_TOPIC_ARN:-${USERS_SNS_TOPIC_ARN:-}}"
SNS_TOPIC_ARN="${SNS_TOPIC_ARN:?❌ SNS_TOPIC_ARN or USERS_SNS_TOPIC_ARN is not set}"
COGNITO_USER_POOL_ID="${COGNITO_USER_POOL_ID:?❌ COGNITO_USER_POOL_ID is not set}"
COGNITO_CLIENT_ID="${COGNITO_CLIENT_ID:?❌ COGNITO_CLIENT_ID is not set}"

AWS_REGION="${AWS_REGION:-us-east-1}"
INSTANCE_TYPE="${INSTANCE_TYPE:-t3.micro}"
KEY_NAME="${KEY_NAME:-postech-key}"
API_NAME="${API_NAME:-postech-gateway}"
RDS_INSTANCE_ID="${RDS_INSTANCE_ID:-postech-db}"
EC2_SG_NAME="${EC2_SG_NAME:-postech-api-sg}"

ECR_REGISTRY="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"
IMAGE="$ECR_REGISTRY/postech-users-api:latest"

log()  { echo "[$(date '+%H:%M:%S')] $*"; }
ok()   { echo "[$(date '+%H:%M:%S')] ✅ $*"; }
warn() { echo "[$(date '+%H:%M:%S')] ⚠️  $*"; }
fail() { echo "[$(date '+%H:%M:%S')] ❌ $*" >&2; exit 1; }

command -v aws &>/dev/null || fail "aws CLI is not installed"

# --- Step 1: Get latest Amazon Linux 2023 AMI --------------------------------
log "Resolving latest Amazon Linux 2023 AMI..."
AMI_ID=$(aws ec2 describe-images \
  --region "$AWS_REGION" \
  --owners amazon \
  --filters "Name=name,Values=al2023-ami-*-x86_64" \
            "Name=state,Values=available" \
  --query 'sort_by(Images, &CreationDate)[-1].ImageId' \
  --output text)
ok "AMI: $AMI_ID"

# --- Step 2: Create key pair if it doesn't exist -----------------------------
log "Checking key pair '$KEY_NAME'..."
KEY_EXISTS=$(aws ec2 describe-key-pairs \
  --region "$AWS_REGION" \
  --key-names "$KEY_NAME" \
  --query 'KeyPairs[0].KeyName' \
  --output text 2>/dev/null) || KEY_EXISTS=""

if [[ -z "$KEY_EXISTS" || "$KEY_EXISTS" == "None" ]]; then
  log "Creating key pair '$KEY_NAME'..."
  aws ec2 create-key-pair \
    --region "$AWS_REGION" \
    --key-name "$KEY_NAME" \
    --query 'KeyMaterial' \
    --output text > "${KEY_NAME}.pem"
  chmod 400 "${KEY_NAME}.pem"
  ok "Key pair created and saved to ${KEY_NAME}.pem"
else
  log "Key pair '$KEY_NAME' already exists, skipping."
fi

# --- Step 3: Terminate existing instance -------------------------------------
log "Checking for existing EC2 instance..."
EXISTING_INSTANCE_ID=$(aws ec2 describe-instances \
  --region "$AWS_REGION" \
  --filters "Name=tag:Name,Values=postech-users-api" \
            "Name=instance-state-name,Values=running,pending,stopped" \
  --query 'Reservations[0].Instances[0].InstanceId' \
  --output text)

if [[ -n "$EXISTING_INSTANCE_ID" && "$EXISTING_INSTANCE_ID" != "None" ]]; then
  log "Terminating existing instance ($EXISTING_INSTANCE_ID)..."
  aws ec2 terminate-instances \
    --region "$AWS_REGION" \
    --instance-ids "$EXISTING_INSTANCE_ID" > /dev/null
  aws ec2 wait instance-terminated \
    --region "$AWS_REGION" \
    --instance-ids "$EXISTING_INSTANCE_ID"
  ok "Old instance terminated"
fi

# --- Step 4: Write User Data script ------------------------------------------
log "Generating User Data script..."
USER_DATA=$(cat <<EOF
#!/bin/bash
set -euo pipefail

# Install Docker + AWS CLI (aws-cli already available on AL2023)
yum update -y
yum install -y docker aws-cli
systemctl start docker
systemctl enable docker

# Authenticate with ECR
aws ecr get-login-password --region "$AWS_REGION" | \
  docker login --username AWS --password-stdin "$ECR_REGISTRY"

# Pull and run the container
docker pull "$IMAGE"

docker run -d \
  --name postech-users-api \
  --restart unless-stopped \
  -p 80:80 \
  -e ASPNETCORE_URLS="http://+:80" \
  -e "ConnectionStrings__DefaultConnection=$DB_CONNECTION_STRING" \
  -e AWS__Region="$AWS_REGION" \
  -e AWS__SnsTopicArn="$SNS_TOPIC_ARN" \
  -e CognitoSettings__UserPoolId="$COGNITO_USER_POOL_ID" \
  -e CognitoSettings__ClientId="$COGNITO_CLIENT_ID" \
  -e CognitoSettings__Region="$AWS_REGION" \
  "$IMAGE"
EOF
)

# --- Step 5: Launch instance --------------------------------------------------
log "Launching EC2 instance..."
INSTANCE_ID=$(aws ec2 run-instances \
  --region "$AWS_REGION" \
  --image-id "$AMI_ID" \
  --instance-type "$INSTANCE_TYPE" \
  --iam-instance-profile Name=LabInstanceProfile \
  --security-groups "$EC2_SG_NAME" \
  --key-name "$KEY_NAME" \
  --user-data "$USER_DATA" \
  --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=postech-users-api}]" \
  --query 'Instances[0].InstanceId' \
  --output text)
ok "Instance launched: $INSTANCE_ID"

# --- Step 6: Wait for running + get IP ---------------------------------------
log "Waiting for instance to reach running state..."
aws ec2 wait instance-running \
  --region "$AWS_REGION" \
  --instance-ids "$INSTANCE_ID"

PUBLIC_IP=$(aws ec2 describe-instances \
  --region "$AWS_REGION" \
  --instance-ids "$INSTANCE_ID" \
  --query 'Reservations[0].Instances[0].PublicIpAddress' \
  --output text)

PRIVATE_IP=$(aws ec2 describe-instances \
  --region "$AWS_REGION" \
  --instance-ids "$INSTANCE_ID" \
  --query 'Reservations[0].Instances[0].PrivateIpAddress' \
  --output text)

ok "Instance running — Public: $PUBLIC_IP | Private: $PRIVATE_IP"

# --- Step 7: Update RDS security group with EC2 SG reference -----------------
# Uses SG-to-SG reference (stable across redeploys) instead of IP CIDR
log "Ensuring RDS allows traffic from EC2 security group..."

EC2_SG_ID=$(aws ec2 describe-security-groups \
  --group-names "$EC2_SG_NAME" \
  --region "$AWS_REGION" \
  --query 'SecurityGroups[0].GroupId' --output text 2>/dev/null) || EC2_SG_ID=""

RDS_SG_ID=$(aws rds describe-db-instances \
  --db-instance-identifier "$RDS_INSTANCE_ID" \
  --region "$AWS_REGION" \
  --query 'DBInstances[0].VpcSecurityGroups[0].VpcSecurityGroupId' \
  --output text 2>/dev/null) || RDS_SG_ID=""

if [[ -n "$EC2_SG_ID" && "$EC2_SG_ID" != "None" && -n "$RDS_SG_ID" && "$RDS_SG_ID" != "None" ]]; then
  aws ec2 authorize-security-group-ingress \
    --group-id "$RDS_SG_ID" \
    --protocol tcp \
    --port 5432 \
    --source-group "$EC2_SG_ID" \
    --region "$AWS_REGION" 2>/dev/null \
    && ok "RDS SG updated: EC2 SG ($EC2_SG_ID) → RDS port 5432" \
    || log "RDS SG rule already exists, skipping."
else
  warn "Could not update RDS security group automatically — update manually if needed."
fi

# --- Step 8: Update API Gateway integration with new EC2 IP ------------------
# Uses overwrite:path to strip the stage prefix before forwarding to EC2
log "Updating API Gateway integration with new EC2 IP ($PUBLIC_IP)..."

API_ID=$(aws apigatewayv2 get-apis \
  --region "$AWS_REGION" \
  --query "Items[?Name=='$API_NAME'].ApiId" \
  --output text 2>/dev/null) || API_ID=""

if [[ -n "$API_ID" && "$API_ID" != "None" ]]; then
  INTEGRATION_ID=$(aws apigatewayv2 get-integrations \
    --api-id "$API_ID" \
    --region "$AWS_REGION" \
    --query 'Items[0].IntegrationId' \
    --output text 2>/dev/null) || INTEGRATION_ID=""

  if [[ -n "$INTEGRATION_ID" && "$INTEGRATION_ID" != "None" ]]; then
    aws apigatewayv2 update-integration \
      --api-id "$API_ID" \
      --integration-id "$INTEGRATION_ID" \
      --integration-uri "http://$PUBLIC_IP" \
      --request-parameters '{"overwrite:path": "$request.path"}' \
      --region "$AWS_REGION" > /dev/null
    ok "API Gateway integration updated → http://$PUBLIC_IP (path stripping enabled)"
  else
    warn "No existing API Gateway integration found — run setup-api-gateway.sh first."
  fi
else
  warn "API Gateway '$API_NAME' not found — run setup-api-gateway.sh after deploy."
fi

# --- Done --------------------------------------------------------------------
ISSUER="https://cognito-idp.$AWS_REGION.amazonaws.com/$COGNITO_USER_POOL_ID"

echo ""
echo "🚀 EC2 deployment complete!"
echo ""
echo "   Instance ID : $INSTANCE_ID"
echo "   Public IP   : $PUBLIC_IP"
echo "   Private IP  : $PRIVATE_IP"
echo "   Image       : $IMAGE"
echo ""
echo "⏳ Wait ~2 minutes for User Data to finish, then verify:"
echo "   curl http://$PUBLIC_IP/health"
echo ""
echo "📋 SSH access:"
echo "   ssh -i ${KEY_NAME}.pem ec2-user@$PUBLIC_IP"
echo ""
echo "📋 Debug commands (once SSH'd in):"
echo "   sudo docker logs postech-users-api --follow"
echo "   sudo docker ps"
echo "   sudo cat /var/log/cloud-init-output.log | tail -50"
echo ""
echo "📋 First-time API Gateway setup:"
echo "   AWS_ACCOUNT_ID=\"$AWS_ACCOUNT_ID\" \\"
echo "   JWT_AUDIENCE=\"$COGNITO_CLIENT_ID\" \\"
echo "   JWT_ISSUER=\"$ISSUER\" \\"
echo "   ./infra/setup-api-gateway.sh"