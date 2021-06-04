using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
    }

    public class ApplicationUser : IdentityUser
    {
        public string CompanyName { get; set; }

        public string CompanyOrgNo { get; set; }
        
        public int? ZgConnectionId { get; set; }
        
        public string ZgPublicKey { get; set; }

        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }
    }
}