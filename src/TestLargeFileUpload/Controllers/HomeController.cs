using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.Dnx.Runtime;
using Raven.Client.FileSystem;
using Raven.Json.Linq;

namespace TestLargeFileUpload.Controllers
{
    public class HomeController : Controller
    {
        private readonly IFilesStore _filesStore;
        private readonly IApplicationEnvironment _appEnvironment;

        public HomeController(IFilesStore filesStore, IApplicationEnvironment appEnvironment)
        {
            _filesStore = filesStore;
            _appEnvironment = appEnvironment;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload()
        {
            
            var extension = "bin";
            var form = await Context.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null) return this.HttpBadRequest(new { Message = "Missing file" });

            var name = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.{extension ?? "unknown"}";




            string filename = $"uploads/{name}";
            await _filesStore.AsyncFilesCommands.UploadRawAsync(filename,
                file.OpenReadStream(),
                new RavenJObject() { { "Raven-Creation-Date", DateTime.UtcNow } },
                file.Length
                );

            return Json(new { name });
        }

        [HttpPost]
        public async Task<IActionResult> UploadToDisk()
        {
            
            var extension = "bin";
            var form = await Context.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null) return this.HttpBadRequest(new { Message = "Missing file" });

            var name = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.{extension ?? "unknown"}";


            using (var f = System.IO.File.OpenWrite($"{_appEnvironment.ApplicationBasePath}\\App_Data\\{name}"))
            using (var s = file.OpenReadStream())
            {
                await s.CopyToAsync(f);
            }

            return Json(new { name });
        }
    }
}
