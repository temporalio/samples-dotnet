using System.Security.Principal;
using Microsoft.EntityFrameworkCore;

namespace TemporalioSamples.ContextPropagation;

// BusinessContext represents all those fun global state things
// you can't use anymore because they presumed on a Thread, not a Task, as the context.
// AsyncLocal uses ExecutionContext under the hood.
// https://github.com/dotnet/runtime/blob/16b6369b7509e58c35431f05681a9f9e5d10afaa/src/libraries/System.Private.CoreLib/src/System/Threading/AsyncLocal.cs#L45
// So don't be scared by the state things here...
public static class BusinessContext
{
    public static readonly AsyncLocal<IPrincipal> CurrentPrincipal = new();
    public static readonly AsyncLocal<DbContext> CurrentDbContext = new();
}