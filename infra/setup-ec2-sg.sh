#!/bin/bash
set -euo pipefail

# =============================================================================
# setup-ec2-sg.sh
# Creates the EC2 security group and opens the required ports.
# Also adds an inbound rule on the RDS security group to allow EC2 → Postgres.
#
# Usage:
#   ./infra/setup-ec2-sg.sh
#
# Optional environment variables:
#   AWS_REGION              - AWS region (default: us-east-1)
#   RDS_INSTANCE_ID         - RDS instance identifier (default: postech-users)
#   EC2_SG_NAME             - EC2 security group name (default: postech-api-sg)
# =============================================================================

AWS_REGION="${AWS_REGION:-us-east-1}"
RDS_INSTANCE_ID="${RDS_INSTANCE_ID:-postech-users}"
EC2_SG_NAME="${EC2_SG_NAME:-postech-api-sg}"

log()  { echo "[$(date '+%H:%M:%S')] $*"; }
ok()   { echo "[$(date '+%H:%M:%S')] ✅ $*"; }
fail() { echo "[$(date '+%H:%M:%S')] ❌ $*" >&2; exit 1; }

command -v aws &>/dev/null || fail "aws CLI is not installed"

# --- Get current public IP ----------------------------------------------------
MY_IP=$(curl -s ifconfig.me)
[[ -n "$MY_IP" ]] || fail "Could not determine public IP"
log "Your public IP: $MY_IP"

# --- Create EC2 security group ------------------------------------------------
log "Creating EC2 security group '$EC2_SG_NAME'..."

if aws ec2 describe-security-groups \
     --group-names "$EC2_SG_NAME" \
     --region "$AWS_REGION" &>/dev/null; then
  log "Security group '$EC2_SG_NAME' already exists, skipping creation."
else
  aws ec2 create-security-group \
    --group-name "$EC2_SG_NAME" \
    --description "Security group for postech users API" \
    --region "$AWS_REGION"
  ok "Security group '$EC2_SG_NAME' created."
fi

EC2_SG_ID=$(aws ec2 describe-security-groups \
  --group-names "$EC2_SG_NAME" \
  --region "$AWS_REGION" \
  --query 'SecurityGroups[0].GroupId' --output text)

log "EC2 Security Group ID: $EC2_SG_ID"

# --- Add inbound rules to EC2 SG (ignore errors if rules already exist) -------
log "Adding inbound rules to EC2 security group..."

aws ec2 authorize-security-group-ingress \
  --group-id "$EC2_SG_ID" \
  --protocol tcp --port 80 --cidr 0.0.0.0/0 \
  --region "$AWS_REGION" 2>/dev/null && ok "HTTP (80) rule added." || log "HTTP (80) rule already exists, skipping."

aws ec2 authorize-security-group-ingress \
  --group-id "$EC2_SG_ID" \
  --protocol tcp --port 22 --cidr "$MY_IP/32" \
  --region "$AWS_REGION" 2>/dev/null && ok "SSH (22) rule added for $MY_IP." || log "SSH (22) rule already exists, skipping."

# --- Allow EC2 SG → RDS on port 5432 -----------------------------------------
log "Fetching RDS security group for '$RDS_INSTANCE_ID'..."

RDS_SG_ID=$(aws rds describe-db-instances \
  --db-instance-identifier "$RDS_INSTANCE_ID" \
  --region "$AWS_REGION" \
  --query 'DBInstances[0].VpcSecurityGroups[0].VpcSecurityGroupId' --output text)

[[ -n "$RDS_SG_ID" && "$RDS_SG_ID" != "None" ]] || fail "Could not find security group for RDS instance '$RDS_INSTANCE_ID'"
log "RDS Security Group ID: $RDS_SG_ID"

aws ec2 authorize-security-group-ingress \
  --group-id "$RDS_SG_ID" \
  --protocol tcp --port 5432 \
  --source-group "$EC2_SG_ID" \
  --region "$AWS_REGION" 2>/dev/null && ok "EC2 → RDS (5432) rule added." || log "EC2 → RDS (5432) rule already exists, skipping."

# --- Done --------------------------------------------------------------------
echo ""
echo "🚀 Security groups ready!"
echo "   EC2 SG : $EC2_SG_ID ($EC2_SG_NAME)"
echo "   RDS SG : $RDS_SG_ID"
echo "   Run './infra/deploy-ec2.sh' to launch the EC2 instance."