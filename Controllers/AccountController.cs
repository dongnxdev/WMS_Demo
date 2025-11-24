using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WMS_Demo.Models; 

namespace WMS_Demo.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        // Khởi tạo các dịch vụ được inject.
        public AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            // Thực hiện đăng nhập.
            var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, false);
            
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Tài khoản không tồn tại hoặc sai mật khẩu.");
            return View();
        }

        // Chỉ có Admin mới có quyền truy cập trang này.
        [HttpGet]
        [Authorize(Roles = "Admin")] 
        public IActionResult Register() => View();

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Register(string email, string password, string role = "User")
        {
            var user = new IdentityUser { UserName = email, Email = email };
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // Gán vai trò cho người dùng vừa tạo.
                await _userManager.AddToRoleAsync(user, role);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}