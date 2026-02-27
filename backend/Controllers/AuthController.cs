using backend;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public IActionResult Login(LoginRequest request)
    {
        var token = _authService.Login(request.Username, request.Password);

        if (token == null)
            return Unauthorized("Invalid credentials");

        return Ok(new { token });
    }
}

public record LoginRequest(string Username, string Password);