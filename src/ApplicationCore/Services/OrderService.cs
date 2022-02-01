using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;

    public class OrderedItem
    {
        public string CatalogItemId { get; set; }

        public string ProductName { get; set; }

        public string PictureUri { get; set; }

        public string UnitPrice { get; set; }

        public string Units { get; set; }

        public string Id { get; set; }
    }

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await runDeliveryOrderAsync(order);
        await runOrderItemsReservationAsync(order);

        await _orderRepository.AddAsync(order);
    }

    private async Task runDeliveryOrderAsync(Order order)
    {
        var jsonOrder = processOrder(order);

        HttpClient _client = new HttpClient();
        var url = "https://kacperfunctionapp.azurewebsites.net/api/DeliveryOrderProcessor?";
        HttpResponseMessage response = await _client.PostAsync(url, new StringContent(jsonOrder, Encoding.UTF8, "application/json"));
    }

    private string processOrder(Order order)
    {
        var ListOfItems = new List<OrderedItem>();
        foreach (OrderItem item in order.OrderItems)
        {
            var newItem = new OrderedItem();
            newItem.CatalogItemId = item.ItemOrdered.CatalogItemId.ToString();
            newItem.ProductName = item.ItemOrdered.ProductName.ToString();
            newItem.PictureUri = item.ItemOrdered.PictureUri.ToString();
            newItem.UnitPrice = item.UnitPrice.ToString();
            newItem.Units = item.Units.ToString();
            newItem.Id = item.Id.ToString();
            ListOfItems.Add(newItem);
        }

        return JsonConvert.SerializeObject(new
        {
            id = Guid.NewGuid().ToString("N"),
            ShippingAddress = order.ShipToAddress,
            ListOfItems = ListOfItems,
            FinalPrice = order.Total()
        });
    }

    private async Task runOrderItemsReservationAsync(Order order)
    {
        string ServiceBusConnectionString = "Endpoint=sb://kacperservicebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=gphrefEDc+N6GNaNsb5wNY3Tpk/ykCiOT7gImKvRkyg=";
        string QueueName = "kacperqueue";

        await using var client = new ServiceBusClient(ServiceBusConnectionString);
        await using ServiceBusSender sender = client.CreateSender(QueueName);

        try
        {
            var jsonOrder = processOrder(order);
            var message = new ServiceBusMessage(jsonOrder);
            await sender.SendMessageAsync(message);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
        }
        finally
        {
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }

    }
}
