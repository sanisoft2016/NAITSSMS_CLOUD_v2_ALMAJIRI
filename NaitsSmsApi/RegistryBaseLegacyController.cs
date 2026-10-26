using Naits.Common.DependencyInjection.Locator;
using Naits.Presentation.Web.Controllers;
using Naits.Presentation.Web.Services;

namespace Naits.Presentation.Web.Areas.Registry.Controllers
{
    [NaitsAuthorize(Roles = "Registry")]
    public class RegistryBaseController : BaseController
    {
        public RegistryBaseController()
        {
            ViewBag.NoImage = "/skin/images/common/noimage.png";
            ViewBag.AdminTheme = "sb-admin";
        }
        // GET: Registry/RegistryBase
    }
}