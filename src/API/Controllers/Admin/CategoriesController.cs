using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.Categories;
using PBL3.Shared.DTOs.Categories;
using PBL3.Shared.DTOs.Common;
using PBL3.API.Extensions;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin, Employee")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// Lấy toàn bộ cây danh mục (Recursive Tree).
        /// </summary>
        [AllowAnonymous]
        [HttpGet("tree")]
        [ProducesResponseType(typeof(ApiResult<List<CategoryTreeDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTree()
        {
            var result = await _categoryService.GetTreeAsync();
            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết 1 danh mục theo Id.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<CategoryDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _categoryService.GetByIdAsync(id);
            return result.ToActionResult(this);
        }

        /// <summary>
        /// Tạo mới danh mục.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<CategoryDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<CategoryDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
        {
            var result = await _categoryService.CreateAsync(request);

            if (!result.Success) return result.ToActionResult(this);

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật danh mục (bao gồm kiểm tra tham chiếu vòng).
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<CategoryDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<CategoryDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryRequest request)
        {
            var result = await _categoryService.UpdateAsync(id, request);
            return result.ToActionResult(this);
        }

        /// <summary>
        /// Xóa mềm danh mục (Soft Delete).
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _categoryService.DeleteAsync(id);
            return result.ToActionResult(this);
        }
    }
}
