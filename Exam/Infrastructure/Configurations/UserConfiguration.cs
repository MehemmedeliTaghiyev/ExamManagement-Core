using Exam.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exam.Infrastructure.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(u => u.Id);

            builder.Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(150);

            builder.HasIndex(u => u.Email)
                .IsUnique();

            builder.Property(u => u.PasswordHash)
                .IsRequired();

            builder.Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(u => u.IsAccessEnabled)
                .HasDefaultValue(true);

            builder.Property(u => u.IsDeleted)
                .HasDefaultValue(false);

            builder.HasQueryFilter(u => !u.IsDeleted);

            builder.Property(u => u.UserName)
                .HasMaxLength(80);

            builder.HasIndex(u => u.UserName)
                .IsUnique()
                .HasFilter("[UserName] IS NOT NULL AND [UserName] <> N''");

            builder.Property(u => u.GroupName)
                .HasMaxLength(80);

            builder.Property(u => u.FirstName).HasMaxLength(80);
            builder.Property(u => u.LastName).HasMaxLength(80);
            builder.Property(u => u.Phone).HasMaxLength(40);
            builder.Property(u => u.TrialMessage).HasMaxLength(1000);

            builder.HasIndex(u => u.TeacherId);
        }
    }
}
