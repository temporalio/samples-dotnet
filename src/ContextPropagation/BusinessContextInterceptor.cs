using System.Diagnostics;
using System.Security.Principal;

namespace TemporalioSamples.ContextPropagation;

using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

/// <summary>
/// General purpose interceptor that can be used to propagate async-local context through workflows
/// and activities. This must be set on the client used for interacting with workflows and used for
/// the worker.
/// </summary>
/// <typeparam name="T">Context data type.</typeparam>
public class BusinessContextInterceptor : IWorkerInterceptor, IClientInterceptor
{
    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor)
    {
        return new BusinessContextWorkflowInboundInterceptor(this, nextInterceptor);
    }

    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor)
    {
        return new BusinessContextActivityInboundInterceptor(this, nextInterceptor);
    }

    private class BusinessContextWorkflowInboundInterceptor : WorkflowInboundInterceptor
    {
        private readonly BusinessContextInterceptor root;

        public BusinessContextWorkflowInboundInterceptor(BusinessContextInterceptor root, WorkflowInboundInterceptor next)
            : base(next)
        {
            this.root = root;
        }
    }

    private class BusinessContextActivityInboundInterceptor : ActivityInboundInterceptor
    {
        private readonly BusinessContextInterceptor root;

        public BusinessContextActivityInboundInterceptor(
            BusinessContextInterceptor root, ActivityInboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            var name = $"{IdentityContext.User.Value?.UserId} : {IdentityContext.User.Value?.ClientId}";
            var id = new GenericIdentity(name);
            var roles = new[] { "admin", "user" };
            BusinessContext.CurrentPrincipal.Value = new GenericPrincipal(id, roles);
            return Next.ExecuteActivityAsync(input);
        }
    }
}