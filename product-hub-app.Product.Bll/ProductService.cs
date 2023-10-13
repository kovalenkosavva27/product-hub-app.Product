using MessagePack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using product_hub_app.Product.Bll.DbConfiguration;
using product_hub_app.Product.Contracts.Commands.ProductCommands;
using product_hub_app.Product.Contracts.Dto;
using product_hub_app.Product.Contracts.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace product_hub_app.Product.Bll
{
    public class ProductService : IProductService
    {
        private readonly ProductDbContext _dbContext;
        private readonly IDistributedCache _cache;

        public ProductService(ProductDbContext dbContext, IDistributedCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        public async Task<IEnumerable<ProductDto>> GetAllProducts(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cachedData = await _cache.GetStringAsync("AllProducts", token: cancellationToken);

            if (cachedData != null)
            {
                return JsonConvert.DeserializeObject<IEnumerable<ProductDto>>(cachedData);
            }
            else
            {
                var products = await _dbContext.Products
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var productDtos = products.Select(MapToDto);

                var serializedData = JsonConvert.SerializeObject(productDtos);
                await _cache.SetStringAsync("Product_AllProducts", serializedData, token: cancellationToken);

                return productDtos;
            }
        }


        public async Task<ProductDto> GetProductById(string productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cachedData = await _cache.GetStringAsync($"Product_{productId}", token: cancellationToken);

            if (cachedData != null)
            {
                return JsonConvert.DeserializeObject<ProductDto>(cachedData);
            }
            else
            {
                var product = await _dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);
                if (product == null)
                {
                    throw new ArgumentException("Продукт не найден.");
                }

                var productDto = MapToDto(product);

                var serializedData = JsonConvert.SerializeObject(productDto);
                await _cache.SetStringAsync($"Product_{productId}", serializedData, token: cancellationToken);

                return productDto;
            }
        }


        public async Task<ProductDto> CreateProduct(ProductCreateCommand createCommand, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var product = new Contracts.Models.Product
            {
                ProductId = Guid.NewGuid().ToString(),
                Name = createCommand.Name,
                Description = createCommand.Description,
                Price = createCommand.Price,
                QuantityInStock = createCommand.QuantityInStock
            };

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var productDto = MapToDto(product);
            var serializedData = JsonConvert.SerializeObject(productDto);
            await _cache.SetStringAsync($"Product_{product.ProductId}", serializedData, token: cancellationToken);
            var allProductsCachedData = await _cache.GetStringAsync("AllProducts", token: cancellationToken);

            if (allProductsCachedData != null)
            {
                var allProducts = JsonConvert.DeserializeObject<List<ProductDto>>(allProductsCachedData);
                allProducts.Add(productDto);

                var updatedData = JsonConvert.SerializeObject(allProducts);
                await _cache.SetStringAsync("AllProducts", updatedData, token: cancellationToken);
            }

            return productDto;
        }

        public async Task<ProductDto> UpdateProduct(string productId, ProductUpdateCommand updateCommand, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);
            if (product == null)
            {
                throw new ArgumentException("Продукт не найден.");
            }

            product.Name = updateCommand.Name;
            product.Description = updateCommand.Description;
            product.Price = updateCommand.Price;
            product.QuantityInStock = updateCommand.QuantityInStock;

            await _dbContext.SaveChangesAsync(cancellationToken);
            var productDto = MapToDto(product);
            var serializedData = JsonConvert.SerializeObject(productDto);
            await _cache.SetStringAsync($"Product_{product.ProductId}", serializedData, token: cancellationToken);
            var allProductsCachedData = await _cache.GetStringAsync("AllProducts", token: cancellationToken);

            if (allProductsCachedData != null)
            {
                var allProducts = JsonConvert.DeserializeObject<List<ProductDto>>(allProductsCachedData);
                var indexToUpdate = allProducts.FindIndex(p => p.ProductId == productId);

                if (indexToUpdate != -1)
                {
                    allProducts[indexToUpdate] = productDto;
                    var updatedData = JsonConvert.SerializeObject(allProducts);
                    await _cache.SetStringAsync("AllProducts", updatedData, token: cancellationToken);
                }
            }

            return productDto;
        }

        public async Task<bool> DeleteProduct(string productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);
            if (product == null)
            {
                throw new ArgumentException("Продукт не найден.");
            }

            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _cache.RemoveAsync($"Product_{product.ProductId}", cancellationToken);
            var allProductsCachedData = await _cache.GetStringAsync("AllProducts", token: cancellationToken);

            if (allProductsCachedData != null)
            {
                var allProducts = JsonConvert.DeserializeObject<List<ProductDto>>(allProductsCachedData);
                allProducts.RemoveAll(p => p.ProductId == productId);

                var updatedData = JsonConvert.SerializeObject(allProducts);
                await _cache.SetStringAsync("AllProducts", updatedData, token: cancellationToken);
            }

            return true;
        }
        public static ProductDto MapToDto(Contracts.Models.Product product)
        {
            return new ProductDto(
                product.ProductId,
                product.Name,
                product.Description,
                product.Price,
                product.QuantityInStock
            );
        }
    }
}
