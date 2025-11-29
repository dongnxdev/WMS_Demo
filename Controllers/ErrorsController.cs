using Microsoft.AspNetCore.Mvc;

namespace WMS_Demo.Controllers
{
    public class ErrorsController : Controller
    {
        // Route này sẽ hứng cái {0} từ Program.cs truyền sang
        [Route("Errors/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            switch (statusCode)
            {
                case 404:
                    // trả về trang index
                    TempData["Error"] = "Chức năng bạn yêu cầu đã được xóa hoặc chuyển đổi";
                    return RedirectToAction("Index","Home");
            }
         
            ViewData["ErrorMessenger"] = "Đã xảy ra lỗi";
            ViewData["ErrorCode"] = statusCode;
            return View("Error"); // Fallback về trang lỗi mặc định nếu không phải 404
        }
    }
}