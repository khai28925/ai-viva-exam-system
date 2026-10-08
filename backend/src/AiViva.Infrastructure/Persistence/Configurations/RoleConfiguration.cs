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
        builder.Property(role => role.Name).HasColumnName("name").HasMaxLength(80);
        builder.Property(role => role.Description).HasColumnName("description").HasMaxLength(255);
        builder.HasIndex(role => role.Code).IsUnique();

        builder.HasData(
            new { Id = Guid.Parse("a8697244-6c13-4de1-9c13-bc6a840179c1"), Code = Role.Codes.Admin, Name = "Administrator", Description = "Manages users and all exam content." },
            new { Id = Guid.Parse("a8697244-6c13-4de1-9c13-bc6a840179c2"), Code = Role.Codes.Lecturer, Name = "Lecturer", Description = "Creates question banks and conducts exams." },
            new { Id = Guid.Parse("a8697244-6c13-4de1-9c13-bc6a840179c3"), Code = Role.Codes.Student, Name = "Student", Description = "Participates in assigned exams." });
    }
}
