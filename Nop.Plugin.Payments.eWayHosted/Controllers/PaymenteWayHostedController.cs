﻿using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.eWayHosted.Models;
using Nop.Services.Configuration;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;

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

        public PaymenteWayHostedController(ISettingService settingService, 
            IPaymentService paymentService, IOrderService orderService, 
            IOrderProcessingService orderProcessingService,
            eWayHostedPaymentSettings eWayHostedPaymentSettings,
            PaymentSettings paymentSettings)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._eWayHostedPaymentSettings = eWayHostedPaymentSettings;
            this._paymentSettings = paymentSettings;
        }
        
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
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
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
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

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.eWayHosted/Views/PaymentInfo.cshtml");
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        [ValidateInput(false)]
        public ActionResult MerchantReturn(FormCollection form)
        {
            var processor =
                _paymentService.LoadPaymentMethodBySystemName("Payments.eWayHosted") as eWayHostedPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("eWayHosted module cannot be loaded");

            var accessPaymentCode = string.Empty;
            if (form["AccessPaymentCode"] != null)
                accessPaymentCode = Request.Form["AccessPaymentCode"];

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