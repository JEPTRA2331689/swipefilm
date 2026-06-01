using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;

namespace swipefilm.Controllers
{
    // SwipeFilm.API/Controllers/AuthController.cs
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly AuthManager _authManager;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            AuthManager jwtService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _authManager = jwtService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var user = new User
            {
                Email = dto.Email,
                UserName = dto.Email,
                DisplayName = dto.DisplayName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { token = _authManager.GenerateToken(user) });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user is null)
                return Unauthorized("Email ou mot de passe incorrect");

            var result = await _signInManager
                .CheckPasswordSignInAsync(user, dto.Password, false);

            if (!result.Succeeded)
                return Unauthorized("Email ou mot de passe incorrect");

            await _userManager.UpdateAsync(user);

            return Ok(new { token = _authManager.GenerateToken(user) });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();

            return Ok(new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.AvatarUrl,
                user.CreatedAt
            });
        }
    }

    // DTOs
    public record RegisterDto(string Email, string Password, string DisplayName);
    public record LoginDto(string Email, string Password);
}
