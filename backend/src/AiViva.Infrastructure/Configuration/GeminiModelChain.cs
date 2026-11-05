using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Configuration;

namespace AiViva.Infrastructure.Configuration;

/// <summary>
/// The Gemini model called first (<c>Gemini:Model</c>) followed by fallback models
/// (<c>Gemini:FallbackModels</c>, comma separated or a JSON array). Rate limits and overload are
/// per model, so the next model is tried when one is rate limited, overloaded, unknown or too slow.
/// </summary>
public sealed class GeminiModelChain
{
    public const string DefaultModel = "gemini-3.8-flash";

    public GeminiModelChain(IConfiguration configuration, TimeSpan defaultFallbackAfter)
    {
        var models = new List<string>();
        void Add(string? model)
        {
            model = model?.Trim();
            if (!string.IsNullOrEmpty(model) && !models.Contains(model, StringComparer.OrdinalIgnoreCase)) models.Add(model);
        }

        Add(configuration["Gemini:Model"]);
        if (models.Count == 0) models.Add(DefaultModel);
        var fallbacks = configuration.GetSection("Gemini:FallbackModels");
        foreach (var model in fallbacks.Value is { } list ? list.Split([',', ';']) : fallbacks.GetChildren().Select(c => c.Value))
            Add(model);
        Models = models;
        FallbackAfter = double.TryParse(configuration["Gemini:FallbackAfterSeconds"], NumberStyles.Float,
            CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds) : defaultFallbackAfter;
        RetryDelay = TimeSpan.FromSeconds(double.TryParse(configuration["Gemini:RetryDelaySeconds"], NumberStyles.Float,
            CultureInfo.InvariantCulture, out var delay) && delay >= 0 ? delay : 3);
    }

    public IReadOnlyList<string> Models { get; }

    public string Primary => Models[0];

    /// <summary>How long a model gets before the next one is tried. The last model keeps the HttpClient timeout.</summary>
    public TimeSpan FallbackAfter { get; }

    /// <summary>Failures another model may not share. Other client errors (e.g. 400) would fail on every model.</summary>
    public static bool IsModelSpecific(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.NotFound or HttpStatusCode.RequestTimeout ||
        (int)status >= 500;

    /// <summary>
    /// Runs <paramref name="attempt"/> with each model in turn. An attempt signals a model-specific
    /// failure by throwing <see cref="GeminiModelFailure"/>; the wrapped error surfaces if no model succeeds.
    /// </summary>
    public async Task<T> RunAsync<T>(Func<string, CancellationToken, Task<T>> attempt, CancellationToken ct,
        Action<string, string, Exception>? onFallback = null)
    {
        // Free-tier overload (429/503) is usually brief, so the whole chain is retried once after a short pause.
        for (var round = 0; ; round++)
        {
            try
            {
                return await RunChainAsync(attempt, ct, onFallback, finalRound: round == RetryRounds);
            }
            catch (GeminiModelFailure failure) when (round < RetryRounds && !ct.IsCancellationRequested)
            {
                onFallback?.Invoke(Models[^1], Models[0], failure.Error);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }

    private const int RetryRounds = 1;

    /// <summary>Pause before the whole chain is tried again (<c>Gemini:RetryDelaySeconds</c>, default 3 s).</summary>
    public TimeSpan RetryDelay { get; }

    private async Task<T> RunChainAsync<T>(Func<string, CancellationToken, Task<T>> attempt, CancellationToken ct,
        Action<string, string, Exception>? onFallback, bool finalRound)
    {
        for (var index = 0; ; index++)
        {
            var model = Models[index];
            var isLast = index == Models.Count - 1;
            using var budget = isLast ? null : CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget?.CancelAfter(FallbackAfter);
            try
            {
                return await attempt(model, budget?.Token ?? ct);
            }
            catch (Exception exception) when (!isLast && !ct.IsCancellationRequested &&
                exception is GeminiModelFailure or OperationCanceledException)
            {
                onFallback?.Invoke(model, Models[index + 1],
                    exception is GeminiModelFailure failure ? failure.Error : new TimeoutException(
                        $"No response within {FallbackAfter.TotalSeconds:0} s.", exception));
            }
            catch (GeminiModelFailure failure) when (finalRound)
            {
                ExceptionDispatchInfo.Capture(failure.Error).Throw();
                throw;
            }
        }
    }
}

/// <summary>Wraps the caller's own error for a failure that is specific to one model.</summary>
public sealed class GeminiModelFailure(Exception error) : Exception(error.Message, error)
{
    public Exception Error { get; } = error;
}
