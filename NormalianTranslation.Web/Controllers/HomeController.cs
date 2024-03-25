using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using NormalianTranslation.Web.Models;
using NormalianTranslation.Web.Utilities;
using System.Diagnostics;

namespace NormalianTranslation.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // https://www.base64decode.org/
        // https://jsonformatter.org/
        // https://learn.microsoft.com/en-us/azure/app-service/tutorial-auth-aad?pivots=platform-windows
        // https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-user-identities
        //{
        //  "typ": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
        //  "val": "mineaduser@normalian.xyz"
        //},
        //{
        //  "typ": "name",
        //  "val": "mineaduser-normalian"
        //},
        //{
        //"typ": "preferred_username",
        //  "val": "mineaduser@normalian.xyz"
        //},
        public IActionResult Index()
        {
#if DEBUG == false
            // App Service EasyAuth gives this token
            var claims = ClaimsPrincipalParser.Parse(Request);
            ViewBag.UserEmail = claims.Claims.FirstOrDefault(_ => _.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress").Value;
#endif
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> UploadFiles(IFormCollection collection)
        {
            string success = string.Empty;
            string error = string.Empty;
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(await KeyVaultService.GetSecret("STORAGE-CONNECTIONSTRING"));
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("source");

                long size = 0;
                foreach (var file in collection.Files)
                {
                    _logger.LogInformation($"{file.FileName} is uploading.");
                    string username = "daisami";
#if DEBUG == true
#elif DEBUG == false
                    var claims = ClaimsPrincipalParser.Parse(Request);
                    var emailaddress = claims.Claims.FirstOrDefault(_ => _.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress").Value;
                    username = emailaddress.Substring(0, emailaddress.IndexOf('@'));
#endif
                    var response = await containerClient.UploadBlobAsync($"{username}/{file.FileName}", file.OpenReadStream());
                    size += file.Length;

                    // BLOB properties will remain even if the BLOL is deleted. Need to delete BLOB properties
                    var targetLang = collection["target_language"];
                    IDictionary<string, string> metadata =
                       new Dictionary<string, string>();

                    // Add some metadata to the container.
                    metadata.Add("target_language", targetLang);

                    // Set the container's metadata.
                    await containerClient.GetBlobClient($"{username}/{file.FileName}").SetMetadataAsync(metadata);

                    success = $"{collection.Files.Count} file(s) / {size} bytes uploaded successfully!";
                    _logger.LogInformation(success);
                }
            }
            catch (Exception ex)
            {
                error = $"Error is happened: {ex.Message}";
                _logger.LogError(ex, error);
            }

            return Json(new
            {
                message = success,
                error = error
            });
        }

    }
}
