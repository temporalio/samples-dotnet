namespace TemporalioSamples.ClientMtls;

using System.CommandLine;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public static class GenClientCerts
{
    private static readonly Oid ServerAuthOid = new Oid("1.3.6.1.5.5.7.3.1");
    private static readonly Oid ClientAuthOid = new Oid("1.3.6.1.5.5.7.3.2");

    public static Command CreateCommand()
    {
        var cmd = new Command("gen-client-certs", "Generate certificates");
        var filePrefixOption = new Option<string>(
            name: "--file-prefix",
            getDefaultValue: () => string.Empty,
            description: "Path and file prefix that generated cert filenames will start with");
        var caCertFileOption = new Option<FileInfo?>(
            name: "--ca-cert-file",
            description: "CA cert to use. If not set, a CA cert/key is generated.");
        var caKeyFileOption = new Option<FileInfo?>(
            name: "--ca-key-file",
            description: "CA key to use. Must be set if --ca-cert-file is, ignored otherwise.");
        cmd.SetHandler(Run, filePrefixOption, caCertFileOption, caKeyFileOption);
        return cmd;
    }

    private static void Run(string filePrefix, FileInfo? caCertFile, FileInfo? caKeyFile)
    {
        // Get CA
        using var caCert = LoadOrGenerateCACert(filePrefix, caCertFile, caKeyFile);

        // Create cert signed by CA valid for 5 years
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=client cert, O=My Org", ecdsa, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { ServerAuthOid, ClientAuthOid }, false));
        var serial = new byte[8];
        RandomNumberGenerator.Fill(serial);
        using var cert = req.Create(
            issuerCertificate: caCert,
            notBefore: DateTimeOffset.UtcNow - TimeSpan.FromDays(5),
            notAfter: DateTimeOffset.UtcNow + TimeSpan.FromDays(365 * 5),
            serialNumber: serial).CopyWithPrivateKey(ecdsa);

        // Write to files
        WriteCerts($"{filePrefix}client", cert);
    }

    private static X509Certificate2 LoadOrGenerateCACert(
        string filePrefix, FileInfo? caCertFile, FileInfo? caKeyFile)
    {
        // Load if files are present
        if (caCertFile != null)
        {
            if (caKeyFile == null)
            {
                throw new ArgumentException("Missing CA key file with CA cert file");
            }
            return X509Certificate2.CreateFromPemFile(caCertFile.FullName, caKeyFile.FullName);
        }

        // Build request
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=root cert, O=My Org", ecdsa, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: true,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, false));
        req.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { ServerAuthOid, ClientAuthOid }, false));

        // Self-sign with 10 years in either direction since it's the root
        var cert = req.CreateSelfSigned(
            notBefore: DateTimeOffset.UtcNow - TimeSpan.FromDays(365 * 10),
            notAfter: DateTimeOffset.UtcNow + TimeSpan.FromDays(365 * 10));

        // Write to files
        WriteCerts($"{filePrefix}client-ca", cert);
        return cert;
    }

    private static void WriteCerts(string prefix, X509Certificate2 cert)
    {
        var certPath = $"{prefix}-cert.pem";
        if (File.Exists(certPath))
        {
            throw new InvalidOperationException($"File {certPath} already exists");
        }
        var keyPath = $"{prefix}-key.pem";
        if (File.Exists(keyPath))
        {
            throw new InvalidOperationException($"File {keyPath} already exists");
        }
        File.WriteAllText(certPath, cert.ExportCertificatePem());
        File.WriteAllText(keyPath, cert.GetECDsaPrivateKey()!.ExportPkcs8PrivateKeyPem());
    }
}