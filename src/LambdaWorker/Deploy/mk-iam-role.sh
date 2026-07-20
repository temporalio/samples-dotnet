#!/bin/bash
set -euo pipefail

# Creates the IAM role that allows Temporal Cloud to invoke your Lambda function.
# Use the same External ID when creating the Temporal Worker Deployment Version.

STACK_NAME="${1:?Usage: mk-iam-role.sh <stack-name> <external-id> <lambda-arn>}"
EXTERNAL_ID="${2:?Usage: mk-iam-role.sh <stack-name> <external-id> <lambda-arn>}"
LAMBDA_ARN="${3:?Usage: mk-iam-role.sh <stack-name> <external-id> <lambda-arn>}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

aws cloudformation create-stack \
  --stack-name "$STACK_NAME" \
  --template-body file://"$SCRIPT_DIR/iam-role-for-temporal-lambda-invoke-test.yaml" \
  --parameters \
    ParameterKey=AssumeRoleExternalId,ParameterValue="$EXTERNAL_ID" \
    ParameterKey=LambdaFunctionARNs,ParameterValue="$LAMBDA_ARN" \
  --capabilities CAPABILITY_NAMED_IAM \
  --query StackId \
  --output text

aws cloudformation wait stack-create-complete --stack-name "$STACK_NAME"
aws cloudformation describe-stacks \
  --stack-name "$STACK_NAME" \
  --query "Stacks[0].Outputs[?OutputKey=='RoleARN' || OutputKey=='RoleName'].{Key:OutputKey,Value:OutputValue}" \
  --output table
