# Update with Start - Lazy Init

This sample demonstrates a shopping cart that uses update with start to lazily start a workflow if it doesn't already
exist and then send an update to it.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory in a
separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

The workflow terminal will output something like:

```
Starting to shop...
Subtotal after item 1: 17.97
Subtotal after item 2: <item not found>
Final order: order id: cart-session-777, items: [{ sku: sku-123, quantity: 3, price: 17.97 }], total: 17.97
```

See the code for more details.