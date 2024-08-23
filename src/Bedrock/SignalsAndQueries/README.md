# Amazon Bedrock workflow using Signals and Queries

Adding signals & queries to the [basic Bedrock sample](../Basic). Starts a workflow with a prompt, allows follow-up 
prompts to be given using Temporal signals, and allows the conversation history to be queried using Temporal queries.

To run, first see `Bedrock` [README.md](../README.md) for prerequisites specific to this sample. Once set up, run the
following from this directory:

1. Run the worker: `dotnet run worker`
2. In another terminal run the client with a prompt.

   Example: `dotnet run send-message 'What animals are marsupials?'`

3. View the worker's output for the response.
4. Give followup prompts by signaling the workflow.

   Example: `dotnet run send-message 'Do they lay eggs?'`
5. Get the conversation history by querying the workflow.

   Example: `dotnet run get-history`
6. The workflow will timeout after inactivity.