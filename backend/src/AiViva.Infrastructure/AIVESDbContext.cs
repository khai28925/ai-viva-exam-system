using System;
using System.Collections.Generic;
using System.Text;
using AiViva.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure
{
    public class AIVESDbContext : DbContext
    {
        public AIVESDbContext(DbContextOptions<AIVESDbContext> options) : base(options) { }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionBank> QuestionBanks { get; set; }
    }
}
