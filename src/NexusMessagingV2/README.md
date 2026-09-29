This sample shows how to expose a long-running Workflow's queries, updates, and signals as Nexus
operations. There are two self-contained examples, each in its own directory:

|                                | `CallerPattern/`                               | `OnDemandPattern/`                                              |
|--------------------------------|------------------------------------------------|-----------------------------------------------------------------|
| **Pattern**                    | Query, update, and signal an existing Workflow | Create and run Workflows on demand, and send signals to them    |
| **Who creates the Workflow?**  | The handler worker starts it on boot           | The caller starts it via a Nexus operation                      |
| **Who knows the Workflow ID?** | Only the handler                               | The caller passes a UserId; the handler derives the Workflow ID |
| **Nexus service**              | `NexusGreetingService`                         | `NexusRemoteGreetingService`                                    |

The `GreetingActivities` and `Language` enum are shared between the two patterns (in the `Common/`
directory). Each pattern has its own `GreetingWorkflow`, Nexus service interface, and handler
implementation. This highlights that the same activity logic can be exposed through Nexus in different
ways depending on whether the caller needs lifecycle control.

See each directory's README for running instructions:
- [CallerPattern README](CallerPattern/README.md)
- [OnDemandPattern README](OnDemandPattern/README.md)
