using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WMS_Demo.Models;
using Microsoft.AspNetCore.Authorization;
namespace WMS_Demo.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Report", "Reports");
        }

        public IActionResult Privacy()
        {
           return RedirectToAction("Report", "Reports");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
