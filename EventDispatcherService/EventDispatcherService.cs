using ClassLibrary;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using MimeKit;
using MimeKit.Text;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace EventDispatcherService
{
    internal sealed class EventDispatcherService : StatefulService, IEventDispatcherService
    {
        private const string QueueName = "eventsQueue";

        public EventDispatcherService(StatefulServiceContext context)
            : base(context)
        { }

        private async Task<IReliableQueue<PurchaseEvent>> GetQueue()
        {
            return await StateManager.GetOrAddAsync<IReliableQueue<PurchaseEvent>>(QueueName);
        }

        public async Task PublishAsync(PurchaseEvent evt)
        {
            var queue = await GetQueue();

            using (var tx = StateManager.CreateTransaction())
            {
                await queue.EnqueueAsync(tx, evt);
                await tx.CommitAsync();
            }
        }

        protected override async Task RunAsync(CancellationToken ct)
        {
            var queue = await GetQueue();

            while (!ct.IsCancellationRequested)
            {
                using (var tx = StateManager.CreateTransaction())
                {
                    var result = await queue.TryDequeueAsync(tx, TimeSpan.FromSeconds(1), ct);

                    if (result.HasValue)
                    {
                        try
                        {
                            await ProcessEventAsync(result.Value);
                            await tx.CommitAsync();

                            ServiceEventSource.Current.ServiceMessage(
                                this.Context,
                                "Mejl uspešno poslat na: {0}",
                                result.Value.PurchaseRequest.Account);
                        }
                        catch (Exception ex)
                        {
                            tx.Abort();

                            ServiceEventSource.Current.ServiceMessage(
                                this.Context,
                                "Greška pri slanju mejla: {0}",
                                ex.Message);

                            await Task.Delay(TimeSpan.FromSeconds(15), ct);
                        }
                    }
                }

                await Task.Delay(500, ct);
            }
        }

        private async Task ProcessEventAsync(PurchaseEvent evt)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("BookStore Podrška", "dijana.stojkovic83@gmail.com"));
            message.To.Add(MailboxAddress.Parse(evt.PurchaseRequest.Account));
            message.Subject = "Kupovina uspešno obavljena";

            message.Body = new TextPart(TextFormat.Html)
            {
                Text = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                        <h2 style='color: #333;'>Kupovina uspešno obavljena</h2>
                        <p>Poštovani,</p>
                        <p>Obaveštavamo Vas da je Vaša kupovina uspešno realizovana.</p>
                        <p><b>Detalji kupovine:</b></p>
                        <ul>
                            <li><b>Naziv knjige:</b> {evt.PurchaseRequest.Name}</li>
                            <li><b>Količina:</b> {evt.PurchaseRequest.Quantity}</li>
                            <li><b>Ukupan iznos:</b> {evt.PurchaseRequest.Quantity * evt.PurchaseRequest.Price} RSD</li>
                            <li><b>Datum i vreme:</b> {evt.Timestamp}</li>
                        </ul>
                        <p>Hvala Vam na ukazanom poverenju.</p>
                        <p>Srdačan pozdrav,<br/><b>BookStore tim</b></p>
                    </body>
                    </html>"
            };

            using var client = new MailKit.Net.Smtp.SmtpClient();

            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);

            await client.AuthenticateAsync("dijana.stojkovic83@gmail.com", "pyyyaccpzzqqpvdw");
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }
    }
}