﻿using vesa.Core.Abstractions;
using vesa.Core.Infrastructure;
using vesa.Cosmos.Abstractions;
using vesa.Cosmos.Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace vesa.Cosmos.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCosmosEventStore(this IServiceCollection services, IConfiguration configuration, string cosmosContainerConfigurationName = "EventCosmosContainerConfiguration")
    {
        services.AddCosmosClient(configuration);
        services.AddCosmosContainerConfiguration(configuration, cosmosContainerConfigurationName);
        services.InitializeDatabase(configuration);
        services.AddSingleton<IEventStore, CosmosEventStore>();
        return services;
    }

    public static IServiceCollection AddCosmosStateViewStore(this IServiceCollection services, IConfiguration configuration, string configurationName)
    {
        services.AddCosmosContainerConfiguration<IStateView>(configuration, configurationName);
        services.AddTransient(typeof(IStateViewStore<>), typeof(CosmosStateViewStore<>));
        return services;
    }

    public static IServiceCollection AddCosmosClient
    (
        this IServiceCollection services,
        IConfiguration configuration,
        string cosmosClientConfigurationSectionName = nameof(CosmosClientConfiguration)
    )
    {
        var cosmosClient = services.GetCosmosClient(configuration, cosmosClientConfigurationSectionName);
        services.AddSingleton(cosmosClient);
        return services;
    }

    public static CosmosClient GetCosmosClient
    (
        this IServiceCollection services,
        IConfiguration configuration,
        string cosmosClientConfigurationSectionName = nameof(CosmosClientConfiguration)
    )
    {
        var cosmosClientConfiguration = configuration.GetSection(cosmosClientConfigurationSectionName).Get<CosmosClientConfiguration>();
        CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions()
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            MaxRetryAttemptsOnRateLimitedRequests = cosmosClientConfiguration.MaxRetryAttemptsOnRateLimitedRequests,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(cosmosClientConfiguration.MaxRetryWaitTimeOnRateLimitedRequestsInSeconds),
        };
        return new CosmosClient(configuration[cosmosClientConfiguration.UrlKey], configuration[cosmosClientConfiguration.AuthKey], cosmosClientOptions);
    }

    public static IServiceCollection AddCosmosContainerConfiguration
    (
        this IServiceCollection services,
        IConfiguration configuration,
        string cosmosContainerConfigurationSectionName = nameof(CosmosContainerConfiguration)
    )
    {
        var cosmosContainerConfiguration = configuration.GetSection(cosmosContainerConfigurationSectionName).Get<CosmosContainerConfiguration>();
        services.AddSingleton<ICosmosContainerConfiguration>(cosmosContainerConfiguration);
        return services;
    }

    public static IServiceCollection AddCosmosContainerConfiguration<TItem>
    (
        this IServiceCollection services,
        IConfiguration configuration,
        string cosmosContainerConfigurationSectionName = nameof(CosmosContainerConfiguration)
    )
        where TItem : class
    {
        var cosmosContainerConfiguration = configuration.GetSection(cosmosContainerConfigurationSectionName).Get<CosmosContainerConfiguration<TItem>>();
        services.AddSingleton<ICosmosContainerConfiguration<TItem>>(cosmosContainerConfiguration);
        return services;
    }

    public static IServiceCollection InitializeDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceProvider = services.BuildServiceProvider();
        var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
        var containerConfigurationTypes = services.Where
        (
            s => s.ServiceType == typeof(ICosmosContainerConfiguration) ||
            (s.ServiceType.IsGenericType && s.ServiceType.GetGenericTypeDefinition() == typeof(ICosmosContainerConfiguration<>))
        )
        .Select(s => s.ServiceType);
        foreach (var containerConfigurationType in containerConfigurationTypes)
        {
            var containerConfiguration = serviceProvider.GetRequiredService(containerConfigurationType) as CosmosContainerConfiguration;
            InitializeContainer(cosmosClient, containerConfiguration);
        }

        return services;
    }

    private static void InitializeContainer(CosmosClient cosmosClient, CosmosContainerConfiguration cosmosContainerConfiguration, int throughput = 400)
    {
        var databaseResponse = cosmosClient.CreateDatabaseIfNotExistsAsync
        (
            cosmosContainerConfiguration.DatabaseName,
            throughput
        )
        .GetAwaiter()
        .GetResult();

        if (cosmosContainerConfiguration.UniqueKeyPaths != null && cosmosContainerConfiguration.UniqueKeyPaths.Any())
        {
            var uniqueKeyPolicy = new UniqueKeyPolicy();
            foreach (var uniqueKeyPath in cosmosContainerConfiguration.UniqueKeyPaths)
            {
                var uniqueKey = new UniqueKey();
                foreach (var property in uniqueKeyPath.Split(','))
                {
                    uniqueKey.Paths.Add(property);

                };
                uniqueKeyPolicy.UniqueKeys.Add(uniqueKey);
            }

            ContainerProperties containerProperties = new ContainerProperties()
            {
                Id = cosmosContainerConfiguration.ContainerName,
                PartitionKeyPath = cosmosContainerConfiguration.PartitionKeyPath,
                UniqueKeyPolicy = uniqueKeyPolicy
            };

            ContainerResponse response = databaseResponse.Database.CreateContainerIfNotExistsAsync
            (
                containerProperties,
                400
            )
            .GetAwaiter()
            .GetResult();
        }
        else
        {
            var cosmosContainerResponse = databaseResponse.Database.CreateContainerIfNotExistsAsync
            (
                cosmosContainerConfiguration.ContainerName,
                cosmosContainerConfiguration.PartitionKeyPath,
                400
            )
            .GetAwaiter()
            .GetResult();
        }
    }

    public static IServiceCollection AddChangeFeedProcessorConfiguration
    (
        this IServiceCollection services,
        IConfiguration configuration,
        string changeFeedProcessorConfigurationSectionName = nameof(ChangeFeedProcessorConfiguration)
    )
    {
        var changeFeedProcessorConfiguration = configuration.GetSection(changeFeedProcessorConfigurationSectionName).Get<ChangeFeedProcessorConfiguration>();
        services.AddSingleton<IChangeFeedProcessorConfiguration>(changeFeedProcessorConfiguration);
        return services;
    }

    public static IServiceCollection AddCosmosEventStoreListener(this IServiceCollection services, IConfiguration configuration)
    {
        //services.AddLeaseCosmosContainer(configuration);
        services.AddChangeFeedProcessorConfiguration(configuration);
        services.AddSingleton<IChangeFeedProcessorFactory<JObject>, EventStoreChangeFeedProcessorFactory>();
        services.AddSingleton<IEventListener, CosmosEventStoreListener>();
        services.AddSingleton<CosmosEventStoreListener>();
        services.AddSingleton<IEventProcessor, EventProcessor>();
        return services;
    }

    private static IServiceCollection AddLeaseCosmosContainer(this IServiceCollection services, IConfiguration configuration, string leaseCosmosContainerConfigurationName = "LeaseCosmosContainerConfiguration")
    {
        services.AddCosmosClient(configuration);
        var cosmosClient = services.GetCosmosClient(configuration);
        var leaseCosmosContainerConfiguration = configuration.GetSection(leaseCosmosContainerConfigurationName).Get<CosmosContainerConfiguration>();
        InitializeContainer(cosmosClient, leaseCosmosContainerConfiguration);
        return services;
    }
}