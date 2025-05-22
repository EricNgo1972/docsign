// In Program.cs, minimal listener for /sign-pdf with certificate selection and PIN prompt
using System.Security.Cryptography.X509Certificates;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Security;
using System.Windows.Forms; // Needs reference to System.Windows.Forms

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});


var app = builder.Build();
app.UseCors();

X509Certificate2 SelectCertificateFromStore()
{
    X509Certificate2 selectedCert = null;
    var thread = new Thread(() =>
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
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
            "Choose a certificate for signing.",
            X509SelectionFlag.SingleSelection
        );
        if (selected.Count > 0)
            selectedCert = selected[0];
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    return selectedCert ?? throw new Exception("Certificate not selected.");
}

app.MapPost("/sign-pdf", async (HttpContext ctx) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    byte[] pdfBytes = Convert.FromBase64String(body);

    var cert = SelectCertificateFromStore(); // Pops up cert picker, triggers token PIN dialog

    using var msIn = new MemoryStream(pdfBytes);
    using var msOut = new MemoryStream();
    var doc = new PdfLoadedDocument(msIn);

    var sigField = doc.Form.Fields.OfType<PdfLoadedSignatureField>().FirstOrDefault(f => f.IsBlank);

    if (sigField == null)
        return Results.BadRequest("No empty signature field found in PDF.");

    var signer = new PdfSignature(doc, sigField, cert, "Local Signature");
    signer.Settings.CryptographicStandard = CryptographicStandard.CADES;
    signer.Settings.DigestAlgorithm = DigestAlgorithm.Sha256;

    doc.Save(msOut);
    doc.Close(true);

    return Results.Text(Convert.ToBase64String(msOut.ToArray()), "text/plain");
});

app.Run("http://localhost:5005");
