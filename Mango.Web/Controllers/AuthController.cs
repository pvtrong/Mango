using Mango.Web.Models;
using Mango.Web.Service.IService;
using Mango.Web.Utility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;

namespace Mango.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ITokenProvider _tokenProvider;

        public AuthController(IAuthService authService, ITokenProvider tokenProvider)
        {
            _authService = authService;
            _tokenProvider = tokenProvider;
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            LoginRequestDto loginRequestDto = new();

            return View(loginRequestDto);
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var roleList = new List<SelectListItem>() { 
                new SelectListItem() { Text = SD.RoleAdmin, Value  = SD.RoleAdmin },
                new SelectListItem() { Text = SD.RoleCustomer, Value  = SD.RoleCustomer },
            };

            ViewBag.RoleList = roleList;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            _tokenProvider.ClearToken();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginRequestDto loginRequestDto)
        {
            if(ModelState.IsValid) {
                ResponseDto? response = await _authService.LoginAsync(loginRequestDto);

                if(response != null && response.IsSuccess) {
                    LoginResponseDto loginResponseDto = JsonConvert.DeserializeObject<LoginResponseDto>(Convert.ToString(response.Result));

                    await SignInUser(loginResponseDto);
                    _tokenProvider.SetToken(loginResponseDto.Token);

                    return RedirectToAction("Index", "Home");
                } else {
                    TempData["error"] = response.Message;
                }
            }

            return View(loginRequestDto);
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterationRequestDto model)
        {
            if(ModelState.IsValid) {
                ResponseDto? response = await _authService.RegisterAsync(model);
                ResponseDto? assignRoleResponse;

                if(response != null && response.IsSuccess) {
                    if (string.IsNullOrEmpty(model.Role)) {
                        model.Role = SD.RoleCustomer;
                    }

                    assignRoleResponse = await _authService.AssignRoleAsync(model);

                    if (assignRoleResponse != null && assignRoleResponse.IsSuccess) {
                        TempData["success"] = "Register successfully";
                        return RedirectToAction(nameof(Login));
                    } else {
                        TempData["error"] = response.Message;
                    }
                } else {
                    TempData["error"] = response.Message;
                }
            }

            var roleList = new List<SelectListItem>() {
                new SelectListItem() { Text = SD.RoleAdmin, Value  = SD.RoleAdmin },
                new SelectListItem() { Text = SD.RoleCustomer, Value  = SD.RoleCustomer },
            };

            ViewBag.RoleList = roleList;

            return View(model);
        }

        private async Task SignInUser(LoginResponseDto model)
        {
            var handler = new JwtSecurityTokenHandler();

            var jwt = handler.ReadJwtToken(model.Token);

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Email, 
                jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Email).Value));
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub,
                jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Sub).Value));
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Name,
                jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Name).Value));

            identity.AddClaim(new Claim(ClaimTypes.Name,
                jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Email).Value));
            identity.AddClaim(new Claim(ClaimTypes.Role,
                jwt.Claims.FirstOrDefault(u => u.Type == "role").Value));

            var pricipal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, pricipal);
        }
    }
}
