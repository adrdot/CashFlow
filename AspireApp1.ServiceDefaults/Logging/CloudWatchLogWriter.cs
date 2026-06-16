using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspireApp1.ServiceDefaults.Logging;

internal sealed class CloudWatchLogWriter : IHostedService, IAsyncDisposable
{
    private readonly CloudWatchOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ConcurrentQueue<InputLogEvent> _queue = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _backgroundTask;

    private AmazonCloudWatchLogsClient? _client;
    private string? _logGroupName;
    private string? _logStreamName;
    private string? _sequenceToken;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public CloudWatchLogWriter(
        IOptions<CloudWatchOptions> options,
        IHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        _backgroundTask = Task.Run(() => RunFlushLoopAsync(_shutdown.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        await _shutdown.CancelAsync();
        if (_backgroundTask is not null)
        {
            await _backgroundTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        await FlushAsync(forceAll: true, CancellationToken.None).ConfigureAwait(false);
    }

    public void Enqueue(LogLevel logLevel, string category, EventId eventId, string message, Exception? exception)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var timestamp = DateTime.UtcNow;
        var payload = BuildMessage(logLevel, category, eventId, message, exception);
        _queue.Enqueue(new InputLogEvent
        {
            Message = payload,
            Timestamp = timestamp
        });
    }

    private async Task RunFlushLoopAsync(CancellationToken cancellationToken)
    {
        var flushPeriod = TimeSpan.FromSeconds(Math.Max(1, _options.FlushPeriodSeconds));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(flushPeriod, cancellationToken).ConfigureAwait(false);
                await FlushAsync(forceAll: false, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"CloudWatch log flush failed: {ex}");
            }
        }
    }

    private async Task FlushAsync(bool forceAll, CancellationToken cancellationToken)
    {
        if (_queue.IsEmpty)
        {
            return;
        }

        await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            var batchSize = Math.Max(1, _options.BatchSize);
            var events = new List<InputLogEvent>(batchSize);

            while (_queue.TryDequeue(out var logEvent))
            {
                events.Add(logEvent);
                if (!forceAll && events.Count >= batchSize)
                {
                    break;
                }
            }

            if (events.Count == 0 || _client is null || _logGroupName is null || _logStreamName is null)
            {
                return;
            }

            events.Sort(static (left, right) => left.Timestamp.CompareTo(right.Timestamp));

            var request = new PutLogEventsRequest
            {
                LogGroupName = _logGroupName,
                LogStreamName = _logStreamName,
                LogEvents = events,
                SequenceToken = _sequenceToken
            };

            try
            {
                var response = await _client.PutLogEventsAsync(request, cancellationToken).ConfigureAwait(false);
                _sequenceToken = response.NextSequenceToken;
            }
            catch (InvalidSequenceTokenException ex)
            {
                _sequenceToken = ex.ExpectedSequenceToken;
                request.SequenceToken = _sequenceToken;
                var response = await _client.PutLogEventsAsync(request, cancellationToken).ConfigureAwait(false);
                _sequenceToken = response.NextSequenceToken;
            }
            catch (DataAlreadyAcceptedException ex)
            {
                _sequenceToken = ex.ExpectedSequenceToken;
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task<bool> EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return true;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is not null)
            {
                return true;
            }

            var credentials = new BasicAWSCredentials(
                Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test",
                Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test");

            var config = new AmazonCloudWatchLogsConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region)
            };

            if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
            {
                config.ServiceURL = _options.ServiceUrl;
                config.UseHttp = _options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                config.AuthenticationRegion = _options.Region;
            }

            _client = new AmazonCloudWatchLogsClient(credentials, config);
            _logGroupName = _options.ResolveLogGroupName(_environment.ApplicationName);
            _logStreamName = $"{Environment.MachineName}/{DateTime.UtcNow:yyyy/MM/dd/HH-mm-ss}";

            try
            {
                await _client.CreateLogGroupAsync(new CreateLogGroupRequest
                {
                    LogGroupName = _logGroupName
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (ResourceAlreadyExistsException)
            {
            }

            try
            {
                await _client.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = _logGroupName,
                    LogStreamName = _logStreamName
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (ResourceAlreadyExistsException)
            {
            }

            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "Unable to initialize CloudWatch logging for {0}: {1}",
                _environment.ApplicationName,
                ex);
            _client?.Dispose();
            _client = null;
            return false;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static string BuildMessage(
        LogLevel logLevel,
        string category,
        EventId eventId,
        string message,
        Exception? exception)
    {
        var builder = new StringBuilder();
        builder.Append('[').Append(logLevel).Append("] ");
        builder.Append(category);

        if (eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name))
        {
            builder.Append(" (").Append(eventId.Id);
            if (!string.IsNullOrWhiteSpace(eventId.Name))
            {
                builder.Append(':').Append(eventId.Name);
            }

            builder.Append(')');
        }

        builder.Append(": ").Append(message);

        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append(exception);
        }

        return builder.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Dispose();
        _flushLock.Dispose();
        _initLock.Dispose();
        _client?.Dispose();

        if (_backgroundTask is not null)
        {
            await _backgroundTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
