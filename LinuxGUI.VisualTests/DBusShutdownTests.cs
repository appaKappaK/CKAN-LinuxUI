using System;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;
using Tmds.DBus.Protocol;

namespace CKAN.LinuxGUI.VisualTests
{
    [TestFixture]
    public sealed class DBusShutdownTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task DisposingDBus_DoesNotSynchronouslyCallStoppedDispatcher(bool closeConnection)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
            {
                Assert.Ignore("Requires a D-Bus session; run with dbus-run-session.");
            }

            // Avalonia 11 uses the compatibility API, so exercise that same path.
#pragma warning disable CS0618
            using var connection = new Connection(Address.Session!);
#pragma warning restore CS0618
            await connection.ConnectAsync();
            var context = new StoppedDispatcherContext();
            var observer = await connection.AddMatchAsync<string>(
                new MatchRule { Type = MessageType.Signal },
                (_, _) => "",
                (_, _, _, _) => { },
                null, null, context,
                ObserverFlags.EmitOnDispose | ObserverFlags.NoSubscribe);

            if (closeConnection)
            {
                Assert.DoesNotThrow(() => connection.Dispose());
            }
            else
            {
                Assert.DoesNotThrow(() => observer.Dispose());
            }
        }

        private sealed class StoppedDispatcherContext : SynchronizationContext
        {
            public override void Send(SendOrPostCallback d, object? state)
                => throw new TaskCanceledException("The dispatcher has stopped.");

            public override void Post(SendOrPostCallback d, object? state)
            {
                // A stopped dispatcher does not run callbacks queued during shutdown.
            }
        }
    }
}
