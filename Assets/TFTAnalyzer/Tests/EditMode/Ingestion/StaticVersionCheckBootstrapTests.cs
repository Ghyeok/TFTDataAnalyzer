using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Editor.Ingestion;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class StaticVersionCheckBootstrapTests
    {
        [TestCase(false, false, false, false, 1)]
        [TestCase(true, false, false, false, 0)]
        [TestCase(false, true, false, false, 0)]
        [TestCase(false, false, true, false, 0)]
        [TestCase(false, false, false, true, 0)]
        public void StartupUsesOneOwnedIdleCallbackAndHonorsEditorAndTestState(bool batch, bool playing, bool busy, bool testing, int calls)
        {
            var root = Path.Combine(Path.GetTempPath(), "TftBootstrap_" + Guid.NewGuid().ToString("N"));
            var host = new Host { Batch = batch, Playing = playing, Busy = busy, Testing = testing };
            var transport = new StaticVersionCheckSchedulerTests.RecordingTransport();
            using (var scheduler = new StaticVersionCheckScheduler(transport, () => DateTimeOffset.UtcNow, root, () => null))
            using (var bootstrap = new StaticVersionCheckBootstrap(scheduler, host))
            {
                try
                {
                    bootstrap.Start(); bootstrap.Start();
                    Assert.That(host.Callbacks.Count, Is.LessThanOrEqualTo(1));
                    host.Pump();
                    Assert.That(transport.Urls.Count, Is.EqualTo(calls));
                    Assert.That(host.Callbacks, Is.Empty, "Busy state must await a real state change, not spin delayCall.");
                    if (!batch)
                    {
                        host.Playing = host.Busy = host.Testing = false;
                        host.Change(); host.Change();
                        Assert.That(host.Callbacks.Count, Is.LessThanOrEqualTo(1));
                        host.Pump();
                        Assert.That(transport.Urls, Has.Count.EqualTo(1));
                    }
                    bootstrap.Dispose();
                    host.Change(); host.Reload(); host.Pump();
                    Assert.That(host.Callbacks, Is.Empty);
                }
                finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            }
        }

        [Test]
        public async Task TestStartAndReloadCancelPendingRequestsAndDropLateResults()
        {
            var root = Path.Combine(Path.GetTempPath(), "TftBootstrap_" + Guid.NewGuid().ToString("N"));
            var host = new Host();
            var pending = new TaskCompletionSource<HttpResponseData>();
            CancellationToken token = default;
            var transport = new StaticVersionCheckSchedulerTests.RecordingTransport
            { OnRequest = value => { token = value; return pending.Task; } };
            using (var scheduler = new StaticVersionCheckScheduler(transport, () => DateTimeOffset.UtcNow, root, () => null))
            using (var bootstrap = new StaticVersionCheckBootstrap(scheduler, host))
            {
                try
                {
                    bootstrap.Start(); host.Pump();
                    Assert.That(transport.Urls, Has.Count.EqualTo(1));
                    host.Testing = true; host.Change();
                    Assert.That(token.IsCancellationRequested, Is.True);
                    host.Reload();
                    pending.SetResult(StaticVersionCheckSchedulerTests.RecordingTransport.Response("[\"16.18.2\"]"));
                    await Task.Yield();
                    Assert.That(scheduler.LatestVersion, Is.Empty);
                    Assert.That(host.Callbacks, Is.Empty);
                }
                finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            }
        }

        private sealed class Host : IStaticVersionCheckHost
        {
            internal bool Batch, Playing, Busy, Testing;
            public bool IsBatchMode => Batch;
            public bool IsPlaying => Playing;
            public bool IsBusy => Busy;
            public bool IsTestRunning => Testing;
            public event Action StateChanged;
            public event Action Reloading;
            internal readonly HashSet<Action> Callbacks = new HashSet<Action>();
            public void Schedule(Action callback) => Callbacks.Add(callback);
            public void Unschedule(Action callback) => Callbacks.Remove(callback);
            internal void Change() => StateChanged?.Invoke();
            internal void Reload() => Reloading?.Invoke();
            internal void Pump()
            { var copy = new List<Action>(Callbacks); Callbacks.Clear(); foreach (var callback in copy) callback(); }
        }
    }
}
