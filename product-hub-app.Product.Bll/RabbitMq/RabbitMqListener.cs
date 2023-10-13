using Microsoft.Extensions.Hosting;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading.Channels;
using Newtonsoft.Json;
using product_hub_app.Product.Bll.DbConfiguration;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using product_hub_app.Product.Contracts.Interfaces;
using product_hub_app.Product.Contracts.Dto;
using Microsoft.Extensions.Caching.Distributed;
using product_hub_app.Product.Contracts.Models;
using Microsoft.Extensions.DependencyInjection;

namespace product_hub_app.Product.Bll.RabbitMq
{
    public class RabbitMqListener : BackgroundService
    {
        private IConnection _connection;
        private IModel _createChannel;
        private IModel _updateChannel;
        private IModel _deleteChannel;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IDistributedCache _cache;
        public RabbitMqListener(IServiceScopeFactory serviceScopeFactory,IDistributedCache cache)
        {
            _cache = cache;
            _serviceScopeFactory = serviceScopeFactory;
            var factory = new ConnectionFactory { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _createChannel = _connection.CreateModel();
            _updateChannel = _connection.CreateModel();
            _deleteChannel = _connection.CreateModel();

            _createChannel.QueueDeclare(queue: "create-queue", durable: false, exclusive: false, autoDelete: true, arguments: null);
            _updateChannel.QueueDeclare(queue: "update-queue", durable: false, exclusive: false, autoDelete: true, arguments: null);
            _deleteChannel.QueueDeclare(queue: "delete-queue", durable: false, exclusive: false, autoDelete: true, arguments: null);
        }
        private async Task UpdateAllProducts(ProductDto productDto, CancellationToken cancellationToken)
        {
            var allProductsCachedData = await _cache.GetStringAsync("AllProducts", token: cancellationToken);

            if (allProductsCachedData != null)
            {
                var allProducts = JsonConvert.DeserializeObject<List<ProductDto>>(allProductsCachedData);
                var indexToUpdate = allProducts.FindIndex(p => p.ProductId == productDto.ProductId);

                if (indexToUpdate != -1)
                {
                    allProducts[indexToUpdate] = productDto;
                    var updatedData = JsonConvert.SerializeObject(allProducts);
                    await _cache.SetStringAsync("AllProducts", updatedData, token: cancellationToken);
                }
            }
        }
        private async Task<string> HandleCreateMessage(OrderProductRequestDto message, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                return JsonConvert.SerializeObject(new OrderProductCreateResponceDto(String.Empty, false));
            }
            using var scope = _serviceScopeFactory.CreateScope();
            var _dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.ProductId == message.ProductId, cancellationToken);

            if (product == null)
            {
                return JsonConvert.SerializeObject(new OrderProductUpdateDeleteResponceDto(false));
            }


