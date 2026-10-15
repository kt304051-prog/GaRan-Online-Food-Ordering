using GaRan.Services;
using GaRan.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace GaRan.ViewComponents;

public sealed class CartCountViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var cart = HttpContext.Session.GetObject<CartVM>("CART") ?? new CartVM();
        return Content(cart.TongSoLuong.ToString());
    }
}
