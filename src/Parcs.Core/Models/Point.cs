using Parcs.Core.Models.Interfaces;
using Parcs.Net;

namespace Parcs.Core.Models
{
    public sealed class Point(long jobId, long moduleId, IManagedChannel managedChannel, IArgumentsProvider argumentsProvider) : IPoint
    {
        private readonly long _jobId = jobId;
        private readonly long _moduleId = moduleId;
        private readonly IArgumentsProvider _argumentsProvider = argumentsProvider;
        private IManagedChannel _managedChannel = managedChannel;
        private bool _managedChannelInitialized = false;
        private bool _classExecuted = false;

        public Guid Id { get; init; } = Guid.NewGuid();

        public async Task<IChannel> CreateChannelAsync()
        {
            if (_managedChannelInitialized)
            {
                return _managedChannel;
            }

            await _managedChannel.WriteSignalAsync(Signal.InitializeJob);
            await _managedChannel.WriteDataAsync(_jobId);

            var isJobInitialized = await _managedChannel.ReadBooleanAsync();
            if (isJobInitialized)
            {
                _managedChannelInitialized = true;
                return _managedChannel;
            }

            await _managedChannel.WriteDataAsync(_moduleId);
            await _managedChannel.WriteObjectAsync(_argumentsProvider.GetArguments());

            _managedChannelInitialized = true;

            return _managedChannel;
        }

        public async Task ExecuteClassAsync(string assemblyName, string className)
        {
            if (!_managedChannelInitialized)
            {
                throw new ArgumentException("No channel has been created.");
            }

            await _managedChannel.WriteSignalAsync(Signal.ExecuteClass);
            await _managedChannel.WriteDataAsync(_jobId);
            await _managedChannel.WriteDataAsync(assemblyName);
            await _managedChannel.WriteDataAsync(className);
            _classExecuted = true;
        }

        /// <summary>Has the point's daemon open an endpoint for the link <paramref name="name"/>; returns its address.</summary>
        internal async Task<string> OpenLinkAsync(string name)
        {
            EnsureLinkable();
            await _managedChannel.WriteSignalAsync(Signal.OpenLink);
            await _managedChannel.WriteDataAsync(_jobId);
            await _managedChannel.WriteDataAsync(name);
            return await ReadLinkReplyAsync(name);
        }

        /// <summary>Has the point's daemon dial <paramref name="address"/> for the link <paramref name="name"/>.</summary>
        internal async Task ConnectLinkAsync(string name, string address)
        {
            EnsureLinkable();
            await _managedChannel.WriteSignalAsync(Signal.ConnectLink);
            await _managedChannel.WriteDataAsync(_jobId);
            await _managedChannel.WriteDataAsync(name);
            await _managedChannel.WriteDataAsync(address);
            await ReadLinkReplyAsync(name);
        }

        private void EnsureLinkable()
        {
            // Once the class runs, this channel belongs to the module as its parent channel.
            if (!_managedChannelInitialized || _classExecuted)
            {
                throw new InvalidOperationException(
                    "Link points after CreateChannelAsync and before ExecuteClassAsync on each of them.");
            }
        }

        private async Task<string> ReadLinkReplyAsync(string name)
        {
            var succeeded = await _managedChannel.ReadBooleanAsync();
            var value = await _managedChannel.ReadStringAsync();
            return succeeded ? value : throw new InvalidOperationException($"Could not set up the link '{name}': {value}");
        }

        public async Task DeleteAsync() => await DisposeAsync();

        public async ValueTask DisposeAsync()
        {
            if (_managedChannel is null || _managedChannel.IsConnected is false)
            {
                return;
            }

            await _managedChannel.WriteSignalAsync(Signal.CloseConnection);

            _managedChannel?.Dispose();
            _managedChannel = null;
        }
    }
}