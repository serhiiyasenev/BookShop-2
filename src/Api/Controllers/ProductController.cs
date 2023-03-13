﻿using BusinessLayer.Interfaces;
using BusinessLayer.Models.Files;
using BusinessLayer.Models.Inbound;
using BusinessLayer.Models.Outbound;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ILogger<ProductController> _logger;
        private readonly AllowedExtensions _allowedExtensions;
        private readonly IFileUploadService _fileUploadService;

        public ProductController(ILogger<ProductController> logger, IProductService productService,
            IOptions<AllowedExtensions> options, IFileUploadService fileUploadService)
        {
            _logger = logger;
            _productService = productService;
            _allowedExtensions = options.Value;
            _fileUploadService = fileUploadService;
        }

        /// <summary>
        /// Create Product
        /// </summary>
        /// <param name="product"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A newly created Product item</returns>
        /// <remarks>
        /// Sample request:
        ///
        ///     {
        ///     "name": "The Three Musketeers",
        ///     "description": "You have likely heard of The Three Musketeers! This story has been reproduced into films, TV series, and other novels..." ,
        ///     "author": "Alexandre Dumas",
        ///     "price": 12.50,
        ///     "imageUrl": "ftp://book.shop/downloads/image.jpg"
        ///     }
        ///
        /// </remarks>
        /// <response code="201">Returns the newly created item</response>
        /// <response code="400">If the item is incorrect</response>
        /// <remarks>
        /// The return newly created Product
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(201, Type = typeof(ProductOutbound))]
        [ProducesResponseType(400, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> AddProduct(ProductInbound product, CancellationToken cancellationToken = default)
        {
            var createdProduct = await _productService.AddItem(product, cancellationToken);
            _logger.LogInformation("Product was created with id: '{Id}'", createdProduct.Id);
            return CreatedAtAction(nameof(AddProduct), createdProduct);
        }

        /// <summary>
        /// Upload image file to local or cloud storage
        /// </summary>
        /// <param name="image"></param>
        /// <response code="200">Returns uploaded file path</response>
        /// <response code="400">If the item is incorrect</response>
        /// <response code="500">If internal server error</response>
        /// <remarks>
        /// The return Simple Result message
        /// </remarks>
        [HttpPost]
        [Route("Image")]
        [ProducesResponseType(200, Type = typeof(SimpleResult))]
        [ProducesResponseType(400, Type = typeof(SimpleResult))]
        [ProducesResponseType(500, Type = typeof(SimpleResult))]
        public async Task<IActionResult> AddProductImage(IFormFile image)
        {
            var filName = image.FileName;
            var validationResult = ValidateFileExtension(filName, _allowedExtensions.ImageAllowed);
            if (validationResult != null) return validationResult;

            var result = await _fileUploadService.UploadFile(filName, image.OpenReadStream());

            if (result.IsSaved)
            {
                _logger.LogInformation("Image '{filName}' saved to Image Storage by path '{message}'",
                    filName, result.Message);
                return Ok(new SimpleResult { Result = result.Message });
            }
            else
            {
                _logger.LogInformation("Image '{filName}' wasn't saved to Image Storage due to '{message}'",
                    filName, result.Message);
                return StatusCode(500, new SimpleResult { Result = $"Failed to save image '{filName}' to Image Storage now." });
            }
        }

        /// <summary>
        /// Get all Products
        /// </summary>
        /// <remarks>
        /// The returns all Products from a storage
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(200, Type = typeof(ResponseModel<ProductOutbound>))]
        public async Task<IActionResult> GetAllProducts([FromQuery] RequestModel request, CancellationToken cancellationToken = default)
        {
            var products = await _productService.GetAll(request, cancellationToken);
            var result = new ResponseModel<ProductOutbound>
            {
                Items = products.FilteredItems,
                TotalCount = products.TotalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
            return Ok(result);
        }

        /// <summary>
        /// Get Product by id
        /// </summary>
        /// <remarks>
        /// The returns pointed by it's Guid Product from a storage
        /// </remarks>
        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(ProductOutbound))]
        [ProducesResponseType(404, Type = typeof(SimpleResult))]
        public async Task<IActionResult> GetProductById(Guid id)
        {
            var product = await _productService.GetItemById(id);
            return product != null ? Ok(product) : NotFound(new SimpleResult { Result = $"NotFound by id: '{id}'" });
        }

        /// <summary>
        /// Update Product by id
        /// </summary>
        /// <remarks>
        /// The returns newly updated Product by Guid
        /// </remarks>
        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(ProductOutbound))]
        [ProducesResponseType(404, Type = typeof(SimpleResult))]
        public async Task<IActionResult> UpdateProductById(Guid id, ProductInbound product)
        {
            var updatedProduct = await _productService.UpdateItemById(id, product);
            return updatedProduct != null ? Ok(updatedProduct) : NotFound(new SimpleResult { Result = $"NotFound by id: '{id}'" });
        }

        /// <summary>
        /// Delete Product by id
        /// </summary>
        /// <remarks>
        /// The return Simple Result message
        /// </remarks>
        [HttpDelete("{id}")]
        [ProducesResponseType(200, Type = typeof(SimpleResult))]
        [ProducesResponseType(404, Type = typeof(SimpleResult))]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var result = await _productService.RemoveItemById(id);
            return result > 0 ? Ok(new SimpleResult { Result = $"Product with id '{id}' was deleted" }) 
                              : NotFound(new SimpleResult { Result = $"NotFound by id: '{id}'" });
        }

        private IActionResult ValidateFileExtension(string filName, string allowedExtensions)
        {
            string fileExtension = Path.GetExtension(filName).ToLowerInvariant();

            if (!allowedExtensions.Split(";").ToList().Contains(fileExtension))
            {
                var result = new SimpleResult { Result = $"Not Allowed '{filName}', extension should be from '{allowedExtensions}'" };
                return BadRequest(result);
            }

            return null;
        }
    }
}
