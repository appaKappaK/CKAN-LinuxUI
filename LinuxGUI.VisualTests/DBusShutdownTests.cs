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
        [Test]
        public void FolderPicker_DBusVariantTypeIsAvailable()
        {
            Assert.That(Type.GetType("Tmds.DBus.Protocol.Variant, Tmds.DBus.Protocol"), Is.Not.Null,
                "Avalonia's Linux folder picker requires the D-Bus Variant type.");
        }

        [TestCase(false)]
        [TestCase(true)]
        [Explicit("Known upstream shutdown race in the Avalonia 11-compatible D-Bus dependency.")]
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
