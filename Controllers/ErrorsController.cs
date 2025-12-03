using Microsoft.AspNetCore.Mvc;

namespace WMS_Demo.Controllers
{
    public class ErrorsController : Controller
    {
        // Xử lý các lỗi HTTP được chuyển hướng từ middleware.
        [Route("Errors/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            switch (statusCode)
            {
                case 404:
                    // Với lỗi 404, thông báo và chuyển hướng về trang chủ.
                    TempData["Error"] = "Chức năng bạn yêu cầu đã được xóa hoặc chuyển đổi";
                    return RedirectToAction("Index","Home");
            }
         
            ViewData["ErrorMessenger"] = "Đã xảy ra lỗi";
            ViewData["ErrorCode"] = statusCode;
            // Các mã lỗi khác sẽ hiển thị trang lỗi chung.
            return View("Error"); 
        }
    }
}