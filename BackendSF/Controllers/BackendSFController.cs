using ClassLibrary;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace BackendSF.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BackendSFController : ControllerBase
    {
        private readonly IValidatorService _validatorService;

        public BackendSFController()
        {
            _validatorService = ServiceProxy.Create<IValidatorService>(
                new Uri("fabric:/MiniProjekat/ValidatorService"));
        }

        [HttpPost("purchase")]
        public async Task<ValidationResultDto> ValidateRequest([FromBody] PurchaseRequestDto request)
        {
            var result = await _validatorService.ValidatePurchaseAsync(request);
            return result;
        }
        [HttpGet("books")]
        public async Task<IActionResult> GetBooks()
        {
            try
            {
                var libraryProxy = ServiceProxy.Create<ILibraryService>(
                    new Uri("fabric:/MiniProjekat/LibraryService"),
                    new ServicePartitionKey(0));

                var books = await libraryProxy.GetAllBooksAsync();
                return Ok(books);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts()
        {
            try
            {
                var bankProxy = ServiceProxy.Create<IBankService>(
                    new Uri("fabric:/MiniProjekat/BankService"),
                    new ServicePartitionKey(0));

                var customers = await bankProxy.GetAllCustomersAsync();
                return Ok(customers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
