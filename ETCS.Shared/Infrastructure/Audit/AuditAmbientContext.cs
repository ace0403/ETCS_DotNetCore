namespace ETCS.Shared.Infrastructure.Audit;

public static class AuditAmbientContext
{
    public const string RecordedItemKey = "Etcs.Audit.EntityRecorded";

    private static readonly AsyncLocal<AuditRequestScope?> CurrentScope = new();

    public static AuditRequestScope? Current => CurrentScope.Value;

    public static IDisposable Begin(AuditRequestScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var prior = CurrentScope.Value;
        CurrentScope.Value = scope;
        return new PopScope(prior);
    }

    private sealed class PopScope : IDisposable
    {
        private readonly AuditRequestScope? _prior;
        private bool _disposed;

        public PopScope(AuditRequestScope? prior)
        {
            _prior = prior;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CurrentScope.Value = _prior;
        }
    }
}
