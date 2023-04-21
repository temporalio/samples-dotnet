# Client mTLS

This sample shows how to connect to an endpoint with mTLS, such as Temporal Cloud.

### Prerequisites

To run, first see [README.md](../../README.md) for prerequisites.

### Generate Client Certificate

Now we need the client certificate for authentication. The certificate authority (i.e. CA) that signs the certificate
must be given to Temporal Cloud or your server to verify the client certificate. If you already have a certificate, you
can skip to the next section. 

To generate a certificate, from this directory run:

    dotnet run gen-client-certs

This will create the CA cert and key at `client-ca-cert.pem` and `client-ca-key.pem`, and create the client cert and key
at `client-cert.pem` and `client-key.pem`. `client-ca-cert.pem` is what has to be provided to Temporal cloud or your
server to verify authentication. To generate another cert from the existing CA, `--ca-cert-file` and `--ca-key-file` can
be passed when running.

⚠️WARNING: This simple development generator creates a simple self-signed CA certificate with 10 year expiration and a
simple client certificate from that with 5 year expiration. The certificates use ECDSA algorithm with P-256 curve and
have basic subject strings. Production setups may want more control over certificate generation.

### Run Workflow

First, we have to run a worker. In a separate terminal, run the worker from this directory:

    dotnet run run-worker --target-host my-host:7233 --namespace my-ns --client-cert client-cert.pem --client-key client-key.pem

This will start a worker. To run against Temporal Cloud, `--target-host` may be something like
`my-namespace.a1b2c.tmprl.cloud:7233` and `--namespace` may be something like `my-namespace.a1b2c`.

With that running, in a separate terminal execute the workflow from this directory:

    dotnet run execute-workflow --target-host my-host:7233 --namespace my-ns --client-cert client-cert.pem --client-key client-key.pem

This will output:

> Workflow result: Hello, Temporal!