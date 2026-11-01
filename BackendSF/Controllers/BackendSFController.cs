using ClassLibrary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;

namespace BackendSF.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BackendSFController : ControllerBase
    {
        private readonly IValidatorService _validatorService;

        public BackendSFController()
        {
            _validatorService =
                ServiceProxy.Create<IValidatorService>(
                    new Uri(
                        "fabric:/MiniProjekat/ValidatorService"));
        }

        [HttpPost("purchase")]
        public async Task<ValidationResultDto> ValidateRequest(
            [FromBody] PurchaseRequestDto request)
        {
            return await _validatorService.ValidatePurchaseAsync(request);
        }

        [HttpGet("books")]
        public async Task<IActionResult> GetBooks()
        {
            try
            {
                var serviceUri =
                    new Uri("fabric:/MiniProjekat/LibraryService");

                using (var fabricClient = new FabricClient())
                {
                    var partitions = await fabricClient.QueryManager
                        .GetPartitionListAsync(serviceUri);

                    var calls = partitions.Select(partition =>
                    {
                        var information =
                            (Int64RangePartitionInformation)
                            partition.PartitionInformation;

                        var proxy = ServiceProxy.Create<ILibraryService>(
                            serviceUri,
                            new ServicePartitionKey(information.LowKey));

                        return proxy.GetAllBooksAsync();
                    });

                    var results = await Task.WhenAll(calls);

                    var books = results
                        .SelectMany(result => result)
                        .OrderBy(book => book.Name)
                        .ToList();

                    return Ok(books);
                }
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
                var serviceUri =
                    new Uri("fabric:/MiniProjekat/BankService");

                using (var fabricClient = new FabricClient())
                {
                    var partitions = await fabricClient.QueryManager
                        .GetPartitionListAsync(serviceUri);

                    var calls = partitions.Select(partition =>
                    {
                        var information =
                            (Int64RangePartitionInformation)
                            partition.PartitionInformation;

                        var proxy = ServiceProxy.Create<IBankService>(
                            serviceUri,
                            new ServicePartitionKey(information.LowKey));

                        return proxy.GetAllCustomersAsync();
                    });

                    var results = await Task.WhenAll(calls);

                    var customers = results
                        .SelectMany(result => result)
                        .OrderBy(customer => customer.Account)
                        .ToList();

                    return Ok(customers);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("books")]
        public async Task<IActionResult> AddBook(
            [FromBody] SeedBookRequest request)
        {
            try
            {
                int bookId;

                if (!int.TryParse(request.BookId, out bookId) ||
                    bookId <= 0)
                {
                    return BadRequest(
                        "BookId mora biti pozitivan broj.");
                }

                var libraryProxy =
                    ServiceProxy.Create<ILibraryService>(
                        new Uri(
                            "fabric:/MiniProjekat/LibraryService"),
                        new ServicePartitionKey(
                            PartitionKeys.ForBook(bookId)));

                var book = new Book(
                    bookId,
                    "Nepoznat autor",
                    request.Title,
                    request.Price,
                    request.Stock);

                await libraryProxy.AddBookAsync(book);

                return Ok(book);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> AddAccount(
            [FromBody] SeedAccountRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.UserId))
                {
                    return BadRequest(
                        "UserId je obavezan.");
                }

                var bankProxy =
                    ServiceProxy.Create<IBankService>(
                        new Uri(
                            "fabric:/MiniProjekat/BankService"),
                        new ServicePartitionKey(
                            PartitionKeys.ForAccount(
                                request.UserId)));

                int customerId = 1000;

                var customer = new Customer(
                    customerId,
                    request.UserId,
                    request.Balance);

                await bankProxy.AddCustomerAsync(customer);

                return Ok(customer);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        public class SeedBookRequest
        {
            public string BookId { get; set; }
            public string Title { get; set; }
            public double Price { get; set; }
            public int Stock { get; set; }
        }

        public class SeedAccountRequest
        {
            public string UserId { get; set; }
            public string Name { get; set; }
            public double Balance { get; set; }
        }
    }
}