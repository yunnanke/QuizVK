using Microsoft.EntityFrameworkCore;
using QuizMvp.Models;

namespace QuizMvp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<AnswerOption> AnswerOptions { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<ParticipantAnswer> ParticipantAnswers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("public");

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(user => user.Id);
                entity.Property(user => user.Username).HasColumnType("character varying").IsRequired();
                entity.Property(user => user.Email).HasColumnType("character varying").IsRequired();
                entity.Property(user => user.PasswordHash).HasColumnType("character varying").IsRequired();
                entity.Property(user => user.Role).HasColumnType("character varying").IsRequired();
            });

            modelBuilder.Entity<Quiz>(entity =>
            {
                entity.ToTable("Quizzes");
                entity.HasKey(quiz => quiz.Id);
                entity.Property(quiz => quiz.Title).HasColumnType("character varying").IsRequired();
                entity.Property(quiz => quiz.Category).HasColumnType("character varying").IsRequired();
                entity.Property(quiz => quiz.Rules).HasColumnType("text").IsRequired();
                entity.HasOne(quiz => quiz.Creator)
                    .WithMany()
                    .HasForeignKey(quiz => quiz.CreatorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Question>(entity =>
            {
                entity.ToTable("Questions");
                entity.HasKey(question => question.Id);
                entity.Property(question => question.Text).HasColumnType("text").IsRequired();
                entity.HasOne(question => question.Quiz)
                    .WithMany(quiz => quiz.Questions)
                    .HasForeignKey(question => question.QuizId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AnswerOption>(entity =>
            {
                entity.ToTable("AnswerOptions");
                entity.HasKey(option => option.Id);
                entity.Property(option => option.Text).HasColumnType("text").IsRequired();
                entity.HasOne(option => option.Question)
                    .WithMany(question => question.Options)
                    .HasForeignKey(option => option.QuestionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Session>(entity =>
            {
                entity.ToTable("Sessions");
                entity.HasKey(session => session.Id);
                entity.Property(session => session.JoinCode).HasColumnType("character varying").IsRequired();
                entity.HasIndex(session => session.JoinCode).IsUnique();
                entity.HasOne(session => session.Quiz)
                    .WithMany(quiz => quiz.Sessions)
                    .HasForeignKey(session => session.QuizId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ParticipantAnswer>(entity =>
            {
                entity.ToTable("ParticipantAnswers");
                entity.HasKey(answer => answer.Id);
                entity.Property(answer => answer.SelectedOptionIds).HasColumnType("text").IsRequired();
                entity.HasOne(answer => answer.Session)
                    .WithMany(session => session.ParticipantAnswers)
                    .HasForeignKey(answer => answer.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(answer => answer.Question)
                    .WithMany()
                    .HasForeignKey(answer => answer.QuestionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(answer => answer.User)
                    .WithMany()
                    .HasForeignKey(answer => answer.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
