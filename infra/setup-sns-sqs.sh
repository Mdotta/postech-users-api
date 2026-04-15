#!/bin/bash
set -euo pipefail

# =============================================================================
# setup-sns-sqs.sh
# Provisions all SNS topics, SQS queues, DLQs, and subscriptions for the
# postech microservices messaging topology:
#
#   users-api    → SNS: user-created    → SQS: notifications-user-created    → Lambda
#   catalog-api  → SNS: order-created   → SQS: payments-order-created        → payment-api
#   payment-api  → SNS: order-processed → SQS: notifications-order-processed → Lambda
#                                       → SQS: catalog-order-processed        → catalog-api
#
# notification-api is a Lambda triggered by SQS (no long-running service needed).
#
# Usage:
#   ./infra/setup-sns-sqs.sh
#
# Required environment variables:
#   AWS_ACCOUNT_ID  - Your AWS account ID
#
# Optional:
#   AWS_REGION      - Defaults to us-east-1
# =============================================================================

AWS_ACCOUNT_ID="${AWS_ACCOUNT_ID:?❌ AWS_ACCOUNT_ID is not set}"
AWS_REGION="${AWS_REGION:-us-east-1}"

# All log/ok/fail write to stderr so they never pollute function return values
log()  { echo "[$(date '+%H:%M:%S')] $*" >&2; }
ok()   { echo "[$(date '+%H:%M:%S')] ✅ $*" >&2; }
fail() { echo "[$(date '+%H:%M:%S')] ❌ $*" >&2; exit 1; }

command -v aws &>/dev/null || fail "aws CLI is not installed"
command -v jq  &>/dev/null || fail "jq is not installed"

SQS_BASE="https://sqs.$AWS_REGION.amazonaws.com/$AWS_ACCOUNT_ID"

# =============================================================================
# Helpers
# =============================================================================

create_sns_topic() {
  local NAME="$1"
  local EXISTING
  EXISTING=$(aws sns list-topics \
    --region "$AWS_REGION" \
    --query "Topics[?ends_with(TopicArn, ':$NAME')].TopicArn" \
    --output text)

  if [[ -n "$EXISTING" && "$EXISTING" != "None" ]]; then
    log "SNS topic '$NAME' already exists, reusing."
    echo "$EXISTING"
  else
    local ARN
    ARN=$(aws sns create-topic \
      --name "$NAME" \
      --region "$AWS_REGION" \
      --query 'TopicArn' --output text)
    ok "SNS topic created: $ARN"
    echo "$ARN"
  fi
}

create_dlq() {
  local QUEUE_NAME="$1"
  local DLQ_NAME="${QUEUE_NAME}-dlq"

  local EXISTING_URL
  EXISTING_URL=$(aws sqs get-queue-url \
    --queue-name "$DLQ_NAME" \
    --region "$AWS_REGION" \
    --query 'QueueUrl' --output text 2>/dev/null) || EXISTING_URL=""

  if [[ -z "$EXISTING_URL" || "$EXISTING_URL" == "None" ]]; then
    aws sqs create-queue \
      --queue-name "$DLQ_NAME" \
      --region "$AWS_REGION" \
      --attributes MessageRetentionPeriod=1209600 > /dev/null
    ok "DLQ created: $DLQ_NAME (14-day retention)"
  else
    log "DLQ '$DLQ_NAME' already exists, reusing."
  fi

  # Only the ARN goes to stdout — log() writes to stderr
  aws sqs get-queue-attributes \
    --queue-url "$SQS_BASE/$DLQ_NAME" \
    --attribute-names QueueArn \
    --region "$AWS_REGION" \
    --query 'Attributes.QueueArn' \
    --output text
}

