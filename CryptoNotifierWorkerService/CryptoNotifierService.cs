using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using RestSharp;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoNotifierWorkerService
{
    public class CryptoNotifierService : BackgroundService
    {


        private double ReturnCurrentIdexPrice()
        {
            var idexBaseUrl = ConfigurationManager.AppSettings.Get("idexBaseUrl");
            var idexApi = ConfigurationManager.AppSettings.Get("idexApi");
            var idexTicker = ConfigurationManager.AppSettings.Get("idexTicker");

            var client = new RestClient(idexBaseUrl);

            var request = new RestRequest(idexApi);

            var queryResult = client.Execute(request);

            dynamic apiResult = JObject.Parse(queryResult.Content);

            return apiResult[idexTicker].last;
        }

        private double IdexReturnedPriceToPesoValue(double idexPrice)
        {
            var cryptoAmount = ConfigurationManager.AppSettings.Get("cryptoAmount");
            var ethToUsdBaseUrl = ConfigurationManager.AppSettings.Get("ethToUsdBaseUrl");
            var ethToUsdApi = ConfigurationManager.AppSettings.Get("ethToUsdApi");

            var client = new RestClient(ethToUsdBaseUrl);

            var request = new RestRequest(ethToUsdApi);

            var queryResult = client.Execute(request);

            dynamic apiResult = JObject.Parse(queryResult.Content);

            var usdToPeso = CalculateUsdToPesoValue();

            return apiResult.USD * idexPrice * usdToPeso * double.Parse(cryptoAmount);
        }

        private double CalculateUsdToPesoValue()
        {
            var currencyBaseUrl = ConfigurationManager.AppSettings.Get("currencyBaseUrl");

            var currencyApi = ConfigurationManager.AppSettings.Get("currencyApi");

            var client = new RestClient(currencyBaseUrl);

            var request = new RestRequest(currencyApi);

            var queryResult = client.Execute(request);

            dynamic apiResult = JObject.Parse(queryResult.Content);

            return apiResult.rates.PHP;
        }

        private async Task SendEmail(double pesoValue)
        {
            var idexTicker = ConfigurationManager.AppSettings.Get("idexTicker");
            var apiKey = ConfigurationManager.AppSettings.Get("sendGridKey");
            var emailAddress = ConfigurationManager.AppSettings.Get("emailAddress");

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("cryptonotifier@gmail.com", "Crypto Notifier");
            var subject = "Crypto Notifier For - " + idexTicker;
            var to = new EmailAddress(emailAddress, "Crypto User");
            var plainTextContent = GeneratePlainMessage(pesoValue);
            var htmlContent = GenerateHtmlEmail(pesoValue);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            _ = await client.SendEmailAsync(msg);
        }

        private string GeneratePlainMessage(double pesoValue)
        {
            var idexTicker = ConfigurationManager.AppSettings.Get("idexTicker");

            StringBuilder sb = new StringBuilder();

            sb
            .Append("The current peso price of ")
            .Append(idexTicker)
            .Append(" is ")
            .AppendFormat("{0:0,0.0}", pesoValue);

            return sb.ToString();
        }

        private string GenerateHtmlEmail(double pesoValue)
        {
            var idexTicker = ConfigurationManager.AppSettings.Get("idexTicker");

            StringBuilder sb = new StringBuilder();

            sb
            .Append("<strong>The current peso price of ")
            .Append(idexTicker)
            .Append(" is ")
            .AppendFormat("{0:0,0.0}", pesoValue)
            .Append("</strong>");

            return sb.ToString();
        }

        private void SendSMS(double pesoValue)
        {
            var nexmoBaseUrl = ConfigurationManager.AppSettings.Get("nexmoBaseUrl");
            var nexmoApi = ConfigurationManager.AppSettings.Get("nexmoApi");

            var mobileNumber = ConfigurationManager.AppSettings.Get("mobileNumber");
            var nexmoKey = ConfigurationManager.AppSettings.Get("nexmoKey");
            var nexmoSecret = ConfigurationManager.AppSettings.Get("nexmoSecret");

            var textMessage = GeneratePlainMessage(pesoValue);

            var nexmoObj = new Nexmo("CryptoNotifier", textMessage, mobileNumber, nexmoKey, nexmoSecret);

            var client = new RestClient(nexmoBaseUrl);

            var request = new RestRequest(nexmoApi, Method.POST)
            {
                RequestFormat = DataFormat.Json
            };

            request.AddJsonBody(nexmoObj);
            _ = client.Execute(request);
        }

  

   
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var dayToSendSms = ConfigurationManager.AppSettings.Get("dayToSendSms");
                var timeToSendSms = ConfigurationManager.AppSettings.Get("timeToSendSms");
                var timeToEmail = ConfigurationManager.AppSettings.Get("timeToEmail");

                //check if today is the sending of the sms, then check the time to be sent
                if (DateTime.Now.ToString("dddd").Equals(dayToSendSms) && DateTime.Now.ToString("HH:mm:ss").Equals(timeToSendSms))
                {
                    SendSMS(GetPesoValueOfEnrolledCrypto());
                }

                if (DateTime.Now.ToString("HH:mm:ss").Equals(timeToEmail)) //sends daily email
                {
                    _ = SendEmail(GetPesoValueOfEnrolledCrypto());
                }

                await Task.Delay(100, stoppingToken);
            }
        }

        private double GetPesoValueOfEnrolledCrypto()
        {
            var idexCurrentPrice = ReturnCurrentIdexPrice();
            return IdexReturnedPriceToPesoValue(idexCurrentPrice);
        }
    }
}
