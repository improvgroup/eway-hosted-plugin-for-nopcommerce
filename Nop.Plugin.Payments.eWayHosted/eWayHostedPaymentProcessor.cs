using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Routing;
using System.Xml;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.eWayHosted.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.eWayHosted
{
    /// <summary>
    /// eWayHosted payment processor
    /// </summary>
    public class eWayHostedPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly eWayHostedPaymentSettings _eWayHostedPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public eWayHostedPaymentProcessor(eWayHostedPaymentSettings eWayHostedPaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, IWebHelper webHelper)
        {
            this._eWayHostedPaymentSettings = eWayHostedPaymentSettings;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
            this._webHelper = webHelper;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Format the string needed to post to the Generate page
        /// </summary>
        /// <param name="fieldName">eWAY Parameter Name</param>
        /// <param name="value">Value of Parameter</param>
        /// <returns>Formated value for the URL</returns>
        private string Format(string fieldName, string value)
        {
            if (!string.IsNullOrEmpty(value))
                return "&" + fieldName + "=" + value;
            else
                return "";
        }
        
        /// <summary>
        /// Parse the result of the transaction request and save the appropriate fields in an object to be used later
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        private TransactionRequestResult ParseRequestResults(string xml)
        {
            string _currentNode;
            var _sr = new StringReader(xml);
            var _xtr = new XmlTextReader(_sr);
            _xtr.XmlResolver = null;
            _xtr.WhitespaceHandling = WhitespaceHandling.None;

            // get the root node
            _xtr.Read();

            var res = new TransactionRequestResult();

            if ((_xtr.NodeType == XmlNodeType.Element) && (_xtr.Name == "TransactionRequest"))
            {
                while (_xtr.Read())
                {
                    if ((_xtr.NodeType == XmlNodeType.Element) && (!_xtr.IsEmptyElement))
                    {
                        _currentNode = _xtr.Name;
                        _xtr.Read();
                        if (_xtr.NodeType == XmlNodeType.Text)
                        {
                            switch (_currentNode)
                            {
                                case "Result":
                                    res.Result = bool.Parse(_xtr.Value);
                                    break;

                                case "URI":
                                    res.Uri = _xtr.Value;
                                    break;

                                case "Error":
                                    res.Error = _xtr.Value;
                                    break;
                            }
                        }
                    }
                }
            }

            return res;
        }

        /// <summary>
        /// Parse the XML Returned and save all the values to be displayed to user
        /// </summary>
        /// <param name="resultXml">XML of the transaction result</param>
        private ValdiationRequestResult ParseXmlResult(string resultXml)
        {
            var result = new ValdiationRequestResult();
            string _currentNode;
            var _sr = new StringReader(resultXml);
            var _xtr = new XmlTextReader(_sr);
            _xtr.XmlResolver = null;
            _xtr.WhitespaceHandling = WhitespaceHandling.None;

            // get the root node
            _xtr.Read();

            if ((_xtr.NodeType == XmlNodeType.Element) && (_xtr.Name == "TransactionResponse"))
            {
                while (_xtr.Read())
                {
                    if ((_xtr.NodeType == XmlNodeType.Element) && (!_xtr.IsEmptyElement))
                    {
                        _currentNode = _xtr.Name;
                        _xtr.Read();
                        if (_xtr.NodeType == XmlNodeType.Text)
                        {
                            switch (_currentNode)
                            {

                                case "AuthCode":
                                    result.AuthCode = _xtr.Value;
                                    break;
                                case "ResponseCode":
                                    result.ResponseCode = _xtr.Value;
                                    break;
                                case "ReturnAmount":
                                    result.ReturnAmount = _xtr.Value;
                                    break;
                                case "TrxnStatus":
                                    result.TrxnStatus = _xtr.Value;
                                    break;
                                case "TrxnNumber":
                                    result.TrxnNumber = _xtr.Value;
                                    break;
                                case "MerchantOption1":
                                    result.MerchnatOption1 = _xtr.Value;
                                    break;
                                case "MerchantOption2":
                                    result.MerchnatOption2 = _xtr.Value;
                                    break;
                                case "MerchantOption3":
                                    result.MerchnatOption3 = _xtr.Value;
                                    break;
                                case "MerchantInvoice":
                                    result.ReferenceInvoice = _xtr.Value;
                                    break;
                                case "MerchantReference":
                                    result.ReferenceNumber = _xtr.Value;
                                    break;
                                case "TrxnResponseMessage":
                                    result.TrxnResponseMessage = _xtr.Value;
                                    break;
                                case "ErrorMessage":
                                    result.ErrorMessage = _xtr.Value;
                                    break;

                            }
                        }
                    }
                }
            }
            else if ((_xtr.NodeType == XmlNodeType.Element) && (_xtr.Name == "TransactionRequest"))
            {
                while (_xtr.Read())
                {
                    if ((_xtr.NodeType == XmlNodeType.Element) && (!_xtr.IsEmptyElement))
                    {
                        _currentNode = _xtr.Name;
                        _xtr.Read();
                        if (_xtr.NodeType == XmlNodeType.Text)
                        {
                            switch (_currentNode)
                            {
                                case "Error":
                                    result.ErrorMessage = _xtr.Value;
                                    break;

                            }
                        }
                    }
                }
            }
            return result;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            string strPost = "CustomerID=" + _eWayHostedPaymentSettings.CustomerId;
            strPost += Format("UserName", _eWayHostedPaymentSettings.Username);
            //send amounts to the generator in DOLLAR FORM. ie 10.05
            strPost += Format("Amount", postProcessPaymentRequest.Order.OrderTotal.ToString("0.00", CultureInfo.InvariantCulture));
            strPost += Format("Currency", _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode);


            // supported languages: 
            // "EN" - English
            // "FR" - French
            // "DE" - German
            // "ES" - Spanish
            // "NL" - Dutch
            strPost += Format("Language", "EN");
            strPost += Format("CustomerFirstName", postProcessPaymentRequest.Order.BillingAddress.FirstName);
            strPost += Format("CustomerLastName", postProcessPaymentRequest.Order.BillingAddress.LastName);
            strPost += Format("CustomerAddress", postProcessPaymentRequest.Order.BillingAddress.Address1);
            strPost += Format("CustomerCity", postProcessPaymentRequest.Order.BillingAddress.City);
            strPost += Format("CustomerState", postProcessPaymentRequest.Order.BillingAddress.StateProvince != null ? postProcessPaymentRequest.Order.BillingAddress.StateProvince.Name : "");
            strPost += Format("CustomerPostCode", postProcessPaymentRequest.Order.BillingAddress.ZipPostalCode);
            strPost += Format("CustomerCountry", postProcessPaymentRequest.Order.BillingAddress.Country != null ?postProcessPaymentRequest.Order.BillingAddress.Country.Name : "");
            strPost += Format("CustomerEmail", postProcessPaymentRequest.Order.BillingAddress.Email);
            strPost += Format("CustomerPhone", postProcessPaymentRequest.Order.BillingAddress.PhoneNumber);
            strPost += Format("InvoiceDescription", postProcessPaymentRequest.Order.Id.ToString());
            strPost += Format("CancelURL", _webHelper.GetStoreLocation(false) + "Plugins/PaymenteWayHosted/MerchantReturn");
            strPost += Format("ReturnUrl", _webHelper.GetStoreLocation(false) + "Plugins/PaymenteWayHosted/MerchantReturn");

            strPost += Format("MerchantReference", postProcessPaymentRequest.Order.Id.ToString());
            strPost += Format("MerchantInvoice", postProcessPaymentRequest.Order.Id.ToString());
            strPost += Format("MerchantOption1", postProcessPaymentRequest.Order.Id.ToString());

            string url = _eWayHostedPaymentSettings.PaymentPage + "Request?" + strPost;

            var objRequest = (HttpWebRequest)WebRequest.Create(url);
            objRequest.Method = WebRequestMethods.Http.Get;

            var objResponse = (HttpWebResponse)objRequest.GetResponse();

            //get the response from the transaction generate page
            string resultXml = "";
            using (var sr = new StreamReader(objResponse.GetResponseStream()))
            {
                resultXml = sr.ReadToEnd();
                // Close and clean up the StreamReader
                sr.Close();
            }

            //parse the result message
            var resultObj = ParseRequestResults(resultXml);

            if (resultObj.Result)
            {
                //redirect the user to the payment page
                HttpContext.Current.Response.Redirect(resultObj.Uri);
            }
            else
            {
                throw new NopException(resultObj.Error);
            }
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return _eWayHostedPaymentSettings.AdditionalFee;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //eWayHosted is the redirection payment method
            //It also validates whether order is also paid (after redirection) so customers will not be able to pay twice
            
            //payment status should be Pending
            if (order.PaymentStatus != PaymentStatus.Pending)
                return false;

            //let's ensure that at least 1 minute passed after order is placed
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymenteWayHosted";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.eWayHosted.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymenteWayHosted";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.eWayHosted.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymenteWayHostedController);
        }

        public override void Install()
        {
            var settings = new eWayHostedPaymentSettings()
            {
                CustomerId = "87654321",
                Username = "TestAccount",
                PaymentPage = "https://payment.ewaygateway.com/",
                AdditionalFee= 0,
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWayHosted.RedirectionTip", "You will be redirected to eWay site to complete the order.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWayHosted.CustomerId", "Customer ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWayHosted.CustomerId.Hint", "Enter customer ID.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWayHosted.Username", "Username");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWayHosted.Username.Hint", "Enter username.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWayHosted.PaymentPage", "Payment page");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWayHosted.PaymentPage.Hint", "Enter payment page.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWayHosted.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWayHosted.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            
            base.Install();
        }


        public override void Uninstall()
        {
            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.eWayHosted.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.eWayHosted.CustomerId");
            this.DeletePluginLocaleResource("Plugins.Payments.eWayHosted.CustomerId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.eWayHosted.Username");
            this.DeletePluginLocaleResource("Plugins.Payments.eWayHosted.Username.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.eWayHosted.PaymentPage");
            this.DeletePluginLocaleResource("Plugins.Payments.eWayHosted.PaymentPage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.eWayHosted.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.eWayHosted.AdditionalFee.Hint");
            
            base.Uninstall();
        }
        /// <summary>
        /// Procedure to check the 64 character access payment code
        /// for security
        /// </summary>
        /// <param name="accessPaymentCode">64 char code</param>
        /// <returns>true if found; false if not found</returns>
        public ValdiationRequestResult CheckAccessCode(string accessPaymentCode)
        {
            //POST to Payment gateway the access code returned
            string strPost = "CustomerID=" + _eWayHostedPaymentSettings.CustomerId;
            strPost += Format("AccessPaymentCode", accessPaymentCode);
            strPost += Format("UserName", _eWayHostedPaymentSettings.Username);

            string url = _eWayHostedPaymentSettings.PaymentPage + "Result?" + strPost;

            var objRequest = (HttpWebRequest)WebRequest.Create(url);
            objRequest.Method = WebRequestMethods.Http.Get;
            string resultXml = "";

            try
            {
                var objResponse = (HttpWebResponse)objRequest.GetResponse();

                //get the response from the transaction generate page
                using (var sr = new StreamReader(objResponse.GetResponseStream()))
                {
                    resultXml = sr.ReadToEnd();
                    // Close and clean up the StreamReader
                    sr.Close();
                }
            }
            catch (Exception exc)
            {
                return new ValdiationRequestResult()
                {
                    ErrorMessage = exc.Message
                };
            }

            //parse the results save the values
            return ParseXmlResult(resultXml);
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        #endregion
    }
}