            if (product == null)
            {
                return JsonConvert.SerializeObject(new OrderProductCreateResponceDto(String.Empty, false));
            }
            if (product.QuantityInStock >= message.Quantity)
            {
                product.QuantityInStock -= message.Quantity;
                await _dbContext.SaveChangesAsync(cancellationToken);
                var serializedProduct = JsonConvert.SerializeObject(product);
                await _cache.SetStringAsync($"Product_{message.ProductId}", serializedProduct, token: cancellationToken);
                await UpdateAllProducts(ProductService.MapToDto(product),cancellationToken);
                return JsonConvert.SerializeObject(new OrderProductCreateResponceDto(product.Name, true));
            }
            else
            {
                var serializedProduct = JsonConvert.SerializeObject(product);
                await _cache.SetStringAsync($"Product_{message.ProductId}", serializedProduct, token: cancellationToken);
                return JsonConvert.SerializeObject(new OrderProductCreateResponceDto(product.Name, false));
            }
        }
        private async Task<string> HandleDeleteMessage(OrderProductRequestDto message, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                return JsonConvert.SerializeObject(new OrderProductUpdateDeleteResponceDto(false));
            }
            using var scope = _serviceScopeFactory.CreateScope();
            var _dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.ProductId == message.ProductId, cancellationToken);

            if (product == null)
            {
                return JsonConvert.SerializeObject(new OrderProductUpdateDeleteResponceDto(false));
            }

            product.QuantityInStock += message.Quantity;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var serializedProduct = JsonConvert.SerializeObject(product);
            await _cache.SetStringAsync($"Product_{message.ProductId}", serializedProduct, token: cancellationToken);
            await UpdateAllProducts(ProductService.MapToDto(product), cancellationToken);
            return JsonConvert.SerializeObject(new OrderProductUpdateDeleteResponceDto(true));
        }
        private async Task<string> HandleUpdateMessage(OrderProductRequestDto message, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                return JsonConvert.SerializeObject(new OrderProductUpdateDeleteResponceDto(false));
            }
            using var scope = _serviceScopeFactory.CreateScope();
            var _dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.ProductId == message.ProductId, cancellationToken);
            if (product == null)
            {
                return JsonConvert.SerializeObject(new OrderProductUpdateDeleteResponceDto(false));
            }

            if (product.QuantityInStock >= message.Quantity)
            {
                product.QuantityInStock -= message.Quantity;
                await _dbContext.SaveChangesAsync(cancellationToken);
                var serializedProduct = JsonConvert.SerializeObject(product);
                await _cache.SetStringAsync($"Product_{message.ProductId}", serializedProduct, token: cancellationToken);
                await UpdateAllProducts(ProductService.MapToDto(product), cancellationToken);
                return JsonConvert.SerializeObject(new OrderProductUpdateDeleteResponceDto(true));
            }
            else
            {
                var serializedProduct = JsonConvert.SerializeObject(product);
                await _cache.SetStringAsync($"Product_{message.ProductId}", serializedProduct, token: cancellationToken);
                return JsonConvert.SerializeObject(new OrderProductUpdateDeleteResponceDto(false));
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var createConsumer = new EventingBasicConsumer(_createChannel);
            createConsumer.Received += async (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonConvert.DeserializeObject<OrderProductRequestDto>(content);
                Debug.WriteLine($"Получено сообщение для создания: {message}");

                var response = await HandleCreateMessage(message, stoppingToken);
                var props = _createChannel.CreateBasicProperties();
                props.CorrelationId = ea.BasicProperties.CorrelationId;

                var responseBytes = Encoding.UTF8.GetBytes(response);
                _createChannel.BasicPublish(exchange: "", routingKey: ea.BasicProperties.ReplyTo, basicProperties: props, body: responseBytes);
            };

            var updateConsumer = new EventingBasicConsumer(_updateChannel);
            updateConsumer.Received += async (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonConvert.DeserializeObject<OrderProductRequestDto>(content);
                Debug.WriteLine($"Получено сообщение для обновления: {message}");

                var response = await HandleUpdateMessage(message, stoppingToken);
                var props = _updateChannel.CreateBasicProperties();
                props.CorrelationId = ea.BasicProperties.CorrelationId;

                var responseBytes = Encoding.UTF8.GetBytes(response);
                _updateChannel.BasicPublish(exchange: "", routingKey: ea.BasicProperties.ReplyTo, basicProperties: props, body: responseBytes);
            };

            var deleteConsumer = new EventingBasicConsumer(_deleteChannel);
            deleteConsumer.Received += async (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonConvert.DeserializeObject<OrderProductRequestDto>(content);
                Debug.WriteLine($"Получено сообщение для удаления: {message}");

                var response = await HandleDeleteMessage(message, stoppingToken);
                var props = _deleteChannel.CreateBasicProperties();
                props.CorrelationId = ea.BasicProperties.CorrelationId;

                var responseBytes = Encoding.UTF8.GetBytes(response);
                _deleteChannel.BasicPublish(exchange: "", routingKey: ea.BasicProperties.ReplyTo, basicProperties: props, body: responseBytes);
            };

            _deleteChannel.BasicConsume("delete-queue", false, deleteConsumer);
            _createChannel.BasicConsume("create-queue", false, createConsumer);
            _updateChannel.BasicConsume("update-queue", false, updateConsumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _createChannel.Close();
            _updateChannel.Close();
            _deleteChannel.Close();
            _connection.Close();
            base.Dispose();
        }
    }

}
