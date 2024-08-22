# Basic Amazon Bedrock workflow

A basic Bedrock workflow. Starts a workflow with a prompt, generates a response and ends the workflow.

To run, first see `Bedrock` [README.md](../README.md) for prerequisites specific to this sample. Once set up, run the following from this directory:

1. Run the worker: `dotnet run worker`
2. In another terminal run the client with a prompt:

   e.g. `dotnet run send-message 'What animals are marsupials?'`