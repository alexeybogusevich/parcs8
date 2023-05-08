using Parcs.Core.Models.Interfaces;
using Parcs.Net;

namespace Parcs.Core.Models
{
    public sealed class Point : IPoint
    {
        private readonly long _jobId;
        private readonly long _moduleId;
        private readonly IArgumentsProvider _argumentsProvider;
        private IManagedChannel _managedChannel;
        private bool _managedChannelInitialized = false;

        public Point(long jobId, long moduleId, IManagedChannel managedChannel, IArgumentsProvider argumentsProvider)
        {
            _jobId = jobId;
            _moduleId = moduleId;
            _managedChannel = managedChannel;
            _argumentsProvider = argumentsProvider;
        }

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
            await _managedChannel.WriteDataAsync(_argumentsProvider.GetPointsNumber());
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
        }

        public async Task DeleteAsync() => await DisposeAsync();

        public async ValueTask DisposeAsync()
        {
            if (_managedChannel is null || _managedChannel.IsConnected is false)
            {
                return;
            }

            await _managedChannel.WriteSignalAsync(Signal.CancelJob);
            await _managedChannel.WriteDataAsync(_jobId);

            await _managedChannel.WriteSignalAsync(Signal.CloseConnection);

            _managedChannel?.Dispose();
            _managedChannel = null;
        }
    }
}