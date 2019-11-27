﻿using OpenActive.FakeDatabase.NET;
using OpenActive.NET;
using OpenActive.Server.NET.OpenBookingHelper;
using OpenActive.Server.NET.StoreBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookingSystem.AspNetFramework
{
    public class AcmeOrderStore : OrderStore<DatabaseTransaction>
    {
        /// <summary>
        /// Initiate customer cancellation for the specified OrderItems
        /// </summary>
        /// <returns>True if Order found, False if Order not found</returns>
        public override bool CustomerCancelOrderItems(OrderIdTemplate orderIdTemplate, OrderIdComponents orderId, List<OrderIdComponents> orderItemIds)
        {
            return FakeBookingSystem.Database.CancelOrderItem(orderId.uuid, orderItemIds.Select(x => x.OrderItemIdLong.Value).ToList(), true);
        }

        public override Lease CreateLease(OrderQuote orderQuote, StoreBookingFlowContext context, DatabaseTransaction databaseTransaction)
        {
            if (orderQuote.TotalPaymentDue.PriceCurrency != "GBP")
            {
                throw new OpenBookingException(new OpenBookingError(), "Unsupported currency");
            }

            // Note if no lease support, simply return null always here instead

            // In this example leasing is only supported at C2
            if (context.Stage == FlowStage.C2)
            {
                // TODO: Make the lease duration configurable
                var leaseExpires = DateTimeOffset.Now + new TimeSpan(0, 5, 0);

                var result = databaseTransaction.Database.AddLease(
                    context.OrderId.uuid,
                    context.BrokerRole == BrokerType.AgentBroker ? BrokerRole.AgentBroker : context.BrokerRole == BrokerType.ResellerBroker ? BrokerRole.ResellerBroker : BrokerRole.NoBroker,
                    context.Broker.Name,
                    context.SellerId.SellerIdLong.Value,
                    context.Customer.Email,
                    leaseExpires
                    );

                if (!result) throw new OpenBookingException(new OrderAlreadyExistsError());

                return new Lease
                {
                    LeaseExpires = leaseExpires
                };
            }
            else
            {
                return null;
            }
        }

        public override void DeleteLease(OrderIdComponents orderId)
        {
            // Note if no lease support, simply do nothing here
            FakeBookingSystem.Database.DeleteLease(orderId.uuid);
        }

        public override void CreateOrder(Order order, StoreBookingFlowContext context, DatabaseTransaction databaseTransaction)
        {
            if (order.TotalPaymentDue.PriceCurrency != "GBP")
            {
                throw new OpenBookingException(new OpenBookingError(), "Unsupported currency");
            }

            var result = databaseTransaction.Database.AddOrder(
                context.OrderId.uuid,
                context.BrokerRole == BrokerType.AgentBroker ? BrokerRole.AgentBroker : context.BrokerRole == BrokerType.ResellerBroker ? BrokerRole.ResellerBroker : BrokerRole.NoBroker,
                context.Broker.Name,
                context.SellerId.SellerIdLong.Value,
                context.Customer.Email,
                context.Payment?.Identifier,
                order.TotalPaymentDue.Price.Value);

            if (!result) throw new OpenBookingException(new OrderAlreadyExistsError());
        }

        public override void DeleteOrder(OrderIdComponents orderId)
        {
            FakeBookingSystem.Database.DeleteOrder(orderId.uuid);
        }


        protected override DatabaseTransaction BeginOrderTransaction(FlowStage stage)
        {
            return new DatabaseTransaction(FakeBookingSystem.Database);
        }

        protected override void CompleteOrderTransaction(DatabaseTransaction databaseTransaction)
        {
            databaseTransaction.CommitTransaction();
        }

        protected override void RollbackOrderTransaction(DatabaseTransaction databaseTransaction)
        {
            databaseTransaction.Database = null;
        }
    }
}
