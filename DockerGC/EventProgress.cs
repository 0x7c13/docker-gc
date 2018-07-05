namespace DockerGC
{
    using System;
    using Docker.DotNet.Models;

    class EventProgress : IProgress<JSONMessage>
    {
        internal Action<JSONMessage> _onCalled;

        void IProgress<JSONMessage>.Report(JSONMessage value)
        {
            _onCalled(value);
        }
    }
}