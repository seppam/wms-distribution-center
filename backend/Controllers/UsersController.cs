using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.DTOs.User;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    [Authorize(Policy = Policies.AdminOnly)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var user = await _userService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [Authorize(Policy = Policies.AdminOnly)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var user = await _userService.UpdateAsync(id, dto);
        return user == null ? NotFound() : Ok(user);
    }

    [Authorize(Policy = Policies.AdminOnly)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
            return BadRequest(new { message = "You cannot delete your own account." });
        var result = await _userService.DeleteAsync(id);
        return result ? NoContent() : NotFound();
    }
}
