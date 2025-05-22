// Minimal C# Local Signing Service using Windows Certificate Store
// Endpoint: /sign-pdf, /sign-xml

using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string CertMatchName = "USB Token Cert"; // Mocked matching criteria

X509Certificate2 GetCertificateFromStore()
{
    var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    var cert = store.Certificates
        .OfType<X509Certificate2>()
        .FirstOrDefault(c => c.Subject.Contains(CertMatchName) && c.HasPrivateKey);

    if (cert == null) throw new Exception("Certificate not found.");
    return cert;
}

app.MapPost("/sign-pdf", async (HttpContext ctx) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    byte[] dataToSign = Convert.FromBase64String(body);
    var cert = GetCertificateFromStore();
    using var rsa = cert.GetRSAPrivateKey();
    byte[] signature = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return Results.Ok(Convert.ToBase64String(signature));
});

app.MapPost("/sign-xml", async (HttpContext ctx) =>
{
    string xmlContent = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var xmlDoc = new XmlDocument();
    xmlDoc.PreserveWhitespace = false;
    xmlDoc.LoadXml(xmlContent);

    var cert = GetCertificateFromStore();
    var signedXml = new SignedXml(xmlDoc);
    signedXml.SigningKey = cert.GetRSAPrivateKey();

    var reference = new Reference("");
    reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
    signedXml.AddReference(reference);

    signedXml.ComputeSignature();
    XmlElement xmlSignature = signedXml.GetXml();
    xmlDoc.DocumentElement?.AppendChild(xmlDoc.ImportNode(xmlSignature, true));

    return Results.Text(xmlDoc.OuterXml, "application/xml");
});

app.Run("http://localhost:5005");
