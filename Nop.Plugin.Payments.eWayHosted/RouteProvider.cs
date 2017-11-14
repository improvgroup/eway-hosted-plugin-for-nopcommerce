﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.eWayHosted
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //Merchant return
            routeBuilder.MapRoute("Plugin.Payments.eWayHosted.MerchantReturn",
                 "Plugins/PaymenteWayHosted/MerchantReturn",
                 new { controller = "PaymenteWayHosted", action = "MerchantReturn" });
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
