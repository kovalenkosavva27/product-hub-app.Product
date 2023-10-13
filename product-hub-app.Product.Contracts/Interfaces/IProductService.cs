using product_hub_app.Product.Contracts.Commands.ProductCommands;
using product_hub_app.Product.Contracts.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace product_hub_app.Product.Contracts.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDto>> GetAllProducts(CancellationToken cancellationToken = default);
        Task<ProductDto> GetProductById(string productId, CancellationToken cancellationToken = default);
        Task<ProductDto> CreateProduct(ProductCreateCommand createCommand, CancellationToken cancellationToken = default);
        Task<ProductDto> UpdateProduct(string productId, ProductUpdateCommand updateCommand, CancellationToken cancellationToken = default);
        Task<bool> DeleteProduct(string productId, CancellationToken cancellationToken = default);
    }
}
