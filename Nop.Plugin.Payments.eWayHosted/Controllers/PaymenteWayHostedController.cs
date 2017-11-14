using System;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.eWayHosted.Models;
using Nop.Services.Configuration;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Services.Security;

namespace Nop.Plugin.Payments.eWayHosted.Controllers
{
    public class PaymenteWayHostedController : BasePaymentController
    {
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly eWayHostedPaymentSettings _eWayHostedPaymentSettings;
        private readonly PaymentSettings _paymentSettings;
        private readonly IPermissionService _permissionService;

        public PaymenteWayHostedController(ISettingService settingService, 
            IPaymentService paymentService, IOrderService orderService, 
            IOrderProcessingService orderProcessingService,
            eWayHostedPaymentSettings eWayHostedPaymentSettings,
            PaymentSettings paymentSettings,
            IPermissionService permissionService)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._eWayHostedPaymentSettings = eWayHostedPaymentSettings;
            this._paymentSettings = paymentSettings;
            this._permissionService = permissionService;
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                CustomerId = _eWayHostedPaymentSettings.CustomerId,
                Username = _eWayHostedPaymentSettings.Username,
                PaymentPage = _eWayHostedPaymentSettings.PaymentPage,
                AdditionalFee = _eWayHostedPaymentSettings.AdditionalFee
            };

            return View("~/Plugins/Payments.eWayHosted/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //save settings
            _eWayHostedPaymentSettings.CustomerId = model.CustomerId;
            _eWayHostedPaymentSettings.Username = model.Username;
            _eWayHostedPaymentSettings.PaymentPage = model.PaymentPage;
            _eWayHostedPaymentSettings.AdditionalFee = model.AdditionalFee;
            _settingService.SaveSetting(_eWayHostedPaymentSettings);

            return Configure();
        }

        public IActionResult MerchantReturn()
        {
            var form = Request.Form;

            var processor =
                _paymentService.LoadPaymentMethodBySystemName("Payments.eWayHosted") as eWayHostedPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("eWayHosted module cannot be loaded");

            var accessPaymentCode = string.Empty;
            if (form.ContainsKey("AccessPaymentCode"))
                accessPaymentCode = form["AccessPaymentCode"];

            //get the result of the transaction based on the unique payment code
            var validationResult = processor.CheckAccessCode(accessPaymentCode);

            if (!string.IsNullOrEmpty(validationResult.ErrorMessage))
            {
                //failed
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            if (string.IsNullOrEmpty(validationResult.TrxnStatus) ||
                !validationResult.TrxnStatus.ToLower().Equals("true"))
            {
                //failed
                return RedirectToAction("Index", "Home", new { area = "" });
            }
            var orderId = Convert.ToInt32(validationResult.MerchnatOption1);
            var order = _orderService.GetOrderById(orderId);
            if (order == null) return RedirectToAction("Index", "Home", new { area = "" });

            if (_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                _orderProcessingService.MarkOrderAsPaid(order);
            }

            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }
    }
}