﻿using Azure.Messaging.ServiceBus;
using Mango.Services.Email.Messages;
using Mango.Services.Email.Repository;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Mango.Services.Email.Messaging
{
    public class AzureServiceBusConsumer : IAzureServiceBusConsumer
    {
        private readonly EmailRepository _emailRepository;
        private readonly string serviceBusConnectionString;
        private readonly string subscriptionEmail;
        private readonly string orderPaymentProcessTopic;
        private readonly string orderUpdatePaymentResultTopic;
        private ServiceBusProcessor orderUpdatePaymentStatusProcessor;

        private readonly IConfiguration _configuration;

        public AzureServiceBusConsumer(EmailRepository emailRepository, IConfiguration configuration)
        {
            _emailRepository = emailRepository;
            _configuration = configuration;

            serviceBusConnectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            subscriptionEmail = _configuration.GetValue<string>("SubscriptionName");
            orderUpdatePaymentResultTopic = _configuration.GetValue<string>("OrderUpdatePaymentResultTopic");
            var client = new ServiceBusClient(serviceBusConnectionString);
            //Read message from the Queue
            //checkOutProcessor = client.CreateProcessor("checkoutqueue");
            orderUpdatePaymentStatusProcessor = client.CreateProcessor(orderUpdatePaymentResultTopic, subscriptionEmail);
        }

        public async Task Start()
        {
            orderUpdatePaymentStatusProcessor.ProcessMessageAsync += OnOrderPaymentUpdateReceived;
            orderUpdatePaymentStatusProcessor.ProcessErrorAsync += ErrorHandler;
            await orderUpdatePaymentStatusProcessor.StartProcessingAsync();
        }

        public async Task Stop()
        {

            await orderUpdatePaymentStatusProcessor.StopProcessingAsync();
            await orderUpdatePaymentStatusProcessor.DisposeAsync();
        }

        Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }


        private async Task OnOrderPaymentUpdateReceived(ProcessMessageEventArgs args)
        {
            var message = args.Message;
            var body = Encoding.UTF8.GetString(message.Body);

            var objMessage = JsonConvert.DeserializeObject<UpdatePaymentResultMessage>(body);

            try
            {
                await _emailRepository.SendAndLogEmail(objMessage);
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception e)
            {
                throw;
            }

        }
    }
}
