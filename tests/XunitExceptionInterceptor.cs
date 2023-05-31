namespace TemporalioSamples.Tests;

using System.Threading.Tasks;
using Temporalio.Exceptions;
using Temporalio.Worker.Interceptors;
using Xunit.Sdk;

public class XunitExceptionInterceptor : IWorkerInterceptor
{
    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
        new WorkflowInbound(nextInterceptor);

    public class WorkflowInbound : WorkflowInboundInterceptor
    {
        public WorkflowInbound(WorkflowInboundInterceptor next)
            : base(next)
        {
        }

        public override async Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
        {
            try
            {
                return await base.ExecuteWorkflowAsync(input);
            }
            catch (XunitException e)
            {
                throw new ApplicationFailureException("Assertion failed", e, "AssertFail");
            }
        }
    }
}