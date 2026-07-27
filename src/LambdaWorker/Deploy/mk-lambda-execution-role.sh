#!/bin/bash
set -euo pipefail

# Creates the IAM execution role used by the Lambda function itself.

ROLE_NAME="${1:?Usage: mk-lambda-execution-role.sh <role-name>}"
TRUST_POLICY="$(mktemp -t temporal-lambda-trust.XXXXXX)"

cleanup() {
  rm -f "$TRUST_POLICY"
}
trap cleanup EXIT

cat > "$TRUST_POLICY" <<'JSON'
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Service": "lambda.amazonaws.com" },
      "Action": "sts:AssumeRole"
    }
  ]
}
JSON

aws iam create-role \
  --role-name "$ROLE_NAME" \
  --assume-role-policy-document file://"$TRUST_POLICY" \
  --query 'Role.{RoleName:RoleName,Arn:Arn}' \
  --output json
aws iam wait role-exists --role-name "$ROLE_NAME"
aws iam attach-role-policy \
  --role-name "$ROLE_NAME" \
  --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