create_sqs_queue() {
  local NAME="$1"
  local DLQ_ARN="$2"

  local EXISTING_URL
  EXISTING_URL=$(aws sqs get-queue-url \
    --queue-name "$NAME" \
    --region "$AWS_REGION" \
    --query 'QueueUrl' --output text 2>/dev/null) || EXISTING_URL=""

  if [[ -n "$EXISTING_URL" && "$EXISTING_URL" != "None" ]]; then
    log "SQS queue '$NAME' already exists, reusing."
  else
    # Write create-queue input to a temp file to avoid shell escaping issues
    local TMP
    TMP=$(mktemp)
    jq -cn \
      --arg name "$NAME" \
      --arg dlq  "$DLQ_ARN" \
      '{
        QueueName: $name,
        Attributes: {
          RedrivePolicy: ({deadLetterTargetArn: $dlq, maxReceiveCount: "3"} | tojson)
        }
      }' > "$TMP"

    aws sqs create-queue \
      --region "$AWS_REGION" \
      --cli-input-json "file://$TMP" > /dev/null

    rm -f "$TMP"
    ok "SQS queue created: $NAME (DLQ attached, maxReceiveCount=3)"
  fi

  aws sqs get-queue-url \
    --queue-name "$NAME" \
    --region "$AWS_REGION" \
    --query 'QueueUrl' \
    --output text
}

subscribe_sqs_to_sns() {
  local TOPIC_ARN="$1"
  local QUEUE_URL="$2"
  local QUEUE_NAME="$3"
  local TOPIC_NAME
  TOPIC_NAME=$(echo "$TOPIC_ARN" | awk -F: '{print $NF}')

  local QUEUE_ARN
  QUEUE_ARN=$(aws sqs get-queue-attributes \
    --queue-url "$QUEUE_URL" \
    --attribute-names QueueArn \
    --region "$AWS_REGION" \
    --query 'Attributes.QueueArn' --output text)

  local EXISTING_SUB
  EXISTING_SUB=$(aws sns list-subscriptions-by-topic \
    --topic-arn "$TOPIC_ARN" \
    --region "$AWS_REGION" \
    --query "Subscriptions[?Endpoint=='$QUEUE_ARN'].SubscriptionArn" \
    --output text)

  if [[ -n "$EXISTING_SUB" && "$EXISTING_SUB" != "None" ]]; then
    log "Subscription $TOPIC_NAME → $QUEUE_NAME already exists, skipping."
    return
  fi

  aws sns subscribe \
    --topic-arn "$TOPIC_ARN" \
    --protocol sqs \
    --notification-endpoint "$QUEUE_ARN" \
    --region "$AWS_REGION" > /dev/null

  # Write policy to temp file — Policy value is a JSON-encoded string
  local TMP
  TMP=$(mktemp)
  jq -cn \
    --arg url  "$QUEUE_URL" \
    --arg qarn "$QUEUE_ARN" \
    --arg tarn "$TOPIC_ARN" \
    '{
      QueueUrl: $url,
      Attributes: {
        Policy: ({
          Version: "2012-10-17",
          Statement: [{
            Effect: "Allow",
            Principal: {Service: "sns.amazonaws.com"},
            Action: "sqs:SendMessage",
            Resource: $qarn,
            Condition: {ArnEquals: {"aws:SourceArn": $tarn}}
          }]
        } | tojson)
      }
    }' > "$TMP"

  aws sqs set-queue-attributes \
    --region "$AWS_REGION" \
    --cli-input-json "file://$TMP" > /dev/null

  rm -f "$TMP"
  ok "Subscribed: $TOPIC_NAME → $QUEUE_NAME"
}

# =============================================================================
# Step 1: Create SNS Topics
# =============================================================================
log "Creating SNS topics..."

TOPIC_USER_CREATED=$(create_sns_topic "user-created")
TOPIC_ORDER_CREATED=$(create_sns_topic "order-created")
TOPIC_ORDER_PROCESSED=$(create_sns_topic "order-processed")

# =============================================================================
# Step 2: Create DLQs (one per consumer queue)
# =============================================================================
log "Creating Dead Letter Queues..."

