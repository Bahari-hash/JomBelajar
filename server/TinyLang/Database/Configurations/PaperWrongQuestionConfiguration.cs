using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

public sealed class PaperWrongQuestionConfiguration
    : IEntityTypeConfiguration<PaperWrongQuestion>
{
    public void Configure(EntityTypeBuilder<PaperWrongQuestion> builder)
    {
        builder.ToTable("paper_wrong_questions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        builder.HasIndex(x => new { x.UserId, x.QuestionId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status, x.LastWrongAt, x.Id });
        builder.HasOne(x => x.User)
            .WithMany(x => x.PaperWrongQuestions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Question)
            .WithMany(x => x.WrongQuestions)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
