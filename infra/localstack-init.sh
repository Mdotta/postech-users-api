#!/bin/bash
ENDPOINT=http://localhost:4566

# SNS Topic
aws --endpoint-url=$ENDPOINT sns create-topic --name user-events

# SQS Queue
aws --endpoint-url=$ENDPOINT sqs create-queue --queue-name user-events-queue

# Subscribe SQS to SNS
TOPIC_ARN=$(aws --endpoint-url=$ENDPOINT sns list-topics --query 'Topics[0].TopicArn' --output text)
QUEUE_URL=$(aws --endpoint-url=$ENDPOINT sqs get-queue-url --queue-name user-events-queue --query QueueUrl --output text)
QUEUE_ARN=$(aws --endpoint-url=$ENDPOINT sqs get-queue-attributes --queue-url $QUEUE_URL --attribute-names QueueArn --query Attributes.QueueArn --output text)

aws --endpoint-url=$ENDPOINT sns subscribe \
  --topic-arn $TOPIC_ARN \
  --protocol sqs \
  --notification-endpoint $QUEUE_ARN