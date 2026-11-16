using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>
    /// Wraps a transport with a hard physical-send budget: one list send, a bounded number of detail sends,
    /// one attempt per request and a fixed minimum spacing between send starts. Once closed it never sends again.
    /// </summary>
    internal sealed class DiagnosticRequestBudget : IRiotApiTransport
    {
        public const int ListLimit = 1;
        public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(1200);
        internal const string ClockStalled = "diagnostic_budget_clock_stalled";
        private const int MaximumSpacingWaits = 100;
        private readonly IRiotApiTransport _inner;
        private readonly int _detailLimit;
        private readonly Func<TimeSpan> _elapsed;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private TimeSpan? _lastStart;
        private bool _closed, _inFlight;

        /// <param name="elapsed">A monotonic elapsed-time source (Stopwatch in production); never the wall clock, so a
        /// clock adjustment cannot shorten the spacing.</param>
        public DiagnosticRequestBudget(IRiotApiTransport inner, int detailLimit, Func<TimeSpan> elapsed,
            Func<TimeSpan, CancellationToken, Task> delay)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (detailLimit < 0) throw new ArgumentOutOfRangeException(nameof(detailLimit));
            _detailLimit = detailLimit;
            _elapsed = elapsed ?? throw new ArgumentNullException(nameof(elapsed));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        }

        public int ListSends { get; private set; }
        public int DetailSends { get; private set; }
        public int TotalSends => ListSends + DetailSends;
        public int Limit => ListLimit + _detailLimit;

        /// <summary>Permanently forbids further sends after a stop condition.</summary>
        public void Close() => _closed = true;

        /// <summary>Counts each physical attempt before it starts, so failed or cancelled sends are never under-reported.</summary>
        public async Task<RiotApiResponse> SendAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken token)
        {
            RiotRouting.ValidateTarget(uri);
            var list = IsList(uri);
            Admit(list);
            if (_inFlight) throw new InvalidOperationException("diagnostic_budget_concurrent_send");
            _inFlight = true;
            try
            {
                for (var waits = 0; _lastStart.HasValue; waits++)
                {
                    var remaining = _lastStart.Value + MinimumInterval - _elapsed();
                    if (remaining <= TimeSpan.Zero) break;
                    if (waits >= MaximumSpacingWaits) throw new InvalidOperationException(ClockStalled);
                    await _delay(WholeMilliseconds(remaining), token);
                }
                token.ThrowIfCancellationRequested();
                Admit(list);
                if (list) ListSends++; else DetailSends++;
                _lastStart = _elapsed();
                return await _inner.SendAsync(uri, headers, token);
            }
            finally { _inFlight = false; }
        }

        /// <summary>
        /// Rounds a wait up to whole milliseconds (minimum 1 ms): Task.Delay(TimeSpan) truncates sub-millisecond
        /// remainders to zero, which would otherwise spin the spacing loop until it reports a false clock stall.
        /// </summary>
        internal static TimeSpan WholeMilliseconds(TimeSpan remaining)
        {
            var milliseconds = (long)Math.Ceiling(remaining.Ticks / (double)TimeSpan.TicksPerMillisecond);
            return TimeSpan.FromTicks(Math.Max(1L, milliseconds) * TimeSpan.TicksPerMillisecond);
        }

        /// <summary>Rejects a send outside the approved plan without touching the inner transport.</summary>
        private void Admit(bool list)
        {
            if (_closed) throw new InvalidOperationException("diagnostic_budget_closed");
            if (list ? ListSends >= ListLimit : ListSends == 0 || DetailSends >= _detailLimit)
                throw new InvalidOperationException("diagnostic_budget_exhausted");
        }

        /// <summary>Distinguishes the by-PUUID ID list endpoint from match details.</summary>
        internal static bool IsList(Uri uri)
            => uri.AbsolutePath.StartsWith("/tft/match/v1/matches/by-puuid/", StringComparison.Ordinal) &&
               uri.AbsolutePath.EndsWith("/ids", StringComparison.Ordinal);
    }
}
