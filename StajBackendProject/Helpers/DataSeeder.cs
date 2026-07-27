using Microsoft.EntityFrameworkCore;
using StajBackendProject.Interfaces;
using StajBackendProject.Models;

namespace StajBackendProject.Helpers
{
    public static class DataSeeder
    {
        public static async Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UsersContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            await context.Database.MigrateAsync();

            if (!await context.Roles.AnyAsync())
            {
                var roles = new List<Role>
                {
                    new Role { Name = "Admin" },
                    new Role { Name = "Manager" },
                    new Role { Name = "User" }
                };

                await context.Roles.AddRangeAsync(roles);
                await context.SaveChangesAsync();
            }

            var adminEmail = "admin@system.com";

            if (!await context.Users.AnyAsync(u => u.Email == adminEmail))
            {
                var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");

                var adminUser = new User
                {
                    FirstName = "System",
                    LastName = "Admin",
                    Email = adminEmail,
                    PhoneNumber = "05555555555",
                    IsActive = true,
                    InsertDate = DateTime.UtcNow,
                    PasswordHash = hasher.Hash("AdminPassword123!"),
                    Roles = new List<Role>()
                };

                if (adminRole != null)
                {
                    adminUser.Roles.Add(adminRole);
                }

                await context.Users.AddAsync(adminUser);
                await context.SaveChangesAsync();
            }
        }
    }
}