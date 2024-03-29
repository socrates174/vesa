﻿using IoCloud.Shared.Settings.Abstractions;

namespace IoCloud.Shared.Settings.Infrastructure
{
    public class EventHubProducerConfiguration : IEventHubProducerConfiguration
    {
        public string ConnectionStringKey { get; set; }
        public int MaximumRetries { get; set; } = 3;
        public int MaximumDelay { get; set; } = 1;
        public int TryTimeOut { get; set; } = 1;
        public string EventHubsRetryMode { get; set; } = "Exponential";
    }
}
