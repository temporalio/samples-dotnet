# Search Attributes

This sample demonstrates how to work with custom search attributes: setting initial values when
starting a workflow, reading and upserting typed values from within the workflow, unsetting values,
and querying the visibility store for workflows by their search attributes.

To run, first see [README.md](../../README.md) for prerequisites. The custom search attributes used
by this sample must be registered on the server, which can be done at dev server startup:

    temporal server start-dev \
      --search-attribute CustomIntField=Int \
      --search-attribute CustomKeywordField=Keyword \
      --search-attribute CustomBoolField=Bool \
      --search-attribute CustomDoubleField=Double \
      --search-attribute CustomDatetimeField=Datetime \
      --search-attribute CustomKeywordListField=KeywordList

When running against Temporal Cloud or a self-hosted service, instead register the custom search
attributes as described in [how to create custom Search Attributes](https://docs.temporal.io/self-hosted-guide/visibility#create-custom-search-attributes).

Then, run the following from this directory in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

The worker terminal will output something like:

```
Running worker
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      SearchAttributes workflow started
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Initial search attribute value. CustomIntField: 1
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomBoolField: True
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomDatetimeField: 07/17/2026 02:42:50 +00:00
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomDoubleField: 3.14
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomIntField: 2
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomKeywordField: Keyword fields supports prefix search
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomKeywordListField: value1, value2
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomBoolField: True
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomDatetimeField: 07/17/2026 02:42:50 +00:00
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomIntField: 2
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomKeywordField: Keyword fields supports prefix search
[22:42:50] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomKeywordListField: value1, value2
[22:42:51] info: Temporalio.Activity:ListExecutions[0]
      Listing executions. Query: CustomIntField=2 AND CustomKeywordField STARTS_WITH 'Keyword fields'
[22:42:51] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      WorkflowId must be the same. Current: search-attributes-3f2c32ec-dbab-48e7-bc95-9018674090d5, from visibility query: search-attributes-3f2c32ec-dbab-48e7-bc95-9018674090d5
[22:42:51] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      RunId must be the same. Current: 019f6df4-88e9-7466-8d85-3298cef0e88a, from visibility query: 019f6df4-88e9-7466-8d85-3298cef0e88a
[22:42:51] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomBoolField: True
[22:42:51] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomDatetimeField: 07/17/2026 02:42:50 +00:00
[22:42:51] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomIntField: 2
[22:42:51] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomKeywordField: Keyword fields supports prefix search
[22:42:51] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Current search attribute value. CustomKeywordListField: value1, value2
[22:42:51] info: Temporalio.Workflow:SearchAttributesWorkflow[0]
      Workflow completed
```

See the code for more details.
