module Activities =
    open Temporalio.Activities
    
    type MyDatabseClient() =
        member this.SelectValueAsync(table: string) =
            task {
                return $"some-db-value from table {table}"
            }

    type MyActivities() =
        let databaseClient = MyDatabseClient()
        [<Activity>]
        member this.SelectFromDatabaseAsync(table: string) =
            task {
                return! databaseClient.SelectValueAsync(table)
            }

        [<Activity>]
        static member DoStaticThing() =
            "some-static-value"

module Workflows =
   open Temporalio.Workflows
   open Microsoft.Extensions.Logging
   open System

    [<Workflow>]
    type MyWorkflow() =

        [<WorkflowRun>]
        member this.RunAsync() =
            task {
                // Run an async instance method activity.
                // Note that in F# the generic types can not be inferred since the Temporal API
                // uses overloads which leads to overload resolution ambiguity. We need to specify the generic types explicitly.
                let! result1 = Workflow.ExecuteActivityAsync<Activities.MyActivities, string>(
                    (fun (act: Activities.MyActivities) -> act.SelectFromDatabaseAsync("some-db-table")),
                    ActivityOptions( StartToCloseTimeout = TimeSpan.FromSeconds(5)))
                Workflow.Logger.LogInformation("Activity instance method result: {Result}", result1);

                // Run a sync static method activity.
                let! result2 = Workflow.ExecuteActivityAsync<string>(
                    (fun _ -> Activities.MyActivities.DoStaticThing()),
                    ActivityOptions( StartToCloseTimeout = TimeSpan.FromSeconds(5)))
                Workflow.Logger.LogInformation("Activity static method result: {Result}", result2);

                // We'll go ahead and return this result
                return result2;
            }

module Main =
    open System
    open Microsoft.Extensions.Logging
    open Temporalio.Client
    open Temporalio.Worker
    open System.Threading

    // Create a client to localhost on default namespace
    let client = TemporalClient.ConnectAsync(TemporalClientConnectOptions(
                    "localhost:7233",
                    LoggerFactory = LoggerFactory.Create(fun builder ->
                        builder.
                            AddSimpleConsole(fun options -> options.TimestampFormat = "[HH:mm:ss] " |> ignore).
                            SetMinimumLevel(LogLevel.Information) |> ignore)
                )).Result

    let runWorkerAsync() =
        // Cancellation token cancelled on ctrl+c
        use tokenSource = new CancellationTokenSource()
        Console.CancelKeyPress.Add(fun eventArgs ->
            tokenSource.Cancel()
            eventArgs.Cancel <- true
        )

        // Create an activity instance with some state
        let activities = Activities.MyActivities()

        // Run worker until cancelled
        Console.WriteLine("Running worker")
        use worker = new TemporalWorker(
            client, TemporalWorkerOptions( "activity-simple-sample").AddWorkflow<Workflows.MyWorkflow>().AddAllActivities<Activities.MyActivities>(activities)
            )
        try
            worker.ExecuteAsync(tokenSource.Token) |> Async.AwaitTask |> Async.RunSynchronously
        with
        | :? OperationCanceledException -> 
            Console.WriteLine("Worker cancelled")

    let executeWorkflowAsync() =
        Console.WriteLine("Executing workflow")
        client.ExecuteWorkflowAsync(
            (fun (wf: Workflows.MyWorkflow) -> wf.RunAsync()),
            WorkflowOptions(id = "activity-simple-workflow-id", taskQueue = "activity-simple-sample")
        )

    [<EntryPoint>]
    let main args =
        match Array.head args with
            | "worker" -> 
                runWorkerAsync()
                0
            | "workflow" -> 
                executeWorkflowAsync() |> Async.AwaitTask |> Async.RunSynchronously |> ignore
                0
            | _ -> failwith "Must pass 'worker' or 'workflow' as the single argument"
