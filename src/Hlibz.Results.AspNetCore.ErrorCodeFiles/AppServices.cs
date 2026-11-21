using System.Diagnostics;
using System.Reflection;

using Microsoft.Extensions.Hosting;

namespace Hlibz.Results.AspNetCore.Tool;

/// <summary>
/// Gets an app's services without running it: runs its entry point until it builds its host,
/// then stops it there by throwing <see cref="HostAbortedException"/> from the host's
/// <c>HostBuilt</c> diagnostic event, as ASP.NET Core's build-time OpenAPI documents and the EF
/// Core tools do. Covers every host built with <c>HostBuilder</c> or
/// <c>HostApplicationBuilder</c>: <c>WebApplication</c>, <c>Host.CreateApplicationBuilder</c> and
/// <c>Host.CreateDefaultBuilder</c>. Nothing after <c>Build()</c> runs: no server starts and no
/// hosted service.
/// </summary>
internal sealed class AppServices
    : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private const string HostingListenerName = "Microsoft.Extensions.Hosting";

    private const string HostBuiltEventName = "HostBuilt";

    /// <summary>
    /// Whether the code running is the entry point's, which an <see cref="AsyncLocal{T}"/> follows
    /// past an <c>await</c> onto other threads; another host built in the same process (a test's)
    /// is left alone.
    /// </summary>
    private readonly AsyncLocal<bool> _inEntryPoint = new();

    private readonly TaskCompletionSource<IServiceProvider> _services =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<IDisposable> _subscriptions = [];

    private AppServices()
    {
    }

    /// <summary>
    /// Runs <paramref name="entryPoint"/> until it builds its host and returns the host's
    /// services.
    /// </summary>
    /// <param name="entryPoint">The app's entry point, a static method with no parameters or one
    /// <see cref="string"/> array.</param>
    /// <param name="timeout">How long to wait for the host.</param>
    /// <returns>The services of the first host the entry point builds.</returns>
    /// <exception cref="InvalidOperationException">The entry point returned without building a
    /// host.</exception>
    /// <exception cref="TimeoutException">No host was built within <paramref name="timeout"/>.</exception>
    public static IServiceProvider Capture(MethodInfo entryPoint, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        AppServices capture = new();
        using IDisposable subscription = DiagnosticListener.AllListeners.Subscribe(capture);
        try
        {
            Thread thread = new(() => capture.Run(entryPoint))
            {
                IsBackground = true,
                Name = "App entry point",
            };
            thread.Start();

            // GetResult rethrows the app's own exception with its stack trace.
            return capture._services.Task.WaitAsync(timeout).GetAwaiter().GetResult();
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                $"The app built no host within {timeout.TotalSeconds:0} seconds.",
                exception);
        }
        finally
        {
            lock (capture._subscriptions)
            {
                capture._subscriptions.ForEach(listener => listener.Dispose());
            }
        }
    }

    /// <inheritdoc/>
    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == HostingListenerName)
        {
            lock (_subscriptions)
            {
                _subscriptions.Add(value.Subscribe(this));
            }
        }
    }

    /// <inheritdoc/>
    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (_inEntryPoint.Value
            && value is { Key: HostBuiltEventName, Value: IHost host }
            && _services.TrySetResult(host.Services))
        {
            // Out of Build() and the entry point, which Run expects.
            throw new HostAbortedException();
        }
    }

    /// <inheritdoc/>
    public void OnCompleted()
    {
    }

    /// <inheritdoc/>
    public void OnError(Exception error)
    {
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Whatever the app throws is handed to Capture's caller.")]
    private void Run(MethodInfo entryPoint)
    {
        _inEntryPoint.Value = true;
        object?[] arguments = entryPoint.GetParameters().Length == 0 ? [] : [Array.Empty<string>()];
        try
        {
            _ = entryPoint.Invoke(null, BindingFlags.DoNotWrapExceptions, null, arguments, null);
        }
        catch (HostAbortedException)
        {
            // Thrown by OnNext once it has the services.
        }
        catch (Exception exception)
        {
            _services.TrySetException(exception);
        }

        _services.TrySetException(new InvalidOperationException(
            $"{entryPoint.DeclaringType?.Assembly.GetName().Name}'s entry point returned without "
            + "building a host (HostBuilder, HostApplicationBuilder or WebApplicationBuilder)."));
    }
}