DLQ_NOTIFICATIONS_USER=$(create_dlq "notifications-user-created")
DLQ_NOTIFICATIONS_ORDER=$(create_dlq "notifications-order-processed")
DLQ_PAYMENTS_ORDER=$(create_dlq "payments-order-created")
DLQ_CATALOG_ORDER=$(create_dlq "catalog-order-processed")

# =============================================================================
# Step 3: Create SQS Queues
# =============================================================================
log "Creating SQS queues..."

QUEUE_NOTIFICATIONS_USER=$(create_sqs_queue  "notifications-user-created"    "$DLQ_NOTIFICATIONS_USER")
QUEUE_NOTIFICATIONS_ORDER=$(create_sqs_queue "notifications-order-processed" "$DLQ_NOTIFICATIONS_ORDER")
QUEUE_PAYMENTS_ORDER=$(create_sqs_queue      "payments-order-created"        "$DLQ_PAYMENTS_ORDER")
QUEUE_CATALOG_ORDER=$(create_sqs_queue       "catalog-order-processed"       "$DLQ_CATALOG_ORDER")

# =============================================================================
# Step 4: Subscribe queues to topics
# =============================================================================
log "Creating SNS → SQS subscriptions..."

subscribe_sqs_to_sns "$TOPIC_USER_CREATED"    "$QUEUE_NOTIFICATIONS_USER"  "notifications-user-created"
subscribe_sqs_to_sns "$TOPIC_ORDER_CREATED"   "$QUEUE_PAYMENTS_ORDER"      "payments-order-created"
subscribe_sqs_to_sns "$TOPIC_ORDER_PROCESSED" "$QUEUE_NOTIFICATIONS_ORDER" "notifications-order-processed"
subscribe_sqs_to_sns "$TOPIC_ORDER_PROCESSED" "$QUEUE_CATALOG_ORDER"       "catalog-order-processed"

# =============================================================================
# Done
# =============================================================================
echo ""
echo "🚀 SNS/SQS topology ready!"
echo ""
echo "   SNS Topics:"
echo "   ├── user-created    : $TOPIC_USER_CREATED"
echo "   ├── order-created   : $TOPIC_ORDER_CREATED"
echo "   └── order-processed : $TOPIC_ORDER_PROCESSED"
echo ""
echo "   SQS Queues (with DLQs):"
echo "   ├── notifications-user-created    → Lambda trigger"
echo "   ├── notifications-order-processed → Lambda trigger"
echo "   ├── payments-order-created        → payment-api consumer"
echo "   └── catalog-order-processed       → catalog-api consumer"
echo ""
echo "📋 Next steps:"
echo ""
echo "   1. Deploy notification Lambda and add SQS triggers:"
echo "      aws lambda create-event-source-mapping \\"
echo "        --function-name postech-notification-handler \\"
echo "        --event-source-arn arn:aws:sqs:$AWS_REGION:$AWS_ACCOUNT_ID:notifications-user-created \\"
echo "        --batch-size 10 --region $AWS_REGION"
echo ""
echo "      aws lambda create-event-source-mapping \\"
echo "        --function-name postech-notification-handler \\"
echo "        --event-source-arn arn:aws:sqs:$AWS_REGION:$AWS_ACCOUNT_ID:notifications-order-processed \\"
echo "        --batch-size 10 --region $AWS_REGION"
echo ""
echo "   2. Set SQS queue URLs as env vars in payment-api and catalog-api:"
echo "      AWS__SqsQueueUrl=$SQS_BASE/payments-order-created"
echo "      AWS__SqsQueueUrl=$SQS_BASE/catalog-order-processed"
echo ""
echo "   3. Update users-api SNS_TOPIC_ARN → $TOPIC_USER_CREATED"
echo ""
echo "📋 Verify a message arrived after registering a user:"
echo "   aws sqs receive-message \\"
echo "     --queue-url $SQS_BASE/notifications-user-created \\"
echo "     --region $AWS_REGION"