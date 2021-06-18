using Microsoft.AspNetCore.Hosting;
using Marketplace.Areas.Identity;

[assembly: HostingStartup(typeof(IdentityHostingStartup))]
namespace Marketplace.Areas.Identity
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