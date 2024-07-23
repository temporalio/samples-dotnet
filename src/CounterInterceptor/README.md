# dotnet-counter-interceptor
The sample demonstrates: 
- the use of a simple Worker Workflow Interceptor that counts the number of Workflow Executions, Child Workflow Executions, and Activity Executions as well as the number of Signals and Queries. It is based
off of the [Java Sample](https://github.com/temporalio/samples-java/tree/main) located [here](https://github.com/temporalio/samples-java/tree/main/core/src/main/java/io/temporal/samples/countinterceptor)
- the use of a simple Client Workflow Interceptor that counts the number of Workflow Executions as well as the number of Signals and Queries.

## Start local Temporal Server
```bash
# run only once
temporal server start-dev
```

## Run Worker Locally
```bash
# make sure you have temporal server running (see section above)
dotnet run
```

## Run Worker using Temporal Cloud
```bash
# set up environment variables
export TEMPORAL_NAMESPACE=<namespace>.<accountId>
export TEMPORAL_ADDRESS=<namespace>.<accountId>.tmprl.cloud:7233
export TEMPORAL_TLS_CERT=/path/to/cert
export TEMPORAL_TLS_KEY=/path/to/key
# run the worker
dotnet run
```