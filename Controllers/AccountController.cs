using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS_Demo.Models;
using WMS_Demo.Helpers; 
using Microsoft.AspNetCore.Authorization;
using WMS_Demo.Data;

namespace WMS_Demo.Controllers
{
    [Authorize] // Phải đăng nhập mới được vào controller này
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private readonly WmsDbContext _context;

        private const int DefaultPageSize = 10;

        // Tiêm (Inject) các dịch vụ của Identity để sử dụng
        public AccountController(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            WmsDbContext context
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        // --- PHẦN LOGIN / LOGOUT ---

        [HttpGet]
        [AllowAnonymous] 
        public IActionResult Login()
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Xin hãy nhập đủ thông tin");
                return View();
            }

            // Thực hiện đăng nhập, lockoutOnFailure: false để đỡ bị khóa khi nhập sai nhiều
            var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
            
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Tài khoản hoặc mật khẩu sai. Hãy thử lại.");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        // --- PHẦN QUẢN LÝ NHÂN VIÊN (CRUD) ---

        // GET: Danh sách nhân viên
        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> Index(string searchString, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;

            var users = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchLower = searchString.ToLower();
                users = users.Where(u => u.UserName.ToLower().Contains(searchLower) || 
                                         u.Email.ToLower().Contains(searchLower) ||
                                         u.FullName.ToLower().Contains(searchLower) ||
                                         u.StaffCode.ToLower().Contains(searchLower));
            }

            users = users.OrderBy(u => u.StaffCode);

            return View(await PaginatedList<ApplicationUser>.CreateAsync(users, pageNumber ?? 1, DefaultPageSize));
        }

        // GET: Tạo mới nhân viên
        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View();

        // GET: Xem chi tiết nhân viên
        // [Authorize(Roles = "Admin")] // Tạm thời gỡ bỏ attribute này để xử lý logic kiểm tra quyền bên trong
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            // --- Logic phân quyền ---
            // Lấy ID của người dùng đang đăng nhập
            var currentUserId = _userManager.GetUserId(User);
            // Kiểm tra xem có phải là Admin không
            var isAdmin = User.IsInRole("Admin");

            // Nếu không phải Admin và cũng không phải đang xem hồ sơ của chính mình -> Cấm
            if (!isAdmin && currentUserId != id)
            {
                return Forbid(); // Trả về lỗi 403 Forbidden
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Lấy thêm Role để hiển thị 
            var roles = await _userManager.GetRolesAsync(user);
            ViewData["Role"] = roles.FirstOrDefault() ?? "Không có";

            return View(user);
        }
        // POST: Tạo mới nhân viên
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ApplicationUser model, string password, string role = "User")
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email và mật khẩu không được để trống.");
                return View(model);
            }
            var emailInUse = await _userManager.Users.AnyAsync(u => u.Email == model.Email);
            // Kiểm tra trùng mã nhân viên nếu mã nhân viên được nhập
            var staffCodeInUse = !string.IsNullOrEmpty(model.StaffCode) && await _userManager.Users.AnyAsync(u => u.StaffCode == model.StaffCode);
            
            if(emailInUse)
            {
                ModelState.AddModelError("", "Email này đã được sử dụng.");
                return View(model);
            }

            if(staffCodeInUse)
            {
                 ModelState.AddModelError("", "Mã nhân viên này đã được sử dụng.");
                 return View(model);
            }
            var user = new ApplicationUser 
            { 
                UserName = model.Email, // Mặc định lấy email làm username cho dễ nhớ
                Email = model.Email,
                FullName = model.FullName,
                StaffCode = model.StaffCode,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // Nếu role chưa có thì tạo (đề phòng)
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
                
                await _userManager.AddToRoleAsync(user, role);
                TempData["Success"] = $"Thêm mới thành công nhân sự: {user.FullName}";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        // GET: Sửa nhân viên
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: Sửa nhân viên
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ApplicationUser model)
        {
            if (id != model.Id) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            model.StaffCode = model.StaffCode?.Trim();
            // var emailInUse = await _userManager.Users.AnyAsync(u => u.Email == model.Email);
            var staffCodeInUse = await _userManager.Users.AnyAsync(u => u.StaffCode == model.StaffCode);
            if (staffCodeInUse && user.StaffCode?.Trim() != model.StaffCode)
            {
                ModelState.AddModelError("", "Mã nhân viên này đã có người dùng rồi.");
                return View(model);
            }
           
            // Cập nhật thông tin
            user.FullName = model.FullName;
            user.IsActive = model.IsActive;
            user.StaffCode = model.StaffCode;
            // Không cho phép sửa Email/Username tại đây để đảm bảo tính nhất quán của logic xác thực
            
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Cập nhật thành công: {user.FullName}";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // GET: Xóa nhân viên (Xác nhận)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        // POST: Xóa nhân viên thật sự
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return RedirectToAction(nameof(Index));

            // Logic chặn xóa chính mình để tránh việc Admin xóa mình khỏi hệ thống
            if (User.Identity.Name == user.UserName)
            {
                TempData["Error"] = "Không thể tự xóa chính mình";
                return RedirectToAction(nameof(Index));
            }
            bool hasRelatedData = await _context.InboundReceipts.AnyAsync(ir => ir.UserId == id) || await _context.OutboundReceipts.AnyAsync(or => or.UserId == id);
            if (hasRelatedData)
            {
                TempData["Error"] = $"Không thể xóa nhân sự '{user.FullName}' vì đã có lịch sử hoạt động.";
                return RedirectToAction(nameof(Index));
            }
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Đã xóa nhân sự: {user.FullName}";
            }
            else 
            {
                TempData["Error"] = "Lỗi khi xóa";
            }
            
            return RedirectToAction(nameof(Index));
        }
    }
}