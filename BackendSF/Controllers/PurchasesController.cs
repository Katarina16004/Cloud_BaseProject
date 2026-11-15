using ClassLibrary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using System.Threading.Tasks;

namespace BackendSF.Controllers
{
    [Route("api/purchases")]
    [ApiController]
    public class PurchasesController : ControllerBase
    {
        private static readonly Uri OrchestratorUri =
            new Uri(
                "fabric:/MiniProjekat/" +
                "PurchaseOrchestrator");

        [HttpPost]
        public async Task<IActionResult> StartPurchase(
            [FromBody] PurchaseStartRequest request)
        {
            if (request == null)
            {
                return BadRequest(
                    "Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(
                request.UserId))
            {
                return BadRequest(
                    "UserId is required.");
            }

            if (request.BookId <= 0)
            {
                return BadRequest(
                    "BookId must be positive.");
            }

            if (request.Quantity <= 0)
            {
                return BadRequest(
                    "Quantity must be greater than zero.");
            }

            if (!Request.Headers.TryGetValue(
                "Idempotency-Key",
                out var idempotencyHeader))
            {
                return BadRequest(
                    "Idempotency-Key header is required.");
            }

            Guid purchaseId;

            if (!Guid.TryParse(
                idempotencyHeader.ToString(),
                out purchaseId))
            {
                return BadRequest(
                    "Idempotency-Key must be a GUID.");
            }

            try
            {
                var proxy =
                    CreateProxy(purchaseId);

                await proxy.StartPurchaseAsync(
                    purchaseId,
                    request.UserId,
                    request.BookId,
                    request.Quantity);

                var location =
                    "/api/purchases/" +
                    purchaseId;

                Response.Headers["Location"] =
                    location;

                return Accepted(
                    location,
                    new
                    {
                        PurchaseId = purchaseId,
                        Status = PurchaseStatus.Started
                    });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    ex.Message);
            }
        }

        [HttpGet("{purchaseId}")]
        public async Task<IActionResult> GetPurchase(
            Guid purchaseId)
        {
            if (purchaseId == Guid.Empty)
            {
                return BadRequest(
                    "Purchase ID is required.");
            }

            try
            {
                var proxy =
                    CreateProxy(purchaseId);

                var purchase =
                    await proxy.GetPurchaseAsync(
                        purchaseId);

                if (purchase == null)
                {
                    return NotFound(
                        "Purchase not found.");
                }

                return Ok(purchase);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    ex.Message);
            }
        }

        [HttpDelete("{purchaseId}")]
        public async Task<IActionResult> CancelPurchase(
            Guid purchaseId)
        {
            if (purchaseId == Guid.Empty)
            {
                return BadRequest(
                    "Purchase ID is required.");
            }

            try
            {
                var proxy =
                    CreateProxy(purchaseId);

                await proxy.CancelPurchaseAsync(
                    purchaseId);

                return Accepted(
                    new
                    {
                        PurchaseId = purchaseId,
                        Status = "CancellationRequested"
                    });
            }
            catch (Exception ex)
            {
                var rootException = UnwrapException(ex);

                if (rootException is InvalidOperationException)
                {
                    return Conflict(rootException.Message);
                }

                return StatusCode(
                    500,
                    rootException.Message);
            }
        }
        private static Exception UnwrapException(
    Exception exception)
        {
            var current = exception;

            while (current is AggregateException aggregate &&
                   aggregate.InnerExceptions.Count > 0)
            {
                current = aggregate.InnerExceptions[0];
            }

            return current;
        }
        private static
            IPurchaseOrchestratorService
            CreateProxy(Guid purchaseId)
        {
            return ServiceProxy.Create<
                IPurchaseOrchestratorService>(
                    OrchestratorUri,
                    new ServicePartitionKey(
                        PartitionKeyHelper
                            .ForPurchase(purchaseId)));
        }
    }

    public class PurchaseStartRequest
    {
        public string UserId { get; set; }

        public int BookId { get; set; }

        public int Quantity { get; set; }
    }
}