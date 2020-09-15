using Microsoft.AspNetCore.Hosting;
using Zwapstore.Areas.Identity;

[assembly: HostingStartup(typeof(IdentityHostingStartup))]
namespace Zwapstore.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) => {
            });
        }
    }
}