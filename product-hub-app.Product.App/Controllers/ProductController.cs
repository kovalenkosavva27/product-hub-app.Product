using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using product_hub_app.Product.Contracts.Commands.ProductCommands;
using product_hub_app.Product.Contracts.Dto;
using product_hub_app.Product.Contracts.Interfaces;

namespace product_hub_app.Product.App.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<ProductDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(string))]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetAllProducts()
        {
            var cancellationToken = HttpContext?.RequestAborted ?? default;
            var products = await _productService.GetAllProducts(cancellationToken);
            return Ok(products);
        }

        [HttpGet("{productId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProductDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(string))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(string))]
        public async Task<ActionResult<ProductDto>> GetProductById(string productId)
        {
            var cancellationToken = HttpContext?.RequestAborted ?? default;
            try
            {
                var product = await _productService.GetProductById(productId, cancellationToken);
                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost("create-product")]
        [Authorize(Roles = "Director")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ProductDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(string))]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateCommand createCommand)
        {
            var cancellationToken = HttpContext?.RequestAborted ?? default;
            try
            {
                var product = await _productService.CreateProduct(createCommand, cancellationToken);
                return CreatedAtAction(nameof(GetProductById), new { productId = product.ProductId }, product);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("update-product/{productId}")]
        [Authorize(Roles = "Director")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProductDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(string))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(string))]
        public async Task<IActionResult> UpdateProduct(string productId, [FromBody] ProductUpdateCommand updateCommand)
        {
            var cancellationToken = HttpContext?.RequestAborted ?? default;
            try
            {
                var product = await _productService.UpdateProduct(productId, updateCommand, cancellationToken);
                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("delete-product/{productId}")]
        [Authorize(Roles = "Director")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(string))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(string))]
        public async Task<IActionResult> DeleteProduct(string productId)
        {
            var cancellationToken = HttpContext?.RequestAborted ?? default;
            try
            {
                var isDeleted = await _productService.DeleteProduct(productId, cancellationToken);
                if (isDeleted)
                {
                    return NoContent();
                }
                else
                {
                    return NotFound("Продукт не найден.");
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

}
