using Microsoft.Extensions.Logging;

namespace TemporalioSamples.Saga;

public class Saga
{
    private ILogger log;
    private Stack<Func<Task>> compensations;
    private Func<ILogger, Task> onCompensationError = default!;
    private Func<ILogger, Task> onCompensationComplete = default!;

    public Saga(ILogger logger)
    {
        log = logger;
        compensations = new Stack<Func<Task>>();
    }

    public void OnCompensationError(Func<ILogger, Task> onCompensationError)
    {
        this.onCompensationError = onCompensationError;
    }

    public void OnCompensationComplete(Func<ILogger, Task> onCompensationComplete)
    {
        this.onCompensationComplete = onCompensationComplete;
    }

    public void AddCompensation(Func<Task> compensation)
    {
        compensations.Push(compensation);
    }

    public async Task CompensateAsync()
    {
        int i = 0;
        while (compensations.Count > 0)
        {
            i++;
            var c = compensations.Pop();

            try
            {
                log.LogInformation("Attempting compensation {I}...", i);
                await c.Invoke();
                log.LogInformation("Compensation {I} successfull!", i);
            }
            catch (Exception)
            {
                /* log details of all other compensations that have not yet been made if this is a show-stopper */
                await onCompensationError(log);
                throw;
            }
        }
        await onCompensationComplete(log);
    }
}