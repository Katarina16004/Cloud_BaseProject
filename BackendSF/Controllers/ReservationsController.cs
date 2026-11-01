using System;
using System.Threading.Tasks;
using ClassLibrary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace BackendSF.Controllers
{
    [Route("api")]
    [ApiController]
    public class ReservationsController :
        ControllerBase
    {
        [HttpPost("books/{bookId}/reservations")]
        public async Task<IActionResult> ReserveBook(
            int bookId,
            [FromBody] ReservationRequest request)
        {
            if (request == null ||
                request.Quantity <= 0)
            {
                return BadRequest(
                    "Quantity mora biti veći od nule.");
            }

            try
            {
                var serviceUri =
                    new Uri(
                        "fabric:/MiniProjekat/LibraryService");

                var proxy =
                    ServiceProxy.Create<ILibraryService>(
                        serviceUri,
                        new ServicePartitionKey(
                            PartitionKeys.ForBook(bookId)));

                var books =
                    await proxy.GetAllBooksAsync();

                var book = books.Find(
                    item => item.BookId == bookId);

                if (book == null)
                {
                    return NotFound(
                        "Knjiga nije pronađena.");
                }

                var txId = Guid.NewGuid();

                var price = await proxy.ReserveAsync(
                    txId,
                    bookId,
                    book.Name,
                    request.Quantity);

                return Ok(new
                {
                    TxId = txId,
                    BookId = bookId,
                    Quantity = request.Quantity,
                    Price = price,
                    Status = ReservationStatus.Reserved
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("books/{bookId}/consistency")]
        public async Task<IActionResult> GetConsistency(
            int bookId)
        {
            try
            {
                var serviceUri =
                    new Uri(
                        "fabric:/MiniProjekat/LibraryService");

                var proxy =
                    ServiceProxy.Create<ILibraryService>(
                        serviceUri,
                        new ServicePartitionKey(
                            PartitionKeys.ForBook(bookId)));

                var result =
                    await proxy.GetConsistencyAsync(bookId);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }

    public class ReservationRequest
    {
        public int Quantity { get; set; }
    }
}