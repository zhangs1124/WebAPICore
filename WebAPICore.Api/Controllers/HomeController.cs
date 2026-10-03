using Microsoft.AspNetCore.Mvc;
using WebAPICore.Api.Filters;

namespace WebAPICore.Api.Controllers;

[CustomAuth]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}