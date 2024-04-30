﻿using eShop.Ordering.Database.SQL.Context;
using eShop.Ordering.Database.SQL.Entities;
using eShop.Ordering.Inquiry.Service.GetCustomerOrders;
using eShop.Ordering.Inquiry.Service.GetDailyOrders;
using eShop.Ordering.Inquiry.Service.GetOrder;
using eShop.Ordering.Inquiry.Service.GetStatusOrders;
using eShop.Ordering.Inquiry.StateViews;
using eShop.Ordering.Management.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using vesa.Blob.Extensions;
using vesa.Core.Abstractions;
using vesa.Core.Infrastructure;
using vesa.Cosmos.Extensions;
using vesa.Cosmos.Infrastructure;
using vesa.File.Extensions;
using vesa.File.Infrastructure;
using vesa.SQL.Extensions;
using vesa.SQL.Infrastructure;

namespace eShop.Ordering.Inquiry.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Configure(this IServiceCollection services, IConfiguration configuration)
    {
        // settings will automatically be used by JsonConvert.SerializeObject/DeserializeObject
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        //Initialize stores - these creates the required filesystem directories on startup if they do not exist.

        // EventStore and EventStoreListener Registration
        switch (configuration["EventStore"])
        {
            case "File":
                services.AddFileEventStore(configuration);
                services.AddFileEventListeners(configuration);
                break;
            case "Blob":
                services.AddBlobEventStore(configuration);
                services.AddBlobEventStoreListener(configuration);
                break;
            case "SQL":
                services.AddSQLStore<OrderingContext>(configuration, ServiceLifetime.Scoped);
                services.AddSQLEventListeners(configuration);
                services.AddScoped<IEventStore, SQLEventStore>();
                break;
            case "Cosmos":
                services.AddCosmosStateViewStore(configuration, "StateViewCosmosContainerConfiguration");
                services.AddCosmosEventStore(configuration);
                services.AddCosmosEventStoreListener(configuration);
                break;
        }
  
        switch (configuration["StateViewStore"])
        {
            case "File":
                services.AddScoped(typeof(IStateViewStore<>), typeof(FileStateViewStore<>));
                break;
            case "SQL":
                services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
                if (configuration["EventStore"] != "SQL")
                {
                    services.AddSQLStore<OrderingContext>(configuration, ServiceLifetime.Scoped);
                }
                services.AddScoped(typeof(IStateViewStore<OrderStateView>), typeof(SQLStateViewStore<OrderStateViewJson, OrderStateView>));
                services.AddScoped(typeof(IStateViewStore<CustomerOrdersStateView>), typeof(SQLStateViewStore<CustomerOrdersStateViewJson, CustomerOrdersStateView>));
                services.AddScoped(typeof(IStateViewStore<StatusOrdersStateView>), typeof(SQLStateViewStore<StatusOrdersStateViewJson, StatusOrdersStateView>));
                services.AddScoped(typeof(IStateViewStore<DailyOrdersStateView>), typeof(SQLStateViewStore<DailyOrdersStateViewJson, DailyOrdersStateView>));
                break;
            case "Cosmos":
                if (configuration["EventStore"] != "Cosmos")
                {
                    services.AddCosmosClient(configuration);
                    services.AddCosmosContainerConfiguration(configuration);
                    services.InitializeDatabase(configuration);
                }
                services.AddCosmosContainerConfiguration<IStateView>(configuration, "StateViewCosmosContainerConfiguration");
                services.AddTransient(typeof(IStateViewStore<>), typeof(CosmosStateViewStore<>));
                break;
        }

        // OrderStateView updaters
        services.AddScoped<IEventHandler<OrderPlacedEvent>, OrderStateViewUpdater>();
        services.AddScoped<IEventHandler<OrderCancelledEvent>, OrderStateViewUpdater>();
        services.AddScoped<IEventHandler<OrderReturnedEvent>, OrderStateViewUpdater>();

        // CustomerOrdersStateView updaters
        services.AddScoped<IEventHandler<OrderPlacedEvent>, CustomerOrdersStateViewUpdater>();
        services.AddScoped<IEventHandler<OrderCancelledEvent>, CustomerOrdersStateViewUpdater>();
        services.AddScoped<IEventHandler<OrderReturnedEvent>, CustomerOrdersStateViewUpdater>();

        // StatusOrdersStateView updaters
        services.AddScoped<IEventHandler<OrderPlacedEvent>, StatusOrdersStateViewUpdater>();
        services.AddScoped<IEventHandler<OrderCancelledEvent>, StatusOrdersStateViewUpdater>();
        services.AddScoped<IEventHandler<OrderReturnedEvent>, StatusOrdersStateViewUpdater>();

        //DailyOrdersStateView updaters
        services.AddScoped<IEventHandler<OrderPlacedEvent>, DailyOrdersStateViewUpdater>();
        services.AddScoped<IEventHandler<OrderCancelledEvent>, DailyOrdersStateViewUpdater>();
        services.AddScoped<IEventHandler<OrderReturnedEvent>, DailyOrdersStateViewUpdater>();

        // Event observers
        services.AddScoped<IEventObservers, EventHandlerObservers<OrderPlacedEvent>>();
        services.AddScoped<IEventObservers, EventHandlerObservers<OrderCancelledEvent>>();
        services.AddScoped<IEventObservers, EventHandlerObservers<OrderReturnedEvent>>();

        return services;
    }
}