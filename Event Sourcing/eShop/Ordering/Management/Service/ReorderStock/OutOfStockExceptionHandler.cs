﻿using eShop.Ordering.Inquiry.StateViews;
using eShop.Ordering.Management.Events;
using eShop.Ordering.Management.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using vesa.Core.Abstractions;
using vesa.Core.Infrastructure;

namespace eShop.Ordering.Management.Service.ReorderStock;

public class OutOfStockExceptionHandler : EventHandler<OutOfStockExceptionEvent, OrderStateView>
{
    public OutOfStockExceptionHandler
    (
        IFactory<OrderStateView> defaultStateViewFactory,
        IDomainEvents domainEvents,
        IServiceProvider serviceProvider,
        IEventStore eventStore
    )
        : base(defaultStateViewFactory, domainEvents, serviceProvider, eventStore)
    {
    }

    public override async Task HandleAsync(OutOfStockExceptionEvent @event, CancellationToken cancellationToken)
    {
        var outOfStockException = @event.Exception as OutOfStockException;
        var reorderStockCommand = new ReorderStockCommand
        (
            // assign the event Id as the command Id so that when the domain generates an event,
            // it takes the command Id as the IdempotencyToken and will prevent the command from being handled twice
            @event.Id,
            outOfStockException.OrderNumber,
            outOfStockException.Items,
            @event.TriggeredBy,
            @event.SequenceNumber
        );

        using (var scope = _serviceProvider.CreateScope())
        {
            var serviceProvider = scope.ServiceProvider;
            var commandHandler = serviceProvider.GetRequiredService<ICommandHandler<ReorderStockCommand>>();
            var events = await commandHandler.HandleAsync(reorderStockCommand, new CancellationToken());
        }
    }
}
