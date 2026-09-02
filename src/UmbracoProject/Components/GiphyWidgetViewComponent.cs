using Microsoft.AspNetCore.Mvc;
using UmbracoProject.Services;

namespace UmbracoProject.Components
{
    public class GiphyWidgetViewComponent(GiphyService svc) : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(string? tag = null, string? rating = null)
        {
            var gif = await svc.GetRandomAsync(tag, rating);
            return View(gif); // view handles null (renders nothing)
        }
    }

}
