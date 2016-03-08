using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.eWayHosted
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            //Merchant return
            routes.MapRoute("Plugin.Payments.eWayHosted.MerchantReturn",
                 "Plugins/PaymenteWayHosted/MerchantReturn",
                 new { controller = "PaymenteWayHosted", action = "MerchantReturn" },
                 new[] { "Nop.Plugin.Payments.eWayHosted.Controllers" }
            );
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
