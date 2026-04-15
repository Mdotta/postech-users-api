#!/bin/bash
set -euo pipefail

# =============================================================================
# deploy-ec2.sh
# Launches an EC2 instance running the postech-users-api container.
#
# Usage:
#   ./infra/deploy-ec2.sh
#
# Required environment variables:
#   AWS_ACCOUNT_ID        - Your AWS account ID
#   DB_CONNECTION_STRING  - Full PostgreSQL connection string
#   SNS_TOPIC_ARN         - ARN of the SNS user-events topic
#   COGNITO_USER_POOL_ID  - Cognito User Pool ID (e.g. us-east-1_eWFpSqOyW)
#   COGNITO_CLIENT_ID     - Cognito App Client ID
#
# Optional:
#   AWS_REGION            - Defaults to us-east-1
#   INSTANCE_TYPE         - Defaults to t3.micro
#   KEY_NAME              - Defaults to postech-key
# =============================================================================

AWS_ACCOUNT_ID="${AWS_ACCOUNT_ID:?❌ AWS_ACCOUNT_ID is not set}"
DB_CONNECTION_STRING="${DB_CONNECTION_STRING:?❌ DB_CONNECTION_STRING is not set}"
SNS_TOPIC_ARN="${SNS_TOPIC_ARN:?❌ SNS_TOPIC_ARN is not set}"
COGNITO_USER_POOL_ID="${COGNITO_USER_POOL_ID:?❌ COGNITO_USER_POOL_ID is not set}"
COGNITO_CLIENT_ID="${COGNITO_CLIENT_ID:?❌ COGNITO_CLIENT_ID is not set}"

AWS_REGION="${AWS_REGION:-us-east-1}"
INSTANCE_TYPE="${INSTANCE_TYPE:-t3.micro}"
KEY_NAME="${KEY_NAME:-postech-key}"

ECR_REGISTRY="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"
IMAGE="$ECR_REGISTRY/postech-users-api:latest"

# --- Helpers -----------------------------------------------------------------
log()  { echo "[$(date '+%H:%M:%S')] $*"; }
ok()   { echo "[$(date '+%H:%M:%S')] ✅ $*"; }
fail() { echo "[$(date '+%H:%M:%S')] ❌ $*" >&2; exit 1; }

# --- Checks ------------------------------------------------------------------
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

# --- Step 2: Create key pair if it doesn't exist ----------------------------
log "Checking key pair '$KEY_NAME'..."

KEY_EXISTS=$(aws ec2 describe-key-pairs \
  --region "$AWS_REGION" \
  --key-names "$KEY_NAME" \
  --query 'KeyPairs[0].KeyName' \
  --output text 2>/dev/null || echo "")

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

# --- Step 3: Check if instance already exists --------------------------------
log "Checking for existing EC2 instance..."

EXISTING_INSTANCE_ID=$(aws ec2 describe-instances \
  --region "$AWS_REGION" \
  --filters "Name=tag:Name,Values=postech-users-api" \
            "Name=instance-state-name,Values=running,pending,stopped" \
  --query 'Reservations[0].Instances[0].InstanceId' \
  --output text)

if [[ -n "$EXISTING_INSTANCE_ID" && "$EXISTING_INSTANCE_ID" != "None" ]]; then
  log "Instance already exists ($EXISTING_INSTANCE_ID). Terminating it for fresh deploy..."
  aws ec2 terminate-instances \
    --region "$AWS_REGION" \
    --instance-ids "$EXISTING_INSTANCE_ID" > /dev/null
  log "Waiting for termination..."
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

AWS_REGION="$AWS_REGION"
AWS_ACCOUNT_ID="$AWS_ACCOUNT_ID"
ECR_REGISTRY="$ECR_REGISTRY"
IMAGE="$IMAGE"

# Install Docker
yum update -y
yum install -y docker
systemctl start docker
systemctl enable docker

# Install AWS CLI v2
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o /tmp/awscliv2.zip
unzip /tmp/awscliv2.zip -d /tmp
/tmp/aws/install

# Authenticate with ECR
aws ecr get-login-password --region "\$AWS_REGION" | \\
  docker login --username AWS --password-stdin "\$ECR_REGISTRY"

# Pull and run the container
docker pull "\$IMAGE"

docker run -d \\
  --name postech-users-api \\
  --restart unless-stopped \\
  -p 80:80 \\
  -e ASPNETCORE_URLS="http://+:80" \\
  -e ConnectionStrings__DefaultConnection="$DB_CONNECTION_STRING" \\
  -e AWS__Region="$AWS_REGION" \\
  -e AWS__SnsTopicArn="$SNS_TOPIC_ARN" \\
  -e CognitoSettings__UserPoolId="$COGNITO_USER_POOL_ID" \\
  -e CognitoSettings__ClientId="$COGNITO_CLIENT_ID" \\
  -e CognitoSettings__Region="$AWS_REGION" \\
  "\$IMAGE"
EOF
)

# --- Step 5: Launch instance -------------------------------------------------
log "Launching EC2 instance..."

INSTANCE_ID=$(aws ec2 run-instances \
  --region "$AWS_REGION" \
  --image-id "$AMI_ID" \
  --instance-type "$INSTANCE_TYPE" \
  --iam-instance-profile Name=LabInstanceProfile \
  --security-groups postech-api-sg \
  --key-name "$KEY_NAME" \
  --user-data "$USER_DATA" \
  --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=postech-users-api}]" \
  --query 'Instances[0].InstanceId' \
  --output text)

ok "Instance launched: $INSTANCE_ID"

# --- Step 6: Wait for running state ------------------------------------------
log "Waiting for instance to reach running state..."

aws ec2 wait instance-running \
  --region "$AWS_REGION" \
  --instance-ids "$INSTANCE_ID"

PUBLIC_IP=$(aws ec2 describe-instances \
  --region "$AWS_REGION" \
  --instance-ids "$INSTANCE_ID" \
  --query 'Reservations[0].Instances[0].PublicIpAddress' \
  --output text)

ok "Instance is running!"

# --- Done --------------------------------------------------------------------
echo ""
echo "🚀 EC2 deployment complete!"
echo ""
echo "   Instance ID : $INSTANCE_ID"
echo "   Public IP   : $PUBLIC_IP"
echo "   Image       : $IMAGE"
echo ""
echo "⏳ Wait ~2 minutes for User Data to finish, then verify:"
echo ""
echo "   curl http://$PUBLIC_IP/scalar/v1"
echo ""
echo "📋 SSH access:"
echo "   ssh -i ${KEY_NAME}.pem ec2-user@$PUBLIC_IP"
echo ""
echo "📋 Debug commands (once SSH'd in):"
echo "   sudo cat /var/log/cloud-init-output.log"
echo "   docker logs postech-users-api"
echo "   docker ps"
echo ""
echo "📋 Run API Gateway setup next:"
echo ""
echo "   AWS_ACCOUNT_ID=\"$AWS_ACCOUNT_ID\" \\"
echo "   JWT_AUDIENCE=\"$COGNITO_CLIENT_ID\" \\"
echo "   JWT_ISSUER=\"https://cognito-idp.$AWS_REGION.amazonaws.com/$COGNITO_USER_POOL_ID\" \\"
echo "   ./infra/setup-api-gateway.sh"