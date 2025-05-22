using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Security;
using System.Windows.Forms; // Add reference to System.Windows.Forms

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Ensure this is called from an STA thread (WinForms requirement)
void EnsureSTAThread(Action action)
{
    var thread = new Thread(() => action());
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
}

X509Certificate2 SelectCertificateFromStore()
{
    X509Certificate2 selectedCert = null;
    EnsureSTAThread(() =>
    {
        var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        var certs = store.Certificates
            .OfType<X509Certificate2>()
            .Where(c => c.HasPrivateKey)
            .ToArray();

        if (certs.Length == 0)
            throw new Exception("No certificates with private keys found.");

        var selected = X509Certificate2UI.SelectFromCollection(
            new X509Certificate2Collection(certs),
            "Select Certificate",
            "Please select a certificate for signing:",
            X509SelectionFlag.SingleSelection
        );

        if (selected.Count > 0)
            selectedCert = selected[0];
    });
    return selectedCert ?? throw new Exception("Certificate not selected.");
}

app.MapPost("/sign-pdf", async (HttpContext ctx) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    byte[] pdfBytes = Convert.FromBase64String(body);

    var cert = SelectCertificateFromStore(); // <-- Shows the popup!

    using var msIn = new MemoryStream(pdfBytes);
    using var msOut = new MemoryStream();
    var doc = new PdfLoadedDocument(msIn);

    // Find the first available signature field
    var sigField = doc.Form.Fields
        .OfType<PdfLoadedSignatureField>()
        .FirstOrDefault(f => f.IsBlank);

    if (sigField == null)
        return Results.BadRequest("No empty signature field found in PDF.");

    var signer = new PdfSignature(doc, sigField, cert, "Server Signature");
    signer.Settings.CryptographicStandard = CryptographicStandard.CADES; // or PKCS7
    signer.Settings.DigestAlgorithm = DigestAlgorithm.Sha256;

    doc.Save(msOut);
    doc.Close(true);

    // Return signed PDF as base64
    return Results.Text(Convert.ToBase64String(msOut.ToArray()), "text/plain");
});

// ... other endpoints as needed

app.Run("http://localhost:5005");
