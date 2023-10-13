using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace product_hub_app.Product.Contracts.Dto
{
    public record ProductDto(string ProductId, string Name, string Description, decimal Price, int QuantityInStock);
}
