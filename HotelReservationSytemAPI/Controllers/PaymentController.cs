using HotelReservationSytemAPI.DTOs;
using HotelReservationSytemAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace HotelReservationSytemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly StripeSettings _stripeSettings;

        public PaymentController(IOptions<StripeSettings> stripeSettings)
        {
            _stripeSettings = stripeSettings.Value;
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
        }

        // 🔹 1. Create payment intent
        [HttpPost("create-payment-intent")]
        public ActionResult CreatePaymentIntent([FromBody] CreatePaymentIntentDto dto)
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(dto.Amount * 100), // Stripe uses cents
                Currency = dto.Currency,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
                Metadata = new Dictionary<string, string>
                {
                    { "UserId", dto.UserId.ToString() }
                }
            };

            var service = new PaymentIntentService();
            var intent = service.Create(options);

            return Ok(new { clientSecret = intent.ClientSecret,id=intent.Id });
        }

        // 🔹 2. Handle webhook events from Stripe
        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    "whsec_YOUR_WEBHOOK_SECRET" // 🔸 replace this with your actual webhook secret from Stripe Dashboard
                );

                if (stripeEvent.Type == "payment_intent.succeeded")
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    Console.WriteLine($"✅ Payment succeeded: {paymentIntent.Id}");
                    // TODO: mark booking as paid in DB
                }
                else if (stripeEvent.Type == "payment_intent.payment_failed")
                {
                    Console.WriteLine($" Payment failed: {stripeEvent.Id}");
                }

                return Ok();
            }
            catch (StripeException e)
            {
                return BadRequest(e.Message);
            }
        }
    }

}

