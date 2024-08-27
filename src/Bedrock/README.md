# AI Chatbot example using Amazon Bedrock

Demonstrates how Temporal and Amazon Bedrock can be used to quickly build bulletproof AI applications.

## Samples

* [Basic](Basic) - A basic Bedrock workflow to process a single prompt.
* [SignalsAndQueries](SignalsAndQueries) - Extension to the basic workflow to allow multiple prompts through signals & queries.
* [Entity](Entity) - Full multi-Turn chat using an entity workflow.

## Pre-requisites

1. An AWS account with Bedrock enabled.
2. A machine that has access to Bedrock.
3. A local Temporal server running on the same machine. See [Temporal's dev server docs](https://docs.temporal.io/cli#start-dev-server) for more information.

These examples use Amazon's .NET SDK. To configure your AWS credentials, follow the instructions in [the AWS SDK for .NET documentation](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/creds-idc.html).

## Running the samples

There are 3 Bedrock samples, see the README.md in each subdirectory for instructions on running each.