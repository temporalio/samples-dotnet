#pragma warning disable CA1063, CA1816 // Don't need to follow dispose rules here

namespace TemporalioSamples.Tests.ActivityDependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using Temporalio.Workflows;
using TemporalioSamples.ActivityDependencyInjection;
using Xunit;
using Xunit.Abstractions;

public class WorkerTests : TestBase
{
    public WorkerTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task ExecuteAsync_DependencyInjection_DisposesWhenExpected()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();
        using var tokenSource = new CancellationTokenSource();
        var taskQueue = $"tq-{Guid.NewGuid()}";

        // Build host
        var host = Host.CreateDefaultBuilder().
            ConfigureLogging(ConfigureLogging).
            ConfigureServices(ctx =>
            {
                // Setup worker and client options
                ctx.Configure<TemporalClientConnectOptions>(options =>
                {
                    options.TargetHost = env.Client.Connection.Options.TargetHost;
                    options.Namespace = env.Client.Options.Namespace;
                });
                ctx.Configure<TemporalWorkerOptions>(options =>
                {
                    options.TaskQueue = taskQueue;
                    options.Interceptors = new[] { new XunitExceptionInterceptor() };
                    options.AddWorkflow<SomeWorkflow>();
                });

                // Add the activities
                ctx.AddTemporalActivitySingleton<ActivitiesSingleton>();
                ctx.AddTemporalActivityScoped<ActivitiesScoped>();
                ctx.AddTemporalActivityTransient<ActivitiesTransient>();

                // Add the worker
                ctx.AddHostedService<Worker>();
            }).
            Build();

        // Start in background
        var hostRunTask = Task.Run(() => host.RunAsync(tokenSource.Token));

        // Run workflow
        await env.Client.ExecuteWorkflowAsync(
            (SomeWorkflow wf) => wf.RunAsync(),
            new(id: $"wf-{Guid.NewGuid()}", taskQueue: taskQueue));

        // Confirm singleton not disposed yet
        Assert.False(ActivitiesSingleton.Disposed);

        // Cancel and wait for host
        tokenSource.Cancel();
        await hostRunTask;

        // Confirm singleton disposed
        Assert.True(ActivitiesSingleton.Disposed);
    }

    [Workflow]
    public class SomeWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            // !!! WARNING !!!
            // This workflow uses known non-deterministic access to external mutable static state
            // for the purposes of this test. This should never be done in a normal workflow.
            var options = new ActivityOptions()
            {
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                RetryPolicy = new() { MaximumAttempts = 1 },
            };

            // Singleton
            Assert.False(ActivitiesSingleton.Created);
            Assert.Equal(
                "singleton",
                await Workflow.ExecuteActivityAsync((ActivitiesSingleton act) => act.DoSingletonThingAsync(), options));
            // Expect non-disposed
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.True(ActivitiesSingleton.Created);
            Assert.False(ActivitiesSingleton.Disposed);
            Assert.False(ActivitiesSingleton.Destructed);

            // Scoped
            Assert.False(ActivitiesScoped.Created);
            Assert.Equal(
                "scoped",
                await Workflow.ExecuteActivityAsync((ActivitiesScoped act) => act.DoScopedThingAsync(), options));
            // Expect disposed
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.True(ActivitiesScoped.Created);
            Assert.True(ActivitiesScoped.Disposed);
            Assert.True(ActivitiesScoped.Destructed);

            // Transient
            Assert.False(ActivitiesTransient.Created);
            Assert.Equal(
                "transient",
                await Workflow.ExecuteActivityAsync((ActivitiesTransient act) => act.DoTransientThingAsync(), options));
            // Expect disposed
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.True(ActivitiesTransient.Created);
            Assert.True(ActivitiesTransient.Destructed);
        }
    }

    public class ActivitiesSingleton : IDisposable
    {
        public static bool Created { get; set; }

        public static bool Destructed { get; set; }

        public static bool Disposed { get; set; }

        public ActivitiesSingleton() => Created = true;

        ~ActivitiesSingleton() => Destructed = true;

        public void Dispose() => Disposed = true;

        [Activity]
        public async Task<string> DoSingletonThingAsync()
        {
            await Task.Delay(200, ActivityExecutionContext.Current.CancellationToken);
            Assert.False(Disposed);
            return "singleton";
        }
    }

    public class ActivitiesScoped : IDisposable
    {
        public static bool Created { get; set; }

        public static bool Destructed { get; set; }

        public static bool Disposed { get; set; }

        public ActivitiesScoped() => Created = true;

        ~ActivitiesScoped() => Destructed = true;

        public void Dispose() => Disposed = true;

        [Activity]
        public async Task<string> DoScopedThingAsync()
        {
            await Task.Delay(200, ActivityExecutionContext.Current.CancellationToken);
            Assert.False(Disposed);
            return "scoped";
        }
    }

    public class ActivitiesTransient
    {
        public static bool Created { get; set; }

        public static bool Destructed { get; set; }

        public ActivitiesTransient() => Created = true;

        ~ActivitiesTransient() => Destructed = true;

        [Activity]
        public async Task<string> DoTransientThingAsync()
        {
            await Task.Delay(200, ActivityExecutionContext.Current.CancellationToken);
            return "transient";
        }
    }
}