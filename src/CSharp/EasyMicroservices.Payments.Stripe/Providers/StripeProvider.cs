﻿using EasyMicroservices.Payments.Models;
using EasyMicroservices.Payments.Models.Requests;
using EasyMicroservices.Payments.Models.Responses;
using EasyMicroservices.Payments.Providers;
using EasyMicroservices.ServiceContracts;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EasyMicroservices.Payments.Stripe.Providers
{
    /// <summary>
    /// 
    /// </summary>
    public class StripeProvider : BasePaymentsProvider
    {
        readonly IStripeClient _Client;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="client"></param>
        public StripeProvider(string apiKey, IStripeClient client = default) : this(client)
        {
            StripeConfiguration.ApiKey = apiKey;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public StripeProvider(IStripeClient client = default)
        {
            if (client == default)
                client = StripeConfiguration.StripeClient;
            _Client = client;
        }
        /// <summary>
        /// 
        /// </summary>
        public static HttpClient HttpClient = new HttpClient();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="paymentOrderRequest"></param>
        /// <returns></returns>
        public override async Task<MessageContract<PaymentOrderResponse>> CreateOrderAsync(PaymentOrderRequest paymentOrderRequest)
        {
            var successUrl = paymentOrderRequest.GetSuccessUrl();
            successUrl.ThrowIfNullOrEmpty(nameof(successUrl));
            var cancelUrl = paymentOrderRequest.GetCancelUrl();
            if (cancelUrl.IsNullOrEmpty())
            {
                paymentOrderRequest.Urls.Add(new Models.PaymentUrl()
                {
                    Url = cancelUrl,
                    Type = DataTypes.RequestUrlType.CancelUrl
                });
            }

            var orders = await CreateOrders(paymentOrderRequest.Orders);
            var options = new SessionCreateOptions
            {
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                LineItems = orders,
                Mode = "payment",
            };
            var service = new SessionService(_Client);
            var session = await service.CreateAsync(options);
            return new PaymentOrderResponse()
            {
                Urls = new List<Models.PaymentUrl>()
                {
                    new Models.PaymentUrl()
                    {
                        Url =  session.Url,
                        Type = DataTypes.RequestUrlType.RedirectUrl
                    }
                }.ToList()
            };
        }

        async Task<List<SessionLineItemOptions>> CreateOrders(List<PaymentOrder> orders)
        {
            List<SessionLineItemOptions> items = new List<SessionLineItemOptions>();
            foreach (var item in orders)
            {
                var product = await CreateProduct(item);
                var price = await CreatePice(product, item);
                items.Add(new SessionLineItemOptions()
                {
                    Price = price.Id,
                    Quantity = 1
                });
            }
            return items;
        }
        async Task<Product> CreateProduct(PaymentOrder paymentOrder)
        {
            var priceOptions = new ProductCreateOptions
            {
                Name = paymentOrder.Name,
                DefaultPriceData = new ProductDefaultPriceDataOptions()
                {
                    Currency = paymentOrder.CurrencyCode.ToString(),
                    UnitAmountDecimal = paymentOrder.Amount
                }
            };

            var productService = new ProductService(_Client);
            var productResponse = await productService.CreateAsync(priceOptions);
            return productResponse;
        }
        async Task<Price> CreatePice(Product product, PaymentOrder paymentOrder)
        {
            var priceOptions = new PriceCreateOptions
            {
                Product = product.Id,
                UnitAmount = (long)paymentOrder.Amount,
                Currency = paymentOrder.CurrencyCode.ToString(),
                //Recurring = new PriceRecurringOptions { Interval = "month" },
                //LookupKey = "standard_monthly",
            };

            var priceService = new PriceService(_Client);
            var priceResponse = await priceService.CreateAsync(priceOptions);
            return priceResponse;
        }
    }
}