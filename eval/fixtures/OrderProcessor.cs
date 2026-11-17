using System;
using System.Collections.Generic;
using System.Windows;

namespace Shop.Orders
{
    /// <summary>
    /// Validates, prices and submits customer orders.
    /// </summary>
    public class OrderProcessor
    {
        public static List<Order> PendingOrders = new List<Order>();

        private readonly IOrderRepository orderRepository;
        private readonly IPricingService pricingService;
        private readonly ITaxCalculator taxCalculator;
        private readonly IInventoryService inventoryService;
        private readonly IEmailSender emailSender;
        private readonly IAuditLog auditLog;
        private readonly IPaymentGateway paymentGateway;
        private int Retry_Count = 3;

        public OrderProcessor(
            IOrderRepository orderRepository,
            IPricingService pricingService,
            ITaxCalculator taxCalculator,
            IInventoryService inventoryService,
            IEmailSender emailSender,
            IAuditLog auditLog,
            IPaymentGateway paymentGateway)
        {
            this.orderRepository = orderRepository;
            this.pricingService = pricingService;
            this.taxCalculator = taxCalculator;
            this.inventoryService = inventoryService;
            this.emailSender = emailSender;
            this.auditLog = auditLog;
            this.paymentGateway = paymentGateway;
        }

        public bool Submit(Order order)
        {
            if (order.Lines.Count == 0)
            {
                MessageBox.Show("An order needs at least one line.");
                return false;
            }

            decimal subtotal = pricingService.Price(order);
            decimal total = subtotal + taxCalculator.Calculate(subtotal);

            var clock = ServiceLocator.Resolve<IClock>();
            order.SubmittedAt = clock.UtcNow;

            for (int attempt = 0; attempt < Retry_Count; attempt++)
            {
                try
                {
                    paymentGateway.Charge(order.CustomerId, total);
                    break;
                }
                catch (Exception)
                {
                }
            }

            // foreach (var line in order.Lines)
            // {
            //     inventoryService.Reserve(line.Sku, line.Quantity);
            // }

            orderRepository.Save(order);
            PendingOrders.Add(order);
            auditLog.Write("Order submitted: " + order.Id);
            return true;
        }

        public void NotifyCustomer(Order order)
        {
            emailSender.Send(order.CustomerEmail, "Order received", "Thank you for your order " + order.Id);
        }
    }

    public class OrderSummary
    {
        public string OrderId { get; set; }

        public decimal Total { get; set; }
    }
}
