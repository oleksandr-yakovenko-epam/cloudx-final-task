using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.eShopWeb.Web.Configuration;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.Web.Pages.Basket
{
    [Authorize]
    public class CheckoutModel : PageModel
    {
        private readonly IBasketService _basketService;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IOrderService _orderService;
        private string _username = null;
        private readonly IBasketViewModelService _basketViewModelService;
        private readonly IAppLogger<CheckoutModel> _logger;
        private readonly CosmosDbSettings _cosmosDbSettings;
        private readonly ServiceBusSettings _serviceBusSettings;

        public CheckoutModel(IBasketService basketService,
            IBasketViewModelService basketViewModelService,
            SignInManager<ApplicationUser> signInManager,
            IOrderService orderService,
            IAppLogger<CheckoutModel> logger,
            CosmosDbSettings cosmosDbSettings,
            ServiceBusSettings serviceBusSettings)
        {
            _basketService = basketService;
            _signInManager = signInManager;
            _orderService = orderService;
            _basketViewModelService = basketViewModelService;
            _logger = logger;
            _cosmosDbSettings = cosmosDbSettings;
            _serviceBusSettings = serviceBusSettings;

            /*_cosmosDbSettings = new CosmosDbSettings
            {
                Endpoint = "https://alexy-eshop-cosmo.documents.azure.com:443/",
                Key = "jF6wYOlBSaq9Cqf8muTop6Al3pmAU4M3oEbVuBUcrKQsPVCQjjt3ddDXaeZeZPrILCKgIumHnr6jFvis18ML4Q=="
            };

            _serviceBusSettings = new ServiceBusSettings
            {
                ConnectionString = "Endpoint=sb://alexyeshopsb.servicebus.windows.net/;SharedAccessKeyName=RootSharedKey;SharedAccessKey=3HfDeF2RgnePo1z9S4DrMsQl5whHctxmkjCwNKXEFT8="
            };*/

        }

        public BasketViewModel BasketModel { get; set; } = new BasketViewModel();

        public async Task OnGet()
        {
            await SetBasketModelAsync();
        }

        public async Task<IActionResult> OnPost(IEnumerable<BasketItemViewModel> items)
        {
            try
            {
                await SetBasketModelAsync();

                if (!ModelState.IsValid)
                {
                    return BadRequest();
                }

                var updateModel = items.ToDictionary(b => b.Id.ToString(), b => b.Quantity);
                await _basketService.SetQuantities(BasketModel.Id, updateModel);
                await _orderService.CreateOrderAsync(BasketModel.Id, new Address("123 Main St.", "Kent", "OH", "United States", "44240"));


                using var cosmosClient = new CosmosClient(_cosmosDbSettings.Endpoint, _cosmosDbSettings.Key);
                var container = cosmosClient.GetContainer("delivery", "delivery-orders");
                await container.CreateItemAsync(new { id = Guid.NewGuid(), order = BasketModel}, PartitionKey.None);

                var json = JsonConvert.SerializeObject(BasketModel);
                var queue = new QueueClient(_serviceBusSettings.ConnectionString, "order-irem-reserve");

                await queue.SendAsync(new Message(Encoding.UTF8.GetBytes(json)));

                await _basketService.DeleteBasketAsync(BasketModel.Id);               
            }
            catch (EmptyBasketOnCheckoutException emptyBasketOnCheckoutException)
            {
                //Redirect to Empty Basket page
                _logger.LogWarning(emptyBasketOnCheckoutException.Message);
                return RedirectToPage("/Basket/Index");
            }

            return RedirectToPage("Success");
        }

        private async Task SetBasketModelAsync()
        {
            if (_signInManager.IsSignedIn(HttpContext.User))
            {
                BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(User.Identity.Name);
            }
            else
            {
                GetOrSetBasketCookieAndUserName();
                BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(_username);
            }
        }

        private void GetOrSetBasketCookieAndUserName()
        {
            if (Request.Cookies.ContainsKey(Constants.BASKET_COOKIENAME))
            {
                _username = Request.Cookies[Constants.BASKET_COOKIENAME];
            }
            if (_username != null) return;

            _username = Guid.NewGuid().ToString();
            var cookieOptions = new CookieOptions();
            cookieOptions.Expires = DateTime.Today.AddYears(10);
            Response.Cookies.Append(Constants.BASKET_COOKIENAME, _username, cookieOptions);
        }
    }
}
