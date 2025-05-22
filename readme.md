This protopty is used for manage/ sign pdf, xml file from a blazor web app. 
Web app works in a sandbox, which can not reach digital certificates on device.

### Sign Pdf with example payload

```
POST http://localhost:5005/sign-pdf
Body: "base64-encoded hash or byte range"
```

### Sign XML with example payload

```xml
POST http://localhost:5005/sign-xml
Body:
<Invoice>
  <ID>12345</ID>
  <Total>199.99</Total>
</Invoice>
```
