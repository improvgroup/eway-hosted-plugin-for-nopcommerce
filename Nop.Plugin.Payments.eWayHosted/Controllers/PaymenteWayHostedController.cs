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
        private readonly eWayHostedPaymentSettings _eWayHostedPaymentSettings;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;

        public PaymenteWayHostedController(
            eWayHostedPaymentSettings eWayHostedPaymentSettings,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentService paymentService,
            IPermissionService permissionService,
            ISettingService settingService,
            PaymentSettings paymentSettings)
        {
            this._eWayHostedPaymentSettings = eWayHostedPaymentSettings;
            this._orderProcessingService = orderProcessingService;
            this._orderService = orderService;
            this._paymentService = paymentService;
            this._permissionService = permissionService;
            this._settingService = settingService;
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

        public IActionResult MerchantReturn(IpnModel model)
        {
            var form = model.Form;

            var processor =
                _paymentService.LoadPaymentMethodBySystemName("Payments.eWayHosted") as eWayHostedPaymentProcessor;
            if (processor == null ||
                !_paymentService.IsPaymentMethodActive(processor) || !processor.PluginDescriptor.Installed)
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