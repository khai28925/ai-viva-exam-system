using AiViva.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiViva.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(role => role.Code).HasColumnName("code").HasMaxLength(16).IsRequired();
        builder.HasIndex(role => role.Code).IsUnique();

        builder.HasData(
            new { Id = Guid.Parse("a8697244-6c13-4de1-9c13-bc6a840179c1"), Code = Role.Codes.Admin },
            new { Id = Guid.Parse("a8697244-6c13-4de1-9c13-bc6a840179c2"), Code = Role.Codes.Lecturer },
            new { Id = Guid.Parse("a8697244-6c13-4de1-9c13-bc6a840179c3"), Code = Role.Codes.Student });
    }
}
