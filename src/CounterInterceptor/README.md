# dotnet-counter-interceptor
The sample demonstrates: 
- the use of a Worker Workflow Interceptor that counts the number of Workflow Executions, Child Workflow Executions, and Activity Executions and the number of Signals and Queries. It is based
off of the [Java sample](https://github.com/temporalio/samples-java/tree/main) located [here](https://github.com/temporalio/samples-java/tree/main/core/src/main/java/io/temporal/samples/countinterceptor)
- the use of a Client Workflow Interceptor that counts the number of Workflow Executions and the number of Signals and Queries.

To run, first see [README.md](https://github.com/temporalio/samples-dotnet/blob/main/README.md) for prerequisites

## Run Worker and Client 
```bash
# make sure you have temporal server running 
dotnet run
```
