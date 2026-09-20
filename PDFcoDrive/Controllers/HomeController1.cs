using Microsoft.AspNetCore.Mvc;

namespace PDFcoDrive.Controllers
{
    public class HomeController1 : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
