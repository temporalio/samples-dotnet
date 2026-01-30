using System.Reflection;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace RoutedVersioning;

/*
 * This acts as a kind of facade that delegates actual behavior to the underlying type.
 * THIS FILE DOESNT CHANGE  - unless, of course, the IMyWorkflow interface changes.
 */
// Use of this attribute is optional unless you want to alias the Workflow to a separate TypeName.
// But this should be the same as the interface which you are using to call the workflow
[Workflow(IMyWorkflow.WorkflowType)]
public class MyWorkflowDontChangeThisFileOftenAndDelegateToImplementors : IMyWorkflow
{
    public static readonly WorkflowVersion LatestVersion = WorkflowVersion.V3;

    private readonly IMyWorkflow inner;

    private readonly Dictionary<WorkflowVersion, Func<StartMyWorkflowRequest, IMyWorkflow>> versions = new Dictionary<WorkflowVersion, Func<StartMyWorkflowRequest, IMyWorkflow>>()
    {
        [LatestVersion] = args => new MyWorkflowLatest(args),
        [WorkflowVersion.V1] = args => new MyWorkflowV1(args),
        [WorkflowVersion.V2] = args => new MyWorkflowV2(args),
    };

    [WorkflowInit]
    public MyWorkflowDontChangeThisFileOftenAndDelegateToImplementors(StartMyWorkflowRequest args)
    {
        if (versions.TryGetValue(args.Options.Version, out var create))
        {
            inner = create(args);
        }
        else
        {
            throw new ApplicationFailureException("Version is required");
        }
    }

    [WorkflowRun]
    public async Task RunAsync(StartMyWorkflowRequest args)
    {
        // optionally leverage the existing `TemporalChangeVersion` search attribute that gets written
        // so visibility queries (eg '`TemporalChangeVersion`in("v2")') can reveal what old files can be removed.
        Workflow.Patched(args.Options.Version.Value);
        await inner.RunAsync(args);
    }

    [WorkflowSignal]
    public Task CallMeMaybeAsync()
    {
        return inner.CallMeMaybeAsync();
    }

    [WorkflowQuery]
    public string GetResult()
    {
        return inner.GetResult();
    }
}