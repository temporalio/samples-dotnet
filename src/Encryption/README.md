# Encryption

This sample shows a custom payload codec that does end-to-end encryption on workflow payloads. It is built to be
compatible with encryption samples from other SDKs.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run --project Worker

Then in another terminal, run the workflow from this directory:

    dotnet run --project Starter

This will show the completed workflow result.

Now, with [Temporal CLI](https://github.com/temporalio/cli) on the `PATH`, show the workflow:

    temporal workflow show --workflow-id encryption-workflow-id

Notice at the bottom there is:

```
  Output: [encoding binary/encrypted: payload encoding is not supported]
```

This is because the contents are encrypted. We can provide the CLI and UI a remote codec server endpoint to decrypt the
payloads when viewing. In another terminal, from this directory run:

    dotnet run --project CodecServer

This starts an ASP.NET web server that can respond to remote codec attempts from the CLI and UI. By default this is
usually at `http://localhost:5000`. So now, with that running, run the CLI command to show with the endpoint:

    temporal workflow show --workflow-id encryption-workflow-id --codec-endpoint http://localhost:5000

Now the output is there:

```
  Output: ["Hello, Temporal!"]
```

Same situation on the UI. Navigating to the UI at http://localhost:8080 and viewing the workflow does not decrypt
output. But setting the "Remote Codec Endpoint" to `http://localhost:5000` will allow the browser to directly
communicate with that codec server to decrypt.