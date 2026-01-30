using Temporalio.Activities;

namespace RoutedVersioning;

public static class Activities
{
    [Activity]
    public static string GenericActivity(string value) => value;
}